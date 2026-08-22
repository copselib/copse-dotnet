namespace Copse.Dags
{
  /// <summary>
  /// One DEPARTURE -- an arrival seen from the dispatching end (design-docs/DAG_CONTRACT_DESIGN.md,
  /// the arrival protocol; vocabulary provisional): the out-edge about to be dispatched,
  /// carrying the target far endpoint (value and ordinal) and the edge payload, in out-edge
  /// order. A departure is a PROPOSAL until the event's verdicts commit at the next advance --
  /// suppressed departures never become their targets' arrivals.
  /// </summary>
  public readonly struct DagDeparture<TNode, TEdge>
  {
    internal DagDeparture(int targetOrdinal, TNode target, TEdge edge)
    {
      TargetOrdinal = targetOrdinal;
      Target = target;
      Edge = edge;
    }

    /// <summary>The target far endpoint's ordinal -- the correlation key to its future event.</summary>
    public int TargetOrdinal { get; }

    /// <summary>The target far endpoint's value.</summary>
    public TNode Target { get; }

    /// <summary>The edge payload the departure will carry.</summary>
    public TEdge Edge { get; }
  }
}
