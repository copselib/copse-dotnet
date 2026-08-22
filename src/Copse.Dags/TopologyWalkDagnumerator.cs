using System;
using System.Collections.Generic;
using System.Text;

namespace Copse.Dags
{
  // The family's one demand-driven walk (Dag.FromTopology's engine, and the builder's through
  // DagNodeTopology -- THE LAZY BUILDER RULING, design-docs/DAG_CONTRACT_DESIGN.md): Kahn's
  // algorithm on demand over a topology's probes, membership keyed by handle equality. The visit
  // protocol is Kahn's trace: pop a ready node = entry; its out-edge group = the dispatch block
  // (discoveries, each decrementing a child's pending count); a child settling becomes ready.
  // Acquisition runs one counting sweep (membership = the closure of the source group over
  // out-edges, plus member in-degree counted from the out-edges actually probed -- the
  // topology's in-edge group is never consulted, so a provider whose in-groups disagree with
  // its out-groups streams by its out-groups, and the conformance battery is where that lie
  // surfaces). A cyclic topology publishes its maximal acyclic prefix and throws
  // DagCycleException at starvation, naming one loop (starvation is the failure, exhaustion is
  // the proof; Materialize is the validator). Ordinals are assigned at first discovery, dense
  // in discovery order -- a lazy walk cannot cite a node's future entry index, so the contract
  // promises a stable per-enumeration correlation key, entries in topological order.
  //
  // Liveness, stated once: a node enters iff at least one of its discoveries was emitted and
  // not severed; a dead or dispatch-suppressed node still settles its targets' pending counts
  // silently, cascading -- the stream shows only the live structure. Entry discipline is
  // depth-biased to match the buffer walk's discovery-order bias (TopologicalDagnumerator):
  // each phase's newly ready nodes push onto the stack in reverse, so the first-dispatched
  // ready child is entered first and chains run deep before siblings.
  internal sealed class TopologyWalkDagnumerator<TValue, THandle, TEdge> : IDagnumerator<TValue, TEdge>
  {
    public TopologyWalkDagnumerator(IDagTopology<TValue, THandle, TEdge> topology)
      : this(topology, SourceGroupOf(topology))
    {
    }

    // Membership SEEDS apart from the source group: the closure walked is the seeds' closure, and
    // the walk's sources are the seeds nothing in that closure points to. For a topology the
    // seeds ARE its source group; the builder hands over its LISTED sources instead, so a listed
    // node another listed node reaches is a member (not a source), and a graph whose every listed
    // node sits on a cycle has an empty acyclic prefix and STARVES -- it does not stream empty.
    // Materialize stays the validator.
    internal TopologyWalkDagnumerator(IDagTopology<TValue, THandle, TEdge> topology, IReadOnlyList<THandle> membershipSeeds)
    {
      _Topology = topology;

      var membershipWalk = new Stack<THandle>();

      foreach (var seed in membershipSeeds)
      {
        if (_States.ContainsKey(seed))
          continue;

        _States.Add(seed, new NodeState());
        _WalkSources.Add(seed);
        membershipWalk.Push(seed);
      }

      while (membershipWalk.Count > 0)
      {
        var member = membershipWalk.Pop();

        for (var childStep = topology.TryGetChildAt(member, 0); childStep.HasValue; childStep = topology.TryGetChildAt(member, childStep.EdgeIndex + 1))
        {
          if (!_States.TryGetValue(childStep.Handle, out var childState))
          {
            childState = new NodeState();
            _States.Add(childStep.Handle, childState);
            membershipWalk.Push(childStep.Handle);
          }

          childState.Pending++;
        }
      }

      // A source another source reaches is a member, not a walk source; each walk source
      // carries one pending conventional discovery (the sources-at-the-start convention).
      for (var index = _WalkSources.Count - 1; index >= 0; index--)
      {
        var state = _States[_WalkSources[index]];

        if (state.Pending == 0)
          state.Pending = 1;
        else
          _WalkSources.RemoveAt(index);
      }

      Mode = DagnumeratorMode.DiscoveringNode;
      Ordinal = -1;
      ParentOrdinal = -1;
      EdgeIndex = 0;
    }

