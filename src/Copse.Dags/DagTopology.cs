namespace Copse.Dags
{
  /// <summary>The topology creation surface (the tree family's <c>TreeTopology</c>, dualized).</summary>
  public static class DagTopology
  {
    /// <summary>
    /// A walkable's topology, call-by-need: the door is knocked once, at the first probe, and
    /// every answer after flows through the topology it bound. The deferral seam every lens
    /// builds on -- a view whose constructor must not force its source holds this.
    /// </summary>
    public static IDagTopology<TValue, THandle, TEdge> Lazy<TValue, THandle, TEdge>(
      IWalkableDagnumerable<TValue, THandle, TEdge> source)
      => new LazyDagTopology<TValue, THandle, TEdge>(source);
  }
}
