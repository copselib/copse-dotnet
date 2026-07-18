namespace Copse.Dags
{
  /// <summary>
  /// The forward half of the DAG dimension split (docs/DAG_CONTRACT_DESIGN.md): a source that
  /// affords the forward-topological walk — the dimension information flows DOWN (rootfix
  /// scans, prunes, contraction; inflows complete at entry). The dimension split makes "which
  /// directions can this source afford" a compile-time fact, exactly as the tree family's
  /// depth-first/breadth-first split does: a forward-only source (a serialized topological
  /// stream) implements just this interface, and asking it for the backward walk is a compile
  /// error rather than a hidden materialization.
  /// </summary>
  public interface IForwardDagnumerable<TNode, TEdge>
  {
    IDagnumerator<TNode, TEdge> GetForwardDagnumerator();
  }
}
