namespace Copse.Dags
{
  public static partial class DagWalker
  {
    /// <summary>
    /// The upstream cone -- everything that reaches the focus, the focus as sole SINK, in the
    /// original orientation: the transpose-conjugate of <see cref="Downstream"/>, built from
    /// the free lens and the cone with no new machinery (the transpose of the downstream cone
    /// of the transpose). At the unfocused stance, the whole dag. Content-equivalent to the
    /// streaming <c>TakeUpstreamWhere</c> at one node (pinned), at a neighborhood price.
    /// </summary>
    public static IWalkableDagnumerable<TValue, THandle, TEdge> Upstream<TValue, THandle, TEdge>(
      this DagWalker<TValue, THandle, TEdge> walker)
    {
      if (!walker.HasFocus)
        return new DagTopologyWalkable<TValue, THandle, TEdge>(walker.Topology);

      var transposedCone = new DagDownstreamWalkable<TValue, THandle, TEdge>(
        DagTransposeTopology<TValue, THandle, TEdge>.Over(walker.Topology),
        walker.Focus);

      return new DagTopologyWalkable<TValue, THandle, TEdge>(
        DagTransposeTopology<TValue, THandle, TEdge>.Over(transposedCone));
    }
  }
}
