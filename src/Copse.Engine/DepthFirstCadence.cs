using Copse.Core;
using System;
using System.Runtime.CompilerServices;

namespace Copse.Engine
{
  /// <summary>What the driver must do next to keep the cadence turning (returned by <see cref="DepthFirstCadence{TNode, TEnumerator}.Advance"/>).</summary>
  public enum DepthFirstStep
  {
    /// <summary>A visit is ready. The driver publishes <see cref="DepthFirstCadence{TNode, TEnumerator}.Current"/> and returns true from its MoveNext(Async).</summary>
    Emit,
    /// <summary>The cadence needs the next child of the active level. The driver advances <see cref="DepthFirstCadence{TNode, TEnumerator}.TopEnumerator"/> (sync or async) and calls <see cref="DepthFirstCadence{TNode, TEnumerator}.SupplyChild"/>.</summary>
    NeedTopChild,
    /// <summary>The cadence needs the next root. The driver advances its roots enumerator (sync or async) and calls <see cref="DepthFirstCadence{TNode, TEnumerator}.SupplyRoot"/>.</summary>
    NeedRoot,
    /// <summary>The traversal is exhausted. The driver returns false.</summary>
    Done,
  }

  // The visit-state of one accepted node on the depth-first path (ported from DepthFirstPath).
  internal struct DepthFirstNodeState<TNode>
  {
    public DepthFirstNodeState(TNode node, NodePosition position)
    {
      Node = node;
      Position = position;
      VisitCount = 0;
    }

    public TNode Node;
    public NodePosition Position;
    public int VisitCount;
  }

  // What the level the engine just backtracked to needs next (from PopFinishedLevelAndClassify).
  internal enum DepthFirstBacktrackStep
  {
    GoToRoot,          // The whole forest path is unwound; schedule the next root.
    PromoteNextChild,  // No visit owed here; advance this level's enumerator.
    EmitReturnVisit,   // The accepted node here owes its next between/after-children visit.
  }

  /// <summary>
  /// The depth-first traversal <b>cadence</b> and all of its structural state, extracted from the
  /// treenumerator so a synchronous and an asynchronous driver can share it verbatim.
  ///
  /// <para><b>Sans-I/O, control flow and all.</b> The original <c>DepthFirstPath</c> already kept the
  /// path <i>state</i> sans-I/O and exposed the active child enumerator by <c>ref</c> for the driver to
  /// advance. But the driver still <i>called</i> that advance from inside its own <c>MoveNext</c>
  /// control flow -- so making the pull awaited would have colored the whole method chain async, and a
  /// shared cadence method could not be both sync and async. This class closes that gap by
  /// <b>inverting the seam</b>: instead of calling I/O, <see cref="Advance"/> <i>returns a request</i>
  /// (<see cref="DepthFirstStep.NeedTopChild"/> / <see cref="DepthFirstStep.NeedRoot"/>) and the thin
  /// driver performs the pull -- synchronously (<c>MoveNext</c>) or asynchronously
  /// (<c>await MoveNextAsync</c>) -- then resumes the cadence via <see cref="SupplyChild"/> /
  /// <see cref="SupplyRoot"/>. Every state transition here is pure synchronous bookkeeping.</para>
  ///
  /// <para>The resumable machine is small because both pull sites collapse: after any child pull, a
  /// child means "push it and emit its scheduling visit," and no child means "backtrack" -- uniformly,
  /// regardless of which control-flow site asked. Likewise every root pull is "push and emit" or
  /// "done."</para>
  ///
  /// <para><b>Prototype limitation.</b> Child enumerators are disposed synchronously here
  /// (<c>TEnumerator : IDisposable</c>). True <c>IAsyncDisposable</c> teardown means inverting disposal
  /// to the driver too (hand popped enumerators back to be awaited); deferred until Option B is
  /// validated. See the class comment tradeoff in the design notes.</para>
  /// </summary>
  internal struct DepthFirstCadence<TNode, TEnumerator> : IDisposable
    where TEnumerator : IDisposable
  {
    // Where the machine resumes on the next Advance. Backtrack is the only multi-step internal loop.
    private enum Phase
    {
      Dispatch,          // fresh MoveNext: route on empty/scheduling/visiting
      ResumeAfterChild,  // returned NeedTopChild; SupplyChild has set _PulledHasChild
      ResumeAfterRoot,   // returned NeedRoot; SupplyRoot has set _PulledHasRoot
      Backtrack,         // unwinding finished levels
      Done,
    }

    public DepthFirstCadence(Func<NodeContext<TNode>, TEnumerator> childEnumeratorFactory)
    {
      _ChildEnumeratorFactory = childEnumeratorFactory;
      _AcceptedNodes = new RefSemiDeque<DepthFirstNodeState<TNode>>();
      _Enumerators = new RefSemiDeque<TEnumerator>();
      _RootNodesSeen = 0;
      _DepthOfLastVisitedNode = -1;
      _RootsFinished = false;
      _CurrentIsScheduling = false;
      _Phase = Phase.Dispatch;
      _PendingStrategies = NodeTraversalStrategies.TraverseAll;
      _PulledHasChild = false;
      _PulledChildNode = default;
      _PulledChildSiblingIndex = 0;
      _PulledHasRoot = false;
      _PulledRootNode = default;
    }

    private readonly Func<NodeContext<TNode>, TEnumerator> _ChildEnumeratorFactory;

    // --- Path state (ported verbatim from DepthFirstPath) ---
    private readonly RefSemiDeque<DepthFirstNodeState<TNode>> _AcceptedNodes;
    private readonly RefSemiDeque<TEnumerator> _Enumerators;
    private int _RootNodesSeen;
    private int _DepthOfLastVisitedNode;

    // --- Cadence state ---
    private bool _RootsFinished;
    private bool _CurrentIsScheduling;   // whether the last-emitted visit was a scheduling node
    private Phase _Phase;
    private NodeTraversalStrategies _PendingStrategies;
    private bool _PulledHasChild;
    private TNode _PulledChildNode;
    private int _PulledChildSiblingIndex;
    private bool _PulledHasRoot;
    private TNode _PulledRootNode;

    private int Depth => _Enumerators.Count - 1;
    private bool IsEmpty => _Enumerators.Count == 0;

    /// <summary>The active level's child enumerator, by ref so the driver advances it in place. THE I/O SEAM.</summary>
    public ref TEnumerator TopEnumerator
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref _Enumerators.GetLast();
    }

