namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The dag of downstream cones: every node relabeled with the cone below it (the node as
    /// sole source, everything it reaches, sharing kept, upward sight severed at the cone's
    /// boundary), shape untouched -- the comonad's <c>duplicate</c> in its cofree presentation
    /// (the tree family's <c>Subtrees</c>, dualized). O(1) per label, lazy per pull; each label
    /// pays its membership sweep at its first parent probe, never before.
    /// </summary>
    public static IWalkableDagnumerable<IWalkableDagnumerable<TValue, THandle, TEdge>, THandle, TEdge> Downstreams<TValue, THandle, TEdge>(
      this IWalkableDagnumerable<TValue, THandle, TEdge> source)
      => source.Extend<TValue, THandle, TEdge, IWalkableDagnumerable<TValue, THandle, TEdge>>(
        (topology, handle) => new DagDownstreamWalkable<TValue, THandle, TEdge>(topology, handle));
  }
}
