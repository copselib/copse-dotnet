using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// LOAD-BEARING SUGAR over <see cref="ReplaceEdges"/> naming the interposition
    /// pattern: matching edges expand to the path <paramref name="expansion"/> returns;
    /// everything else keeps its payload untouched. The <c>Where</c> suffix marks the
    /// predicate, per the grammar, and the sugar earns its seat the way `TakeTrees` does over
    /// `TakeNodesUntil` — the call site states the intent ("expand these edges") instead of
    /// restating the identity branch at every use. The canonical workload is
    /// reify-the-missing-entity: attribution units living as edge decorations (a program id
    /// on an ownership stake) become fresh interior nodes —
    /// <c>ExpandEdgesWhere(e =&gt; HasProgram(e), e =&gt; DagEdgePath.Through(e.Edge,
    /// ProgramNode(e), passThrough))</c> — after which path-dependent queries collapse to
    /// per-node ones. All of the bind's clauses apply: fresh interior nodes, cycle-safe by
    /// construction, born-here ordinals (−1), capture-shaped by convention.
    /// </summary>
    public static DagBuffer<TNode, TEdge> ExpandEdgesWhere<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<DagEdgeContext<TNode, TEdge>, bool> predicate,
      Func<DagEdgeContext<TNode, TEdge>, DagEdgePath<TNode, TEdge>> expansion)
    {
      if (predicate == null)
        throw new ArgumentNullException(nameof(predicate));
      if (expansion == null)
        throw new ArgumentNullException(nameof(expansion));

      return ReplaceEdges(
        source,
        edgeContext => predicate(edgeContext)
          ? expansion(edgeContext)
          : DagEdgePath<TNode, TEdge>.Keep(edgeContext.Edge));
    }
  }
}