    private static List<THandle> SourceGroupOf(IDagTopology<TValue, THandle, TEdge> topology)
    {
      var sources = new List<THandle>();

      for (var sourceStep = topology.TryGetSourceAt(0); sourceStep.HasValue; sourceStep = topology.TryGetSourceAt(sourceStep.EdgeIndex + 1))
        sources.Add(sourceStep.Handle);

      return sources;
    }

    private readonly IDagTopology<TValue, THandle, TEdge> _Topology;
    private readonly Dictionary<THandle, NodeState> _States = new();
    private readonly List<THandle> _WalkSources = new();
    private readonly Stack<THandle> _Ready = new();
    private readonly List<THandle> _ReadyBatch = new();

    private WalkPhase _Phase = WalkPhase.NotStarted;
    private int _SourceIndex;
    private int _NextOrdinal;
    private int _SettledCount;
    private THandle _Current;
    private NodeState _CurrentState;
    private int _OutEdgeIndex;
    private bool _SuppressDispatch;
    private THandle _LastDiscovered;

    // The handle behind the current visit -- the family's own seam (the protocol publishes
    // values; a topology walk knows which handle it stands on, and the builder's owned-node
    // views read it here instead of re-deriving it from the stream).
    internal THandle CurrentHandle { get; private set; }

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
              for (var childStep = _Topology.TryGetChildAt(_Current, _OutEdgeIndex); childStep.HasValue; childStep = _Topology.TryGetChildAt(_Current, ++_OutEdgeIndex))
                SettleSilently(childStep.Handle);
            }

            var nextChild = _Topology.TryGetChildAt(_Current, _OutEdgeIndex);

