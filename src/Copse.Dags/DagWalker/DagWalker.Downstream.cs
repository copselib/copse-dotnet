namespace Copse.Dags
{
  public static partial class DagWalker
  {
    /// <summary>
    /// The INCLUSIVE HOIST (the tree family's <c>Subtree()</c>, dualized): at a node, the
    /// downstream cone with the focus as sole source -- the value-bearing nodes at-or-below the
    /// focus, sharing kept, upward sight severed at the cone's boundary; at the UNFOCUSED stance,
    /// the whole dag (the identity view -- nothing above the virtual source to sever, and the
    /// valueless focus has no spelling in the dagnumerable, so it drops out by type). Door then
    /// hoist is the identity round trip, no case analysis. The reverse door: the cone's door
    /// lands on its own unfocused stance above the severed root, and the interior round trip
    /// forgets upward context -- severance is the cofree forgetting, deliberately asymmetric.
    /// </summary>
    public static IWalkableDagnumerable<TValue, THandle, TEdge> Downstream<TValue, THandle, TEdge>(
      this DagWalker<TValue, THandle, TEdge> walker)
    {
      if (!walker.HasFocus)
        return new DagTopologyWalkable<TValue, THandle, TEdge>(walker.Topology);

      return new DagDownstreamWalkable<TValue, THandle, TEdge>(walker.Topology, walker.Focus);
    }
  }
}
