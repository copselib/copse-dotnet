using System;

namespace Copse.Dags
{
  /// <summary>
  /// The arrival protocol (docs/DAG_CONTRACT_DESIGN.md; the successor model, direction
  /// ratified 2026-07-28; vocabulary provisional): one <see cref="DagNodeEvent{TNode, TEdge}"/>
  /// per live node, in topological order -- the grouped presentation the visit protocol's
  /// every consumer regroups into anyway. The protocol stays a DIALOGUE: between an event and
  /// the next <see cref="MoveNext"/> the consumer may answer it with per-edge verdicts --
  /// <see cref="SeverArrival"/> and <see cref="SuppressDeparture"/> -- which commit when the
  /// walk advances. Verdicts shape only the future (an event cannot be retracted): a
  /// suppressed departure never becomes its target's arrival, severing ALL of a node's
  /// arrivals voids that node's departures wholesale, and the liveness fold does the rest --
  /// a non-source node left with no committed arrivals never events, and its own departures
  /// die with it.
  /// </summary>
  public interface IArrivalDagnumerator<TNode, TEdge> : IDisposable
  {
    /// <summary>Commits the current event's verdicts, then advances to the next live node's event.</summary>
    bool MoveNext();

    /// <summary>The event under dialogue; null before the first advance and after exhaustion.</summary>
    DagNodeEvent<TNode, TEdge> Current { get; }

    /// <summary>
    /// Verdict on <see cref="Current"/>: declares the arrival at <paramref name="arrivalIndex"/>
    /// void. If every arrival of a non-source event is severed, its departures are voided
    /// wholesale at commit. Throws when no event is under dialogue or the index is out of
    /// range (the strict ethos).
    /// </summary>
    void SeverArrival(int arrivalIndex);

    /// <summary>
    /// Verdict on <see cref="Current"/>: the departure at <paramref name="departureIndex"/>
    /// is never dispatched -- its target loses that arrival, and exclusively-reached targets
    /// die by the liveness fold. Throws when no event is under dialogue or the index is out
    /// of range.
    /// </summary>
    void SuppressDeparture(int departureIndex);
  }
}
