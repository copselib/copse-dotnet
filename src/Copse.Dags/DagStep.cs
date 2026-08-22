namespace Copse.Dags
{
  /// <summary>
  /// One adjacency answer in a dag topology: the far node of an edge, the edge's payload, and
  /// the edge's index within the group it was asked from (an out-edge group, an in-edge group,
  /// or the virtual source's child group -- the sources). The dag walker's steps are EDGE-ATOMIC
  /// (WALKER_DESIGN.md: positions are nodes, steps are (edge, node) pairs), so every probe
  /// answers with the edge it crossed. <see cref="HasValue"/> false is the typed miss past the
  /// end of a group; <c>default</c> is that miss. Sources arrive on the virtual source's seed
  /// edge, whose payload is <c>default</c> -- the dispatcher-less arrival of the virtual source
  /// family, seen from the walker side.
  /// </summary>
  public readonly struct DagStep<THandle, TEdge>
  {
    public DagStep(THandle handle, TEdge edge, int edgeIndex)
    {
      HasValue = true;
      Handle = handle;
      Edge = edge;
      EdgeIndex = edgeIndex;
    }

    public readonly bool HasValue;
    public readonly THandle Handle;
    public readonly TEdge Edge;
    public readonly int EdgeIndex;

    public override string ToString()
      => HasValue ? $"{Edge} -> {Handle} [{EdgeIndex}]" : "none";
  }
}