    /// <summary>The node whose visit the driver should publish when <see cref="Advance"/> returns <see cref="DepthFirstStep.Emit"/>.</summary>
    public ref DepthFirstNodeState<TNode> Current
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref _AcceptedNodes.GetLast();
    }

    /// <summary>Begin one MoveNext step: stash the consumer's strategy (it applies to the pending scheduling node) and route to a fresh dispatch.</summary>
    public void BeginMove(NodeTraversalStrategies strategies)
    {
      _PendingStrategies = strategies;
      if (_Phase != Phase.Done)
        _Phase = Phase.Dispatch;
    }

    /// <summary>Resume after the driver advanced <see cref="TopEnumerator"/>.</summary>
    public void SupplyChild(bool hasChild, TNode node, int siblingIndex)
    {
      _PulledHasChild = hasChild;
      _PulledChildNode = node;
      _PulledChildSiblingIndex = siblingIndex;
    }

    /// <summary>Resume after the driver advanced its roots enumerator.</summary>
    public void SupplyRoot(bool hasRoot, TNode node)
    {
      _PulledHasRoot = hasRoot;
      _PulledRootNode = node;
    }

    /// <summary>
    /// Advance the pure state machine one step. Loops internally only across cost-free transitions
    /// (entering/continuing backtracking); anything that needs I/O or yields a visit returns.
    /// </summary>
    public DepthFirstStep Advance()
    {
      while (true)
      {
        switch (_Phase)
        {
          case Phase.Dispatch:
            if (IsEmpty)
              return RequestRootOrDone();

            if (_CurrentIsScheduling)
            {
              // OnScheduling: apply the consumer strategy to the node just scheduled.
              if (_PendingStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
                if (SkipRemainingSiblings())
                  _RootsFinished = true;

              // SkipNodeAndDescendants is a superset of SkipNode, so it must be tested first.
              if (_PendingStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
              {
                _Phase = Phase.Backtrack;
                continue;
              }

              if (_PendingStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
              {
                SkipCurrentNode();
                _Phase = Phase.ResumeAfterChild;   // promote a child into the swallowed slot
                return DepthFirstStep.NeedTopChild;
              }

              if (_PendingStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
                DisposeCurrentEnumerator();

              // Accept: emit the node's first visiting visit.
              return EmitTakeVisit();
            }

            // OnVisiting: a visiting visit was just emitted -- descend into the next child.
            _Phase = Phase.ResumeAfterChild;
            return DepthFirstStep.NeedTopChild;

          case Phase.ResumeAfterChild:
            if (_PulledHasChild)
              return EmitPushChild();
            _Phase = Phase.Backtrack;   // no child to promote/descend into -> unwind
            continue;

          case Phase.ResumeAfterRoot:
            if (_PulledHasRoot)
              return EmitPushRoot();
            _RootsFinished = true;
            _Phase = Phase.Done;
            return DepthFirstStep.Done;

          case Phase.Backtrack:
            switch (PopFinishedLevelAndClassify())
            {
              case DepthFirstBacktrackStep.GoToRoot:
                return RequestRootOrDone();

              case DepthFirstBacktrackStep.PromoteNextChild:
                _Phase = Phase.ResumeAfterChild;
                return DepthFirstStep.NeedTopChild;

              default: // EmitReturnVisit
                return EmitTakeVisit();
            }

          default: // Done
            return DepthFirstStep.Done;
        }
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DepthFirstStep RequestRootOrDone()
    {
      if (_RootsFinished)
      {
        _Phase = Phase.Done;
        return DepthFirstStep.Done;
      }

      _Phase = Phase.ResumeAfterRoot;
      return DepthFirstStep.NeedRoot;
    }

    // Emit helpers: mutate the path, set _CurrentIsScheduling, hand back to the caller loop as Emit.
    private DepthFirstStep EmitPushChild()
    {
      PushLevel(_PulledChildNode, new NodePosition(_PulledChildSiblingIndex, Depth + 1));
      _CurrentIsScheduling = true;
      _Phase = Phase.Dispatch;   // next BeginMove re-enters here; harmless placeholder
      return DepthFirstStep.Emit;
    }

    private DepthFirstStep EmitPushRoot()
    {
      PushLevel(_PulledRootNode, new NodePosition(_RootNodesSeen++, 0));
      _CurrentIsScheduling = true;
      _Phase = Phase.Dispatch;
      return DepthFirstStep.Emit;
    }

    private DepthFirstStep EmitTakeVisit()
    {
      ref var node = ref _AcceptedNodes.GetLast();
      node.VisitCount++;
      _DepthOfLastVisitedNode = node.Position.Depth;
      _CurrentIsScheduling = false;
      _Phase = Phase.Dispatch;
      return DepthFirstStep.Emit;
    }

    // --- Path operations (ported verbatim from DepthFirstPath) ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushLevel(TNode node, NodePosition position)
    {
      _AcceptedNodes.AddLast(new DepthFirstNodeState<TNode>(node, position));
      _Enumerators.AddLast(_ChildEnumeratorFactory(new NodeContext<TNode>(node, position)));
    }

    private void SkipCurrentNode() => _AcceptedNodes.RemoveLast();

    private void DisposeCurrentEnumerator() => _Enumerators.GetLast().Dispose();

    private DepthFirstBacktrackStep PopFinishedLevelAndClassify()
    {
      if (_AcceptedNodes.Count > 0 && _AcceptedNodes.GetLast().Position.Depth == _Enumerators.Count - 1)
        _AcceptedNodes.RemoveLast();

      _Enumerators.RemoveLast().Dispose();

      var depth = _Enumerators.Count - 1;

      if (depth < 0)
        return DepthFirstBacktrackStep.GoToRoot;

      if (depth == _DepthOfLastVisitedNode
        || _AcceptedNodes.Count == 0
        || _AcceptedNodes.GetLast().Position.Depth < depth)
        return DepthFirstBacktrackStep.PromoteNextChild;

      return DepthFirstBacktrackStep.EmitReturnVisit;
    }

    private bool SkipRemainingSiblings()
    {
      var wasEffectiveRoot = _AcceptedNodes.Count == 1;

      var parentDepth = wasEffectiveRoot ? 0 : _AcceptedNodes.GetFromBack(1).Position.Depth;
      var depthDelta = _Enumerators.Count - parentDepth;

      for (int i = 1; i < depthDelta; i++)
        _Enumerators.GetFromBack(i).Dispose();

      return wasEffectiveRoot;
    }

    public void Dispose()
    {
      if (_Enumerators == null)
        return;

      while (_Enumerators.Count > 0)
        _Enumerators.RemoveLast().Dispose();
    }
  }
}
