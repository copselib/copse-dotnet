using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// LOAD-BEARING SUGAR over <see cref="ReplaceNodes"/> naming the node-division pattern,
    /// the node-channel twin of <see cref="ExpandEdgesWhere"/>: matching nodes expand to the
    /// graph <paramref name="expansion"/> returns; everything else keeps its seat untouched.
    /// The <c>Where</c> suffix marks the predicate, per the grammar. The canonical workload
    /// is the cell-division move (docs/SUBSTITUTION_TAXONOMY.md): a node divides into
    /// alternatives or stretches into a chain, and every incident edge divides with it --
    /// each replacement node keeps its own edge to each neighbor, sharing making the
    /// operation linear where the tree unfolding would pay copies. All of the replacement's
    /// clauses apply: sources take the in-edges, every node takes the out-edges, fresh
    /// interior identity, liveness on <c>Drop</c>, capture-shaped by convention.
    /// </summary>
    public static DagBuffer<TNode, TEdge> ExpandNodesWhere<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, bool> predicate,
      Func<TNode, DagNodeGraph<TNode, TEdge>> expansion)
    {
      if (predicate == null)
        throw new ArgumentNullException(nameof(predicate));
      if (expansion == null)
        throw new ArgumentNullException(nameof(expansion));

      return ReplaceNodes(
        source,
        node => predicate(node)
          ? expansion(node)
          : DagNodeGraph<TNode, TEdge>.Keep(node));
    }
  }
}
