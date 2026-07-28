using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  // The grouping layer: the arrival protocol synthesized over ANY visit-protocol source --
  // the bijection's cheap direction (grouped -> flat is a free explode; this is the one place
  // the expensive direction runs, ONCE, instead of inside every operator). Mechanics ride the
  // visit protocol's own guarantees: dispatch contiguity makes a node's departure group the
  // discovery block after its entry (read with one-entry lookahead), and topological order
  // makes its arrival group complete before its entry (buffered per pending ordinal --
  // O(frontier) state, plus the source set). The inner walk is answered TraverseAll
  // throughout; sever/suppress liveness is this layer's own fold, layered on top: a dead
  // candidate's event never fires and its block never commits, which cascades. Perf posture
  // is the reference walk's (per-event allocation; the buffer-reuse discipline is the
  // migration's business, recorded in the design doc).
  internal sealed class ArrivalDagnumerator<TNode, TEdge> : IArrivalDagnumerator<TNode, TEdge>
  {
    public ArrivalDagnumerator(IDagnumerator<TNode, TEdge> inner)
    {
      _Inner = inner;
    }

    private readonly IDagnumerator<TNode, TEdge> _Inner;
    private readonly Dictionary<int, List<DagArrival<TNode, TEdge>>> _PendingArrivals = new();
    private readonly HashSet<int> _SourceOrdinals = new();

    private bool _Started;
    private bool _NextEntryPending;
    private int _NextEntryOrdinal;
    private TNode _NextEntryValue;

    private bool[] _SeveredArrivals;
    private bool[] _SuppressedDepartures;

    public DagNodeEvent<TNode, TEdge> Current { get; private set; }

    public bool MoveNext()
    {
      CommitCurrentVerdicts();

      while (true)
      {
        if (!TryReadNextCandidate(out var ordinal, out var value, out var departures))
          return false;

        if (_PendingArrivals.TryGetValue(ordinal, out var arrivals))
          _PendingArrivals.Remove(ordinal); // The frontier shrinks as events fire.

        var isSource = _SourceOrdinals.Contains(ordinal);

        if (!isSource && (arrivals == null || arrivals.Count == 0))
          continue; // Dead: no event, and its uncommitted departures die with it -- the fold.

        IReadOnlyList<DagArrival<TNode, TEdge>> arrivalGroup =
          arrivals ?? (IReadOnlyList<DagArrival<TNode, TEdge>>)Array.Empty<DagArrival<TNode, TEdge>>();

        Current = new DagNodeEvent<TNode, TEdge>(ordinal, value, arrivalGroup, departures, isSource);
        _SeveredArrivals = new bool[arrivalGroup.Count];
        _SuppressedDepartures = new bool[departures.Count];
        return true;
      }
    }

    public void SeverArrival(int arrivalIndex)
    {
      if (Current == null)
        throw new InvalidOperationException("No event is under dialogue -- verdicts answer the current event.");

      if (arrivalIndex < 0 || arrivalIndex >= Current.Arrivals.Count)
        throw new ArgumentOutOfRangeException(nameof(arrivalIndex));

      _SeveredArrivals[arrivalIndex] = true;
    }

    public void SuppressDeparture(int departureIndex)
    {
      if (Current == null)
        throw new InvalidOperationException("No event is under dialogue -- verdicts answer the current event.");

      if (departureIndex < 0 || departureIndex >= Current.Departures.Count)
        throw new ArgumentOutOfRangeException(nameof(departureIndex));

      _SuppressedDepartures[departureIndex] = true;
    }

    // Verdicts commit exactly once, at the advance: unsuppressed departures become their
    // targets' arrivals -- unless every arrival was severed, which voids the departures
    // wholesale (the event itself stood; verdicts shape only the future).
    private void CommitCurrentVerdicts()
    {
      if (Current == null)
        return;

      var allArrivalsSevered = Current.Arrivals.Count > 0;
      for (var arrivalIndex = 0; arrivalIndex < Current.Arrivals.Count; arrivalIndex++)
        allArrivalsSevered &= _SeveredArrivals[arrivalIndex];

      if (!allArrivalsSevered)
      {
        for (var departureIndex = 0; departureIndex < Current.Departures.Count; departureIndex++)
        {
          if (_SuppressedDepartures[departureIndex])
            continue;

          var departure = Current.Departures[departureIndex];

          if (!_PendingArrivals.TryGetValue(departure.TargetOrdinal, out var targetArrivals))
            _PendingArrivals[departure.TargetOrdinal] = targetArrivals = new List<DagArrival<TNode, TEdge>>();

          targetArrivals.Add(new DagArrival<TNode, TEdge>(Current.Ordinal, Current.Value, departure.Edge));
        }
      }

      Current = null;
      _SeveredArrivals = null;
      _SuppressedDepartures = null;
    }

    private bool TryReadNextCandidate(out int ordinal, out TNode value, out List<DagDeparture<TNode, TEdge>> departures)
    {
      if (!_Started)
      {
        _Started = true;
        PumpToNextEntry(departureBlock: null); // Only conventional source discoveries precede the first entry.
      }

      if (!_NextEntryPending)
      {
        ordinal = default;
        value = default;
        departures = null;
        return false;
      }

      ordinal = _NextEntryOrdinal;
      value = _NextEntryValue;
      _NextEntryPending = false;

      departures = new List<DagDeparture<TNode, TEdge>>();
      PumpToNextEntry(departures); // Dispatch contiguity: the block after the entry is the departure group.
      return true;
    }

    private void PumpToNextEntry(List<DagDeparture<TNode, TEdge>> departureBlock)
    {
      while (_Inner.MoveNext(DagTraversalStrategies.TraverseAll))
      {
        if (_Inner.Mode == DagnumeratorMode.DiscoveringNode)
        {
          if (_Inner.ParentOrdinal == -1)
            _SourceOrdinals.Add(_Inner.Ordinal);
          else
            departureBlock.Add(new DagDeparture<TNode, TEdge>(_Inner.Ordinal, _Inner.Node, _Inner.Edge));

          continue;
        }

        _NextEntryPending = true;
        _NextEntryOrdinal = _Inner.Ordinal;
        _NextEntryValue = _Inner.Node;
        return;
      }
    }

    public void Dispose() => _Inner.Dispose();
  }
}
