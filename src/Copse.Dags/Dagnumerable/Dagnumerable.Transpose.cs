namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The orientation flip -- the operator the retired backward dimension became
    /// (the 2026-08-02 re-founding): the same nodes and edges with every arrow reversed,
    /// presented in the transpose's own topological order. <c>Materialize().Transpose()</c>,
    /// spelled once: MATERIALIZES by definition when the source is not already a buffer --
    /// the return type declares it (the laziness policy's documented-when-not clause), because
    /// a transpose is a whole-graph fact, so a lazy transposed stream cannot honestly exist.
    /// On a <see cref="DagBuffer{TNode, TEdge}"/> the flip is free in the amortized sense: the
    /// transpose adjacency is built lazily once and back-linked, so transposing back costs an
    /// O(n) value reversal and no adjacency work at all. The whole forward operator family
    /// points upward through this -- prune ancestors, scan the transpose, seed an upward
    /// dispatch (<c>Transpose().SourcefixDispatch(seed, survey)</c> is the seeded upward pass
    /// the deferred SinkfixDispatch seed flavor would name).
    /// </summary>
    public static DagBuffer<TNode, TEdge> Transpose<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source)
      => DagBuffer<TNode, TEdge>.From(source).Transpose();
  }
}
