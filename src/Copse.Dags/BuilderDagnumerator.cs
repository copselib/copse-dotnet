using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Dags
{
  // The builder's demand-driven walk (THE LAZY BUILDER RULING, 2026-08-06,
  // docs/DAG_CONTRACT_DESIGN.md): Kahn's algorithm run on demand over the live node graph --
  // the visit protocol IS Kahn's trace (pop a ready node = entry; dispatch its out-edges =
  // discoveries, each decrementing a child's pending count; a child settling becomes ready).
  // No topological snapshot, no CSR arrays, no cycle check at acquisition: the eager
  // acquisition was a smuggled buffer, and cycle detection is STARVATION -- the ready stack
  // drains with members unsettled -- surfacing as DagCycleException at exhaustion, after the
  // maximal acyclic downward-closed prefix has been published, deterministically per drain.
  // Materialize is the validator; the completed buffer is the certificate.
  //
  // Acquisition runs ONE light counting pass (membership + member-in-degree, a visited-set
  // walk over child edges): the stray-parent affordance (a member may have a parent OUTSIDE
  // the dag, whose edges are not the dag's) makes in-degree a reachability fact, so it cannot
  // ride AddChild bookkeeping. No ordering and no validation happen here; everything after is
  // O(consumed).
  //
  // Ordinals are assigned at FIRST DISCOVERY (dense in discovery order): a lazy walk cannot
  // cite a node's future entry index, so the eager walk's entries-carry-increasing-ordinals
  // presentation is narrowed to the contract's real promise -- a stable per-enumeration
  // correlation key, entries in topological order.
  //
  // Liveness is the reference walk's, stated once there: a node enters iff at least one of
  // its discoveries was emitted and not severed; a dead or dispatch-suppressed node still
  // settles its targets' pending counts silently, cascading -- the stream shows only the
  // live structure. Entry discipline is depth-biased to match the eager walk's
  // discovery-order bias: each phase's newly ready nodes push onto the stack in reverse, so
  // the first-dispatched ready child is entered first and chains run deep before siblings.
  internal sealed class BuilderDagnumerator<TValue, TEdge> : IDagnumerator<TValue, TEdge>
  {
    public BuilderDagnumerator(IReadOnlyList<DagNode<TValue, TEdge>> sources)
    {
      // The counting pass: membership (reachable closure of the source list over child
      // edges) and member-in-degree. Order-independent; no cycle detection.
      var membershipWalk = new Stack<DagNode<TValue, TEdge>>();

      for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
      {
        if (_States.ContainsKey(sources[sourceIndex]))
          continue;

        _States.Add(sources[sourceIndex], new NodeState());
        membershipWalk.Push(sources[sourceIndex]);
      }

      while (membershipWalk.Count > 0)
      {
        var member = membershipWalk.Pop();

        foreach (var childEdge in member.ChildEdges)
        {
          if (!_States.TryGetValue(childEdge.Child, out var childState))
          {
            childState = new NodeState();
            _States.Add(childEdge.Child, childState);
            membershipWalk.Push(childEdge.Child);
          }

          childState.Pending++;
        }
      }

      // The walk's own sources are the members nothing points to; each carries one pending
      // conventional discovery (the sources-at-the-start convention), in source-list order.
      foreach (var source in sources)
      {
        var state = _States[source];

        if (state.Pending == 0 && !_WalkSources.Contains(source))
        {
          state.Pending = 1;
          _WalkSources.Add(source);
        }
      }

      // The pre-enumeration convention (the ForestRoot analog).
      Mode = DagnumeratorMode.DiscoveringNode;
      Ordinal = -1;
      ParentOrdinal = -1;
      EdgeIndex = 0;
    }

    private readonly Dictionary<DagNode<TValue, TEdge>, NodeState> _States =
      new(ReferenceEqualityComparer.Instance);
    private readonly List<DagNode<TValue, TEdge>> _WalkSources = new();
    private readonly Stack<DagNode<TValue, TEdge>> _Ready = new();
    private readonly List<DagNode<TValue, TEdge>> _ReadyBatch = new();

    private WalkPhase _Phase = WalkPhase.NotStarted;
    private int _SourceIndex;
    private int _NextOrdinal;
    private int _SettledCount;
    private DagNode<TValue, TEdge> _Current;
    private NodeState _CurrentState;
    private int _OutEdgeIndex;
    private bool _SuppressDispatch;
    private DagNode<TValue, TEdge> _LastDiscovered;

    public DagnumeratorMode Mode { get; private set; }
    public TValue Node { get; private set; }
    public int Ordinal { get; private set; }
    public TEdge Edge { get; private set; }
    public int ParentOrdinal { get; private set; }
    public int EdgeIndex { get; private set; }

    public bool MoveNext(DagTraversalStrategies strategies)
    {
      ApplyStrategiesToCurrentVisit(strategies);

      while (true)
      {
        switch (_Phase)
        {
          case WalkPhase.SourceDiscoveries when _SourceIndex < _WalkSources.Count:
            var source = _WalkSources[_SourceIndex];
            var sourceState = _States[source];
            sourceState.Pending--;
            PublishDiscovery(source, sourceState, parentOrdinal: -1, edgeIndex: _SourceIndex, edge: default);
            _SourceIndex++;
            return true;

          case WalkPhase.NotStarted:
          case WalkPhase.SourceDiscoveries:
          case WalkPhase.Entering:
            FlushReadyBatch();

            if (_Ready.Count == 0)
            {
              if (_SettledCount != _States.Count)
                throw new DagCycleException(DescribeStarvation());

              _Phase = WalkPhase.Done;
              continue;
            }

            _Current = _Ready.Pop();
            _CurrentState = _States[_Current];
            _SuppressDispatch = false;
            _OutEdgeIndex = 0;
            _SettledCount++;
            PublishEntry(_Current, _CurrentState);
            _Phase = WalkPhase.Dispatching;
            return true;

          case WalkPhase.Dispatching:
            if (_SuppressDispatch)
            {
              for (; _OutEdgeIndex < _Current.ChildEdges.Count; _OutEdgeIndex++)
                SettleSilently(_Current.ChildEdges[_OutEdgeIndex].Child);
            }

            if (_OutEdgeIndex < _Current.ChildEdges.Count)
            {
              var childEdge = _Current.ChildEdges[_OutEdgeIndex];
              var childState = _States[childEdge.Child];
              childState.Pending--;
              PublishDiscovery(childEdge.Child, childState, _CurrentState.Ordinal, _OutEdgeIndex, childEdge.Value);
              _OutEdgeIndex++;
              return true;
            }

            _Phase = WalkPhase.Entering;
            continue;

          case WalkPhase.Done:
            return false;

          default:
            throw new InvalidOperationException($"Unknown walk phase {_Phase}.");
        }
      }
    }

    // The consumer's verdict on the visit just witnessed, plus the routing that verdict
    // settles: an answered discovery whose node has no discoveries left is ready (a live one)
    // or dead (a severed-everywhere one, cascading silently).
    private void ApplyStrategiesToCurrentVisit(DagTraversalStrategies strategies)
    {
      if ((strategies & ~(DagTraversalStrategies.SkipEdge | DagTraversalStrategies.SkipOutEdges)) != 0)
        throw new ArgumentException($"Unknown strategy flags: {strategies}.", nameof(strategies));

      if (_Phase == WalkPhase.NotStarted || _Phase == WalkPhase.Done)
      {
        // The pre-enumeration sentinel (and the exhausted stream) accept only TraverseAll.
        if (strategies != DagTraversalStrategies.TraverseAll)
          throw new ArgumentException(
            $"{strategies} answers no visit -- the dagnumerator has not published one.", nameof(strategies));

        if (_Phase == WalkPhase.NotStarted)
          _Phase = WalkPhase.SourceDiscoveries;
        return;
      }

      if (Mode == DagnumeratorMode.DiscoveringNode)
      {
        if (strategies.HasFlag(DagTraversalStrategies.SkipOutEdges))
          throw new ArgumentException(
            "SkipOutEdges answers an entry; the current visit is a discovery.", nameof(strategies));

        var discoveredState = _States[_LastDiscovered];

        if (!strategies.HasFlag(DagTraversalStrategies.SkipEdge))
          discoveredState.Live++;

        if (discoveredState.Pending == 0)
          RouteSettled(_LastDiscovered, discoveredState);
      }
      else
      {
        if (strategies.HasFlag(DagTraversalStrategies.SkipEdge))
          throw new ArgumentException(
            "SkipEdge answers a discovery; the current visit is an entry.", nameof(strategies));

        if (strategies.HasFlag(DagTraversalStrategies.SkipOutEdges))
          _SuppressDispatch = true;
      }
    }

    // A node with no discoveries left goes to the ready batch if anything was delivered, and
    // dies otherwise -- its own out-edges settling silently, cascading (an explicit stack:
    // chains may be deep).
    private void RouteSettled(DagNode<TValue, TEdge> node, NodeState state)
    {
      if (state.Live > 0)
      {
        _ReadyBatch.Add(node);
        return;
      }

      var deadWalk = new Stack<DagNode<TValue, TEdge>>();
      deadWalk.Push(node);
      _SettledCount++;

      while (deadWalk.Count > 0)
      {
        var dead = deadWalk.Pop();

        foreach (var childEdge in dead.ChildEdges)
        {
          var childState = _States[childEdge.Child];
          childState.Pending--;

          if (childState.Pending != 0)
            continue;

          if (childState.Live > 0)
          {
            _ReadyBatch.Add(childEdge.Child);
          }
          else
          {
            deadWalk.Push(childEdge.Child);
            _SettledCount++;
          }
        }
      }
    }

    private void SettleSilently(DagNode<TValue, TEdge> child)
    {
      var childState = _States[child];
      childState.Pending--;

      if (childState.Pending == 0)
        RouteSettled(child, childState);
    }

    // Each phase's newly ready nodes push in REVERSE, so the first-dispatched ready child
    // pops first -- the depth-biased discipline matching the eager walk's discovery-order
    // bias.
    private void FlushReadyBatch()
    {
      for (var batchIndex = _ReadyBatch.Count - 1; batchIndex >= 0; batchIndex--)
        _Ready.Push(_ReadyBatch[batchIndex]);

      _ReadyBatch.Clear();
    }

    private void PublishDiscovery(DagNode<TValue, TEdge> node, NodeState state, int parentOrdinal, int edgeIndex, TEdge edge)
    {
      if (state.Ordinal < 0)
        state.Ordinal = _NextOrdinal++;

      _LastDiscovered = node;
      Mode = DagnumeratorMode.DiscoveringNode;
      Node = node.Value;
      Ordinal = state.Ordinal;
      Edge = edge;
      ParentOrdinal = parentOrdinal;
      EdgeIndex = edgeIndex;
    }

    private void PublishEntry(DagNode<TValue, TEdge> node, NodeState state)
    {
      Mode = DagnumeratorMode.EnteringNode;
      Node = node.Value;
      Ordinal = state.Ordinal;
      Edge = default;
      ParentOrdinal = -1;
      EdgeIndex = 0;
    }

    private string DescribeStarvation()
    {
      // At exhaustion every unsettled member still waits on an undelivered in-edge.
      var starved = _States
        .Where(memberState => memberState.Value.Pending > 0)
        .Select(memberState => memberState.Key.Value?.ToString() ?? "<null>")
        .Take(8)
        .ToList();

      var starvedCount = _States.Count - _SettledCount;

      return $"Cycle detected: {starvedCount} node(s) starved -- every remaining node waits on an undelivered in-edge"
        + (starved.Count > 0 ? $" (including: {string.Join(", ", starved)})" : "")
        + ".";
    }

    public void Dispose()
    {
    }

    private sealed class NodeState
    {
      public int Pending;
      public int Live;
      public int Ordinal = -1;
    }

    private enum WalkPhase
    {
      NotStarted,
      SourceDiscoveries,
      Entering,
      Dispatching,
      Done,
    }
  }
}
