using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  // The buffer's walk (design-docs/DAG_CONTRACT_DESIGN.md): handed a captured dag as flat CSR
  // adjacency (sequential flat-array passes are the measured winner), emits the Discover/Enter
  // stream with the liveness fold running as it goes. The walk is orientation-blind — a
  // transpose walk is the same class over the transpose's adjacency; direction was resolved by
  // whoever built the arrays. Ordinals are the buffer's dense indices, though the CONTRACT
  // does not promise density (wrappers preserve their source's ordinals, so pruned streams
  // carry gaps).
  //
  // Liveness, stated once: a node enters iff at least one of its discoveries was emitted and the
  // consumer did not sever it. A dead or dispatch-suppressed node still DECREMENTS its targets'
  // pending counts (silently — the stream shows only the live structure), which is what lets a
  // shared target with another live in-edge proceed while an exclusively-reached one vanishes.
  internal sealed class TopologicalDagnumerator<TValue, TEdge> : IDagnumerator<TValue, TEdge>
  {
    public TopologicalDagnumerator(TValue[] values, int[] outEdgeOffsets, int[] outEdgeTargets, TEdge[] outEdgePayloads)
    {
      _Values = values;
      _OutEdgeOffsets = outEdgeOffsets;
      _OutEdgeTargets = outEdgeTargets;
      _OutEdgePayloads = outEdgePayloads;

      _PendingDiscoveries = new int[values.Length];
      _LiveDiscoveries = new int[values.Length];

      for (var edgeIndex = 0; edgeIndex < outEdgeTargets.Length; edgeIndex++)
        _PendingDiscoveries[outEdgeTargets[edgeIndex]]++;

      for (var ordinal = 0; ordinal < values.Length; ordinal++)
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

    private readonly TValue[] _Values;
    private readonly int[] _OutEdgeOffsets;
    private readonly int[] _OutEdgeTargets;
    private readonly TEdge[] _OutEdgePayloads;
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
            if (_TopoIndex >= _Values.Length)
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
              _OutEdgeIndex = OutDegree(_TopoIndex);
            }

            if (_OutEdgeIndex < OutDegree(_TopoIndex))
            {
              var edgeSlot = _OutEdgeOffsets[_TopoIndex] + _OutEdgeIndex;
              var targetOrdinal = _OutEdgeTargets[edgeSlot];
              _PendingDiscoveries[targetOrdinal]--;
              PublishDiscovery(targetOrdinal, parentOrdinal: _TopoIndex, edgeIndex: _OutEdgeIndex, _OutEdgePayloads[edgeSlot]);
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

    private int OutDegree(int ordinal) => _OutEdgeOffsets[ordinal + 1] - _OutEdgeOffsets[ordinal];

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
      Node = _Values[ordinal];
      Ordinal = ordinal;
      Edge = edge;
      ParentOrdinal = parentOrdinal;
      EdgeIndex = edgeIndex;
    }

    private void PublishEntry(int ordinal)
    {
      Mode = DagnumeratorMode.EnteringNode;
      Node = _Values[ordinal];
      Ordinal = ordinal;
      Edge = default;
      ParentOrdinal = -1;
      EdgeIndex = 0;
    }

    private void DecrementTargetsSilently(int ordinal, int fromOutEdgeIndex)
    {
      for (var outEdgeIndex = fromOutEdgeIndex; outEdgeIndex < OutDegree(ordinal); outEdgeIndex++)
        _PendingDiscoveries[_OutEdgeTargets[_OutEdgeOffsets[ordinal] + outEdgeIndex]]--;
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
