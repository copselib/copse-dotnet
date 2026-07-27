namespace Copse.Dags
{
  /// <summary>
  /// One live edge with both endpoints in scope -- what <see cref="Dagnumerable.GetEdges"/>
  /// yields: the dispatching parent's value, the discovered child's value, the payload, and the
  /// edge's index among the child's live in-edges in arrival order. That index is the honest
  /// correlation key to per-edge pass results: a dispatch result's
  /// <see cref="DagDispatchNode{TNode, TDispatch, TEdge}.Inflows"/> arrive in the same order,
  /// so <c>edge.Child.Inflows[edge.InEdgeIndex]</c> is THIS edge's delivery -- no payload
  /// comparison (user values are never compared), no parallel-edge ambiguity.
  /// </summary>
  public readonly struct DagEdgeContext<TNode, TEdge>
  {
    public DagEdgeContext(TNode parent, TNode child, TEdge edge, int inEdgeIndex)
    {
      Parent = parent;
      Child = child;
      Edge = edge;
      InEdgeIndex = inEdgeIndex;
    }

    public TNode Parent { get; }
    public TNode Child { get; }
    public TEdge Edge { get; }

    /// <summary>This edge's index among the child's live in-edges, in arrival (discovery) order.</summary>
    public int InEdgeIndex { get; }
  }
}
