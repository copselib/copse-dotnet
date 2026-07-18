namespace Copse.Dags
{
  /// <summary>
  /// The composite DAG traversal contract (docs/DAG_CONTRACT_DESIGN.md): a source affording
  /// BOTH dimensions — forward-topological (information flowing down) and backward-topological
  /// (the transpose; information flowing up). A pure composite of the two single-dimension
  /// interfaces, mirroring ITreenumerable's shape over its depth-first/breadth-first halves.
  /// </summary>
  public interface IDagnumerable<TNode, TEdge> : IForwardDagnumerable<TNode, TEdge>, IBackwardDagnumerable<TNode, TEdge>
  {
  }
}
