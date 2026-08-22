namespace Copse.Dags
{
  /// <summary>
  /// The subdivision's node: a NODE of the original dag, or one of its EDGES reified as a node
  /// (<see cref="Dagnumerable.Subdivide"/>). The discriminated union the bijection promised --
  /// the flat (edge, node) model IS node-only traversal of the edge subdivision -- as a plain
  /// struct: the parity bit and one of two payloads. An edge element carries the edge's
  /// context as of subdivision (parent and child VALUES, payload, in-edge index); only the
  /// payload survives <see cref="Dagnumerable.Unsubdivide"/>, which reads the endpoints from
  /// the subdivided structure, so a bind that rewrites node values leaves stale values in edge
  /// contexts and nothing is wrong. Every edge operator is the node operator on this carrier:
  /// <c>SelectEdges</c>, <c>PruneEdges</c>, <c>ReplaceEdges</c> are the bind restricted to
  /// edge elements, pinned in the subdivision battery.
  /// </summary>
  public readonly struct DagElement<TNode, TEdge>
  {
    private DagElement(bool isEdge, TNode node, DagEdgeContext<TNode, TEdge> edge)
    {
      IsEdge = isEdge;
      Node = node;
      Edge = edge;
    }

    public readonly bool IsEdge;

    /// <summary>The node's value; <c>default</c> on an edge element.</summary>
    public readonly TNode Node;

    /// <summary>The edge's context as of subdivision; <c>default</c> on a node element.</summary>
    public readonly DagEdgeContext<TNode, TEdge> Edge;

    public static DagElement<TNode, TEdge> OfNode(TNode node) => new DagElement<TNode, TEdge>(false, node, default);

    public static DagElement<TNode, TEdge> OfEdge(DagEdgeContext<TNode, TEdge> edge) => new DagElement<TNode, TEdge>(true, default, edge);

    public override string ToString() => IsEdge ? $"[{Edge.Parent} -{Edge.Edge}-> {Edge.Child}]" : Node?.ToString() ?? "(null)";
  }
}
