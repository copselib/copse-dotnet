namespace Copse.Dags
{
  public static partial class DagWalker
  {
    /// <summary>
    /// The free lens: the same stance over the transposed topology -- the two edge groups trade
    /// places (<c>MoveToChild</c> now climbs, <c>MoveToParent</c> descends), payloads ride
    /// unchanged, the focus stays, and the unfocused stance stays unfocused: the virtual source
    /// of the transpose is the virtual SINK of the source. Involutive and O(1) -- two method
    /// references trading places, where the order algebra's <c>Transpose()</c> is a
    /// materialized reversal; the transpose's source group (the sinks) is one sweep, memoized.
    /// </summary>
    public static DagWalker<TValue, THandle, TEdge> Transpose<TValue, THandle, TEdge>(
      this DagWalker<TValue, THandle, TEdge> walker)
    {
      var transposed = DagTransposeTopology<TValue, THandle, TEdge>.Over(walker.Topology);

      return walker.HasFocus
        ? new DagWalker<TValue, THandle, TEdge>(transposed, walker.Focus)
        : new DagWalker<TValue, THandle, TEdge>(transposed);
    }
  }
}