            if (nextChild.HasValue)
            {
              var childState = _States[nextChild.Handle];
              childState.Pending--;
              PublishDiscovery(nextChild.Handle, childState, _CurrentState.Ordinal, _OutEdgeIndex, nextChild.Edge);
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

    private void ApplyStrategiesToCurrentVisit(DagTraversalStrategies strategies)
    {
      if ((strategies & ~(DagTraversalStrategies.SkipEdge | DagTraversalStrategies.SkipOutEdges)) != 0)
        throw new ArgumentException($"Unknown strategy flags: {strategies}.", nameof(strategies));

      if (_Phase == WalkPhase.NotStarted || _Phase == WalkPhase.Done)
      {
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

    private void RouteSettled(THandle handle, NodeState state)
    {
      if (state.Live > 0)
      {
        _ReadyBatch.Add(handle);
        return;
      }

      var deadWalk = new Stack<THandle>();
      deadWalk.Push(handle);
      _SettledCount++;

      while (deadWalk.Count > 0)
      {
        var dead = deadWalk.Pop();

        for (var childStep = _Topology.TryGetChildAt(dead, 0); childStep.HasValue; childStep = _Topology.TryGetChildAt(dead, childStep.EdgeIndex + 1))
        {
          var childState = _States[childStep.Handle];
          childState.Pending--;

          if (childState.Pending != 0)
            continue;

          if (childState.Live > 0)
          {
            _ReadyBatch.Add(childStep.Handle);
          }
          else
          {
            deadWalk.Push(childStep.Handle);
            _SettledCount++;
          }
        }
      }
    }

    private void SettleSilently(THandle child)
    {
      var childState = _States[child];
      childState.Pending--;

      if (childState.Pending == 0)
        RouteSettled(child, childState);
    }

    private void FlushReadyBatch()
    {
      for (var batchIndex = _ReadyBatch.Count - 1; batchIndex >= 0; batchIndex--)
        _Ready.Push(_ReadyBatch[batchIndex]);

      _ReadyBatch.Clear();
    }

    private void PublishDiscovery(THandle handle, NodeState state, int parentOrdinal, int edgeIndex, TEdge edge)
    {
      if (state.Ordinal < 0)
        state.Ordinal = _NextOrdinal++;

      _LastDiscovered = handle;
      Mode = DagnumeratorMode.DiscoveringNode;
      CurrentHandle = handle;
      Node = _Topology.GetValue(handle);
      Ordinal = state.Ordinal;
      Edge = edge;
      ParentOrdinal = parentOrdinal;
      EdgeIndex = edgeIndex;
    }

    private void PublishEntry(THandle handle, NodeState state)
    {
      Mode = DagnumeratorMode.EnteringNode;
      CurrentHandle = handle;
      Node = _Topology.GetValue(handle);
      Ordinal = state.Ordinal;
      Edge = default;
      ParentOrdinal = -1;
      EdgeIndex = 0;
    }

    // Starvation names the loop: every unsettled member
    // waits on an undelivered in-edge whose parent is itself unsettled, so a DFS restricted to
    // the starved members always closes a loop.
    private string DescribeStarvation()
    {
      var starvedCount = _States.Count - _SettledCount;
      var context = $"({starvedCount} node(s) starved -- every remaining node waits on an undelivered in-edge).";
      var cyclePath = DescribeOneStarvedCycle();

      return cyclePath == null
        ? $"Cycle detected: the walk starved with no loop found among the unsettled members {context}"
        : $"{cyclePath} {context}";
    }

    private string DescribeOneStarvedCycle()
    {
      var starved = new HashSet<THandle>();

      foreach (var memberState in _States)
        if (memberState.Value.Pending > 0)
          starved.Add(memberState.Key);

      var visitedNodes = new HashSet<THandle>();
      var nodesOnPath = new HashSet<THandle>();
      var pathFrames = new List<StarvedPathFrame>();

      foreach (var memberState in _States)
      {
        var root = memberState.Key;

        if (!starved.Contains(root) || !visitedNodes.Add(root))
          continue;

        nodesOnPath.Add(root);
        pathFrames.Add(new StarvedPathFrame(root));

        while (pathFrames.Count > 0)
        {
          var frameIndex = pathFrames.Count - 1;
          var frame = pathFrames[frameIndex];
          var childStep = _Topology.TryGetChildAt(frame.Handle, frame.NextChildIndex);

          if (!childStep.HasValue)
          {
            pathFrames.RemoveAt(frameIndex);
            nodesOnPath.Remove(frame.Handle);
            continue;
          }

          frame.NextChildIndex++;
          pathFrames[frameIndex] = frame;

          if (!starved.Contains(childStep.Handle))
            continue;

          if (nodesOnPath.Contains(childStep.Handle))
            return DescribeCycleFromPath(pathFrames, childStep.Handle);

          if (visitedNodes.Add(childStep.Handle))
          {
            nodesOnPath.Add(childStep.Handle);
            pathFrames.Add(new StarvedPathFrame(childStep.Handle));
          }
        }
      }

      return null;
    }

    private string DescribeCycleFromPath(List<StarvedPathFrame> pathFrames, THandle reencountered)
    {
      var cycleStartIndex = pathFrames.FindIndex(frame => EqualityComparer<THandle>.Default.Equals(frame.Handle, reencountered));
      var cycleDescription = new StringBuilder("Cycle detected: ");

      for (var frameIndex = cycleStartIndex; frameIndex < pathFrames.Count; frameIndex++)
        cycleDescription.Append(_Topology.GetValue(pathFrames[frameIndex].Handle)?.ToString() ?? "<null>").Append(" -> ");

      return cycleDescription.Append(_Topology.GetValue(reencountered)?.ToString() ?? "<null>").ToString();
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

    private struct StarvedPathFrame
    {
      public StarvedPathFrame(THandle handle)
      {
        Handle = handle;
        NextChildIndex = 0;
      }

      public readonly THandle Handle;
      public int NextChildIndex;
    }
  }
}
