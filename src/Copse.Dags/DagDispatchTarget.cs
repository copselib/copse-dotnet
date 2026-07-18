using System;

namespace Copse.Dags
{
  /// <summary>
  /// One live edge presented to a dispatch survey -- an out-edge downward
  /// (<see cref="Dagnumerable.RootfixDispatch"/>), an in-edge upward
  /// (<see cref="Dagnumerable.LeaffixDispatch"/>): the far node's source value, the edge
  /// payload, and the exactly-once <see cref="Dispatch"/> slot
  /// the survey must write -- unwritten and double-written slots both throw (the strict ethos:
  /// a dispatch that silently drops or duplicates an outflow is a conservation bug, not a
  /// default). Only LIVE edges are presented: an edge severed upstream (a prune, a consumer
  /// skip) is not the survey's to fund.
  /// </summary>
  public sealed class DagDispatchTarget<TNode, TDispatch, TEdge>
  {
    internal DagDispatchTarget(TNode value, TEdge edge, int targetOrdinal)
    {
      Value = value;
      Edge = edge;
      TargetOrdinal = targetOrdinal;
    }

    public TNode Value { get; }
    public TEdge Edge { get; }

    internal int TargetOrdinal { get; }
    internal bool IsDispatched { get; private set; }
    internal TDispatch DispatchedValue { get; private set; }

    /// <summary>Writes this edge's outflow. Exactly once: a second write throws.</summary>
    public void Dispatch(TDispatch value)
    {
      if (IsDispatched)
        throw new InvalidOperationException($"The edge to '{Value}' was dispatched twice.");

      IsDispatched = true;
      DispatchedValue = value;
    }
  }
}
