using Copse.Core;
using Copse.Core.Async;
using Copse.Traversal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Async
{
  /// <summary>
  /// Depth-first <b>async</b> treenumerator in the <b>direct style</b>: the natural inlined control flow
  /// (OnScheduling / OnVisiting / Backtrack / TryPushNextChild), with <c>await</c> at the two I/O seams
  /// (child pull, root pull), over the shared color-agnostic
  /// <see cref="DepthFirstPathState{TNode, TEnumerator}"/>. No inverted cadence.
  ///
  /// <para><b>This is the single source of truth.</b> Strip the <c>await</c>s and the two async seams
  /// collapse to synchronous pulls, yielding exactly
  /// <c>Copse.Treenumerators.DepthFirstDirectTreenumerator</c> -- which benchmarks at parity with the
  /// hand-tuned engine (1.02x), where the inverted cadence cost 1.61x. A Roslyn async→sync generator
  /// (Npgsql pattern) would emit that sync twin from this file.</para>
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

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Finished)
        return false;

      var moved = await OnMoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false);

      if (!moved)
        _Finished = true;

      return moved;
    }

    private ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Path.IsEmpty)
        return MoveToNextRootNodeAsync();

      if (Mode == TreenumeratorMode.SchedulingNode)
        return OnSchedulingAsync(nodeTraversalStrategies);

      return OnVisitingAsync();
    }

    private async ValueTask<bool> MoveToNextRootNodeAsync()
    {
      if (_RootsEnumeratorFinished || !await _RootsEnumerator.MoveNextAsync().ConfigureAwait(false))
        return false;

      Publish(ref _Path.PushRoot(_RootsEnumerator.Current));
      return true;
    }

    private async ValueTask<bool> OnSchedulingAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        if (_Path.SkipRemainingSiblings())
          _RootsEnumeratorFinished = true;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
        return await BacktrackAsync().ConfigureAwait(false);

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Path.SkipCurrentNode();

        if (await TryPushNextChildAsync().ConfigureAwait(false))
          return true;

        return await BacktrackAsync().ConfigureAwait(false);
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        _Path.DisposeCurrentEnumerator();

      Publish(ref _Path.TakeNextVisit());
      return true;
    }

    private async ValueTask<bool> OnVisitingAsync()
    {
      if (await TryPushNextChildAsync().ConfigureAwait(false))
        return true;

      return await BacktrackAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> BacktrackAsync()
    {
      while (true)
      {
        switch (_Path.PopFinishedLevelAndClassify())
        {
          case DepthFirstBacktrackStep.GoToRoot:
            return await MoveToNextRootNodeAsync().ConfigureAwait(false);

          case DepthFirstBacktrackStep.PromoteNextChild:
            if (await TryPushNextChildAsync().ConfigureAwait(false))
              return true;
            continue;

          default: // EmitReturnVisit
            Publish(ref _Path.TakeNextVisit());
            return true;
        }
      }
    }

    // THE SEAM: the ONLY line that differs from the sync twin -- an awaited pull instead of a sync one.
    private async ValueTask<bool> TryPushNextChildAsync()
    {
      if (!await _Path.TopEnumerator.MoveNextAsync().ConfigureAwait(false))
        return false;

      var child = _Path.TopEnumerator.Current;
      Publish(ref _Path.PushChild(child.Node, child.SiblingIndex));
      return true;
    }

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
