namespace Copse.Dags
{
  /// <summary>
  /// One published visit of the DAG protocol, bundled (the NodeVisit analog): what
  /// <see cref="Dagnumerable.Do"/> hands its action, and what tests and diagnostics record.
  /// Edge context (<see cref="Edge"/>, <see cref="ParentOrdinal"/>, <see cref="EdgeIndex"/>)
  /// is meaningful in <see cref="DagnumeratorMode.DiscoveringNode"/> only.
  /// </summary>
  public readonly struct DagVisit<TNode, TEdge>
  {
    public DagVisit(DagnumeratorMode mode, TNode node, int ordinal, TEdge edge, int parentOrdinal, int edgeIndex)
    {
      Mode = mode;
      Node = node;
      Ordinal = ordinal;
      Edge = edge;
      ParentOrdinal = parentOrdinal;
      EdgeIndex = edgeIndex;
    }

    public DagnumeratorMode Mode { get; }
    public TNode Node { get; }
    public int Ordinal { get; }
    public TEdge Edge { get; }
    public int ParentOrdinal { get; }
    public int EdgeIndex { get; }
  }
}
