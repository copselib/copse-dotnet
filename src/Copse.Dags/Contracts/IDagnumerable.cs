namespace Copse.Dags
{
  /// <summary>
  /// THE DAG traversal contract (design-docs/DAG_CONTRACT_DESIGN.md; the trio collapsed to one by the
  /// re-founding): a source affording the forward-topological walk — the canonical
  /// linear presentation, the order a streaming pass wants, the order a flat encoding stores.
  /// There is no backward dimension: the backward walk is definitionally forward-of-the-
  /// transpose, which makes orientation-flipping an OPERATOR — <c>Transpose()</c>, free on
  /// <see cref="DagBuffer{TNode, TEdge}"/> (swap which adjacency you read) and an explicit
  /// capture (<c>Materialize().Transpose()</c>) from anything else. The affordance story is a
  /// store-capability fact, not a dimension: <c>Materialize</c> is the escalation, as ever.
  /// </summary>
  public interface IDagnumerable<TNode, TEdge>
  {
    IDagnumerator<TNode, TEdge> GetDagnumerator();
  }
}
