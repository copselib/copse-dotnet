using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  // The reference walk behind both dimensions (docs/DAG_CONTRACT_DESIGN.md): one class, handed
  // an acquisition-time topological snapshot and a direction, emits the Discover/Enter stream
  // with the liveness fold running as it goes. Direction is total role-reversal — backward walks
  // the reversed order reading parent edges as out-edges — and is resolved at construction into
  // snapshot-relative adjacency, so the protocol code is direction-blind. Snapshot-relative
  // matters: a member node may have a STRAY parent outside the dag (linked above a root, never
  // reachable from one); its edges are not the dag's and neither count toward pending nor
  // appear in the stream. Perf posture is the builder family's (correctness first).
  //
  // Liveness, stated once: a node enters iff at least one of its discoveries was emitted and the
  // consumer did not sever it. A dead or dispatch-suppressed node still DECREMENTS its targets'
  // pending counts (silently — the stream shows only the live structure), which is what lets a
  // shared target with another live in-edge proceed while an exclusively-reached one vanishes.
  internal sealed class TopologicalDagnumerator<TValue, TEdge> : IDagnumerator<TValue, TEdge>
  {
    public TopologicalDagnumerator(IReadOnlyList<DagNode<TValue, TEdge>> topologicalOrder, bool forward)
    {
      _TopologicalOrder = topologicalOrder;

      var ordinals = new Dictionary<DagNode<TValue, TEdge>, int>(topologicalOrder.Count);
      for (var ordinal = 0; ordinal < topologicalOrder.Count; ordinal++)
        ordinals[topologicalOrder[ordinal]] = ordinal;

      // Resolve direction into snapshot-relative out-edge lists, and pending counts from the
      // in-degrees those lists imply. Sources (walked in-degree zero) owe one conventional
      // discovery instead.
      _OutEdges = new (int TargetOrdinal, TEdge Edge)[topologicalOrder.Count][];
      _PendingDiscoveries = new int[topologicalOrder.Count];
      _LiveDiscoveries = new int[topologicalOrder.Count];

      for (var ordinal = 0; ordinal < topologicalOrder.Count; ordinal++)
      {
        var node = topologicalOrder[ordinal];
        var outEdges = new List<(int TargetOrdinal, TEdge Edge)>();

        if (forward)
        {
          foreach (var childEdge in node.ChildEdges)
            outEdges.Add((ordinals[childEdge.Child], childEdge.Value));
        }
        else
        {
          foreach (var parentEdge in node.ParentEdges)
            if (ordinals.TryGetValue(parentEdge.Parent, out var parentOrdinal))
              outEdges.Add((parentOrdinal, parentEdge.Value));
        }

        _OutEdges[ordinal] = outEdges.ToArray();
        foreach (var outEdge in _OutEdges[ordinal])
          _PendingDiscoveries[outEdge.TargetOrdinal]++;
      }

      for (var ordinal = 0; ordinal < topologicalOrder.Count; ordinal++)
      {
        if (_PendingDiscoveries[ordinal] == 0)
        {
          _PendingDiscoveries[ordinal] = 1;
          _Sources.Add(ordinal);
        }
      }

      // The pre-enumeration convention (the ForestRoot analog).
      Mode = DagnumeratorMode.DiscoveringNode;
      Ordinal = -1;
      ParentOrdinal = -1;
      EdgeIndex = 0;
    }

    private readonly IReadOnlyList<DagNode<TValue, TEdge>> _TopologicalOrder;
    private readonly (int TargetOrdinal, TEdge Edge)[][] _OutEdges;
    private readonly int[] _PendingDiscoveries;
    private readonly int[] _LiveDiscoveries;
    private readonly List<int> _Sources = new();

    private WalkPhase _Phase = WalkPhase.NotStarted;
    private int _SourceIndex;
    private int _TopoIndex;
    private int _OutEdgeIndex;
    private bool _SuppressDispatch;

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
          case WalkPhase.NotStarted:
            _Phase = WalkPhase.SourceDiscoveries;
            continue;

          case WalkPhase.SourceDiscoveries:
            if (_SourceIndex < _Sources.Count)
            {
              var sourceOrdinal = _Sources[_SourceIndex];
              _PendingDiscoveries[sourceOrdinal]--;
              PublishDiscovery(sourceOrdinal, parentOrdinal: -1, edgeIndex: _SourceIndex, edge: default);
              _SourceIndex++;
              return true;
            }

            _Phase = WalkPhase.Entering;
            _TopoIndex = 0;
            continue;

          case WalkPhase.Entering:
            if (_TopoIndex >= _TopologicalOrder.Count)
            {
              _Phase = WalkPhase.Done;
              continue;
            }

            // All in-edge sources sit earlier in topological order and have fully dispatched
            // (or silently decremented), so the count is settled when the slot is reached.
            if (_PendingDiscoveries[_TopoIndex] != 0)
              throw new InvalidOperationException(
                $"Pending discoveries not settled at entry (ordinal {_TopoIndex}) -- the topological snapshot is inconsistent.");

            if (_LiveDiscoveries[_TopoIndex] == 0)
            {
              // Dead node: no entry, no dispatch -- but its targets' pending counts still
              // settle, silently. The stream shows only the live structure.
              DecrementTargetsSilently(_TopoIndex, fromOutEdgeIndex: 0);
              _TopoIndex++;
              continue;
            }

            _SuppressDispatch = false;
            PublishEntry(_TopoIndex);
            _Phase = WalkPhase.Dispatching;
            _OutEdgeIndex = 0;
            return true;

          case WalkPhase.Dispatching:
            if (_SuppressDispatch)
            {
              DecrementTargetsSilently(_TopoIndex, _OutEdgeIndex);
              _OutEdgeIndex = _OutEdges[_TopoIndex].Length;
            }

            if (_OutEdgeIndex < _OutEdges[_TopoIndex].Length)
            {
              var (targetOrdinal, edge) = _OutEdges[_TopoIndex][_OutEdgeIndex];
              _PendingDiscoveries[targetOrdinal]--;
              PublishDiscovery(targetOrdinal, parentOrdinal: _TopoIndex, edgeIndex: _OutEdgeIndex, edge);
              _OutEdgeIndex++;
              return true;
            }

            _TopoIndex++;
            _Phase = WalkPhase.Entering;
            continue;

          case WalkPhase.Done:
            return false;

          default:
            throw new InvalidOperationException($"Unknown walk phase {_Phase}.");
        }
      }
    }

    // The consumer's verdict on the visit just witnessed. Verdicts only shape the future:
    // a discovery's liveness lands here (severed edges never count), and an entry's dispatch
    // suppression is recorded for the dispatch phase about to run.
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
        return;
      }

      if (Mode == DagnumeratorMode.DiscoveringNode)
      {
        if (strategies.HasFlag(DagTraversalStrategies.SkipOutEdges))
          throw new ArgumentException(
            "SkipOutEdges answers an entry; the current visit is a discovery.", nameof(strategies));

        if (!strategies.HasFlag(DagTraversalStrategies.SkipEdge))
          _LiveDiscoveries[Ordinal]++;
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

    private void PublishDiscovery(int ordinal, int parentOrdinal, int edgeIndex, TEdge edge)
    {
      Mode = DagnumeratorMode.DiscoveringNode;
      Node = _TopologicalOrder[ordinal].Value;
      Ordinal = ordinal;
      Edge = edge;
      ParentOrdinal = parentOrdinal;
      EdgeIndex = edgeIndex;
    }

    private void PublishEntry(int ordinal)
    {
      Mode = DagnumeratorMode.EnteringNode;
      Node = _TopologicalOrder[ordinal].Value;
      Ordinal = ordinal;
      Edge = default;
      ParentOrdinal = -1;
      EdgeIndex = 0;
    }

    private void DecrementTargetsSilently(int ordinal, int fromOutEdgeIndex)
    {
      for (var outEdgeIndex = fromOutEdgeIndex; outEdgeIndex < _OutEdges[ordinal].Length; outEdgeIndex++)
        _PendingDiscoveries[_OutEdges[ordinal][outEdgeIndex].TargetOrdinal]--;
    }

    public void Dispose()
    {
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
