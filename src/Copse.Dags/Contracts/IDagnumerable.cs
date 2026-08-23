namespace Copse.Dags
{
  /// <summary>
  /// THE DAG traversal contract (design-docs/DAG_CONTRACT_DESIGN.md): a source affording the
  /// forward-topological walk — the canonical linear presentation, the order a streaming pass
  /// wants, the order a flat encoding stores. The backward walk is forward-of-the-transpose,
  /// which makes orientation-flipping an OPERATOR — <c>Transpose()</c>, free on
  /// <see cref="DagBuffer{TNode, TEdge}"/> (swap which adjacency you read) and an explicit
  /// capture (<c>Materialize().Transpose()</c>) from anything else. <c>Materialize</c> is the
  /// escalation from any source to the capture.
  /// </summary>
  public interface IDagnumerable<TNode, TEdge>
  {
    IDagnumerator<TNode, TEdge> GetDagnumerator();
  }
}
