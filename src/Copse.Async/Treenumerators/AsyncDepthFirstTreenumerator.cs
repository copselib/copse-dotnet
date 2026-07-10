using Copse.Core;
using Copse.Core.Async;
using Copse.Traversal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Async.Treenumerators
{
  /// <summary>
  /// Depth-first <b>async</b> treenumerator in the <b>direct style</b>: the natural inlined control flow
  /// (OnScheduling / OnVisiting / Backtrack / TryPushNextChild) over the shared color-agnostic
  /// <see cref="DepthFirstPathState{TNode, TEnumerator}"/>, with the two I/O seams (child pull,
  /// root pull) PROBED so a pull that completes inline costs no state machine at all (the
  /// fast-path probe idiom -- see AsyncToSync). No inverted cadence.
  ///
  /// <para><b>This is the single source of truth.</b> The probe guards vanish in transcription and
  /// the two seams collapse to synchronous pulls, yielding exactly
  /// <c>Copse.Treenumerators.DepthFirstTreenumerator</c> -- which benchmarks at parity with the
  /// hand-tuned original engine (1.02x), where an inverted cadence cost 1.61x.</para>
  /// </summary>
  public sealed class AsyncDepthFirstTreenumerator<TValue, TNode, TAsyncChildEnumerator>
    : IAsyncTreenumerator<TValue>
    where TAsyncChildEnumerator : IAsyncChildEnumerator<TNode>
  {
    public AsyncDepthFirstTreenumerator(
      IAsyncEnumerable<TNode> rootNodes,
      Func<NodeContext<TNode>, TAsyncChildEnumerator> childEnumeratorFactory,
      Func<TNode, TValue> map)
    {
      _RootsEnumerator = rootNodes.GetAsyncEnumerator();
      _Path = new DepthFirstPathState<TNode, TAsyncChildEnumerator>(childEnumeratorFactory);
      _Map = map;
    }

    private readonly IAsyncEnumerator<TNode> _RootsEnumerator;
    private DepthFirstPathState<TNode, TAsyncChildEnumerator> _Path;
    private readonly Func<TNode, TValue> _Map;

    private bool _Finished;
    private bool _RootsEnumeratorFinished;

    public TValue Node { get; private set; } = default;
    public int VisitCount { get; private set; } = 0;
    public TreenumeratorMode Mode { get; private set; } = default;
    public NodePosition Position { get; private set; } = NodePosition.ForestRoot;

    // NOT async, and neither are the helpers below: both pulls are PROBED, and a pull that
    // completes inline stays ordinary method calls with no state machine -- the fast-path probe
    // idiom (see AsyncToSync). Unlike a store grow ("grow until", idempotent), a pull ADVANCES
    // its cursor, so a pending pull resumes through a continuation that CONSUMES the pulled
    // result and runs its caller's tail -- never by re-entering the probing method.
    public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Finished)
        return new ValueTask<bool>(false);

      var moved = OnMoveNextAsync(nodeTraversalStrategies);

      if (!moved.IsCompletedSuccessfully)
        return AwaitThenFinishMoveNextAsync(moved);

      if (!moved.Result)
        _Finished = true;

      return new ValueTask<bool>(moved.Result);
    }

    private ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Path.IsEmpty)
        return MoveToNextRootNodeAsync();

      if (Mode == TreenumeratorMode.SchedulingNode)
        return OnSchedulingAsync(nodeTraversalStrategies);

      return OnVisitingAsync();
    }

    private ValueTask<bool> MoveToNextRootNodeAsync()
    {
      if (_RootsEnumeratorFinished)
        return new ValueTask<bool>(false);

      var moved = _RootsEnumerator.MoveNextAsync();

      if (!moved.IsCompletedSuccessfully)
        return AwaitThenPushRootAsync(moved);

      if (!moved.Result)
        return new ValueTask<bool>(false);

      Publish(ref _Path.PushRoot(_RootsEnumerator.Current));
      return new ValueTask<bool>(true);
    }

    private ValueTask<bool> OnSchedulingAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        if (_Path.SkipRemainingSiblings())
          _RootsEnumeratorFinished = true;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
        return BacktrackAsync();

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Path.SkipCurrentNode();

        var pushed = TryPushNextChildAsync();

        if (!pushed.IsCompletedSuccessfully)
          return AwaitPushThenBacktrackAsync(pushed);

        if (pushed.Result)
          return new ValueTask<bool>(true);

        return BacktrackAsync();
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        _Path.DisposeCurrentEnumerator();

      Publish(ref _Path.TakeNextVisit());
      return new ValueTask<bool>(true);
    }

    private ValueTask<bool> OnVisitingAsync()
    {
      var pushed = TryPushNextChildAsync();

      if (!pushed.IsCompletedSuccessfully)
        return AwaitPushThenBacktrackAsync(pushed);

      if (pushed.Result)
        return new ValueTask<bool>(true);

      return BacktrackAsync();
    }

    private ValueTask<bool> BacktrackAsync()
    {
      while (true)
      {
        switch (_Path.PopFinishedLevelAndClassify())
        {
          case DepthFirstBacktrackStep.GoToRoot:
            return MoveToNextRootNodeAsync();

          case DepthFirstBacktrackStep.PromoteNextChild:
            var pushed = TryPushNextChildAsync();

            if (!pushed.IsCompletedSuccessfully)
              return AwaitPushThenBacktrackAsync(pushed);

            if (pushed.Result)
              return new ValueTask<bool>(true);

            continue;

          default: // EmitReturnVisit
            Publish(ref _Path.TakeNextVisit());
            return new ValueTask<bool>(true);
        }
      }
    }

    // THE SEAM: the child pull, probed. The pulled result lands through the same consume helper
    // whether the pull answered inline or through the pending continuation.
    private ValueTask<bool> TryPushNextChildAsync()
    {
      var result = _Path.TopEnumerator.MoveNextAsync();

      if (!result.IsCompletedSuccessfully)
        return AwaitThenPushPulledChildAsync(result);

      return new ValueTask<bool>(TryPushPulledChild(result.Result));
    }

    // Land a pulled child on the path; false when the enumerator was exhausted.
    private bool TryPushPulledChild(ChildResult<TNode> result)
    {
      if (!result.HasChild)
        return false;

      Publish(ref _Path.PushChild(result.Child.Node, result.Child.SiblingIndex));
      return true;
    }

    // codegen: begin async-only
    //
    // The suspension continuations. Both pulls ADVANCE their cursor, so every continuation
    // CONSUMES the pulled result and runs its caller's tail rather than re-entering the probing
    // method. Awaiting a pending push then falling into Backtrack is exactly the fast path's
    // push-failed handling -- and, from inside Backtrack, its loop's `continue` (the next
    // iteration pops the level whose push just came up empty).
    private async ValueTask<bool> AwaitThenFinishMoveNextAsync(ValueTask<bool> pendingMove)
    {
      var moved = await pendingMove.ConfigureAwait(false);

      if (!moved)
        _Finished = true;

      return moved;
    }

    private async ValueTask<bool> AwaitThenPushRootAsync(ValueTask<bool> pendingMove)
    {
      if (!await pendingMove.ConfigureAwait(false))
        return false;

      Publish(ref _Path.PushRoot(_RootsEnumerator.Current));
      return true;
    }

    private async ValueTask<bool> AwaitThenPushPulledChildAsync(ValueTask<ChildResult<TNode>> pendingPull)
    {
      return TryPushPulledChild(await pendingPull.ConfigureAwait(false));
    }

    private async ValueTask<bool> AwaitPushThenBacktrackAsync(ValueTask<bool> pendingPush)
    {
      if (await pendingPush.ConfigureAwait(false))
        return true;

      return await BacktrackAsync().ConfigureAwait(false);
    }
    // codegen: end async-only

    private void Publish(ref DepthFirstNodeState<TNode> node)
    {
      Mode = node.VisitCount == 0 ? TreenumeratorMode.SchedulingNode : TreenumeratorMode.VisitingNode;
      Node = _Map(node.Node);
      VisitCount = node.VisitCount;
      Position = node.Position;
    }

    public async ValueTask DisposeAsync()
    {
      _Path.Dispose();
      await _RootsEnumerator.DisposeAsync().ConfigureAwait(false);
    }
  }
}
