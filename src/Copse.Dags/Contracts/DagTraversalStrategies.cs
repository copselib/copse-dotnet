using System;

namespace Copse.Dags
{
  /// <summary>
  /// Consumer strategies for <see cref="IDagnumerator{TNode, TEdge}.MoveNext"/> — the DAG analog
  /// of the tree family's NodeTraversalStrategies, reshaped by one fact: when a visit is
  /// published, everything about it has already been witnessed, so a consumer can only shape the
  /// FUTURE. Hence there is deliberately no consumer skip-node: an entry cannot be retracted,
  /// and removing a node from the logical dag is operator business (PruneBefore), not a consumer
  /// verdict. A node whose every potential discovery is severed or never emitted simply never
  /// enters — consumer skips compose with the traversal's liveness fold.
  ///
  /// Each strategy is meaningful in exactly one <see cref="DagnumeratorMode"/>; passing it in
  /// the other mode throws.
  /// </summary>
  [Flags]
  public enum DagTraversalStrategies
  {
    TraverseAll = 0,

    /// <summary>
    /// Discovery only: sever the just-discovered in-edge. If this was the node's last live
    /// in-edge, the node never enters (and whatever is reachable only through it never
    /// appears). Per-edge granularity the tree protocol cannot express.
    /// </summary>
    SkipEdge = 1,

    /// <summary>
    /// Entry only: keep the node, dispatch none of its out-edges. Downstream nodes stay live
    /// only if another live in-edge reaches them.
    /// </summary>
    SkipOutEdges = 2,
  }
}
