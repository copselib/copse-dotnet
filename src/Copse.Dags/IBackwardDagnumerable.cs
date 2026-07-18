namespace Copse.Dags
{
  /// <summary>
  /// The backward half of the DAG dimension split (docs/DAG_CONTRACT_DESIGN.md): the
  /// transpose's forward walk — the dimension information flows UP (the leaffix family;
  /// child results complete at entry). In the backward walk the roles reverse wholesale:
  /// sources are the dag's sinks, an in-edge is an out-edge of the transpose, and the same
  /// protocol (<see cref="IDagnumerator{TNode, TEdge}"/>) reads unchanged over it.
  /// </summary>
  public interface IBackwardDagnumerable<TNode, TEdge>
  {
    IDagnumerator<TNode, TEdge> GetBackwardDagnumerator();
  }
}
