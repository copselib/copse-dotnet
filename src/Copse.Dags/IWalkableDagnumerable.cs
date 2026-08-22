namespace Copse.Dags
{
  /// <summary>
  /// A dag that affords a walker: the comonadic surface beside the monadic one. One member, and
  /// it cannot miss -- both factories are total (the tree family's door-only charter, dualized):
  /// <see cref="IDagnumerable{TNode, TEdge}"/> is a dagnumerator factory, this is a DAG WALKER
  /// factory. The door hands over the UNFOCUSED walker -- standing on the virtual source, above
  /// every source, the sources its child group -- bound at birth to the best topology the source
  /// affords (the buffer's CSR, the builder's node adjacency, a lens's rewritten answers). The
  /// walkable then leaves the story: it appears in no navigation call path. Captures are never
  /// address-poor: <see cref="DagBuffer{TNode, TEdge}"/> is the family's walkable buffer, with
  /// dense ordinals as handles.
  /// </summary>
  public interface IWalkableDagnumerable<TValue, THandle, TEdge> : IDagnumerable<TValue, TEdge>
  {
    DagWalker<TValue, THandle, TEdge> GetDagWalker();
  }
}
