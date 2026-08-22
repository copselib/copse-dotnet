using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  /// <summary>
  /// Edge replacement's element (EDGE REPLACEMENT, 2026-08-07, design-docs/DAG_CONTRACT_DESIGN.md): what
  /// one edge becomes under <see cref="Dagnumerable.ReplaceEdges"/> -- a PATH with implicit
  /// endpoints. The substitute for <c>u -(f)-> v</c> starts at <c>u</c>, runs through zero or
  /// more fresh interior nodes, and ends at <c>v</c>: a first payload, then one
  /// <see cref="DagEdgePathLink{TNode, TEdge}"/> per interior node (the node, then the payload
  /// leaving it). The three shapes, by factory:
  ///
  /// <para><see cref="Keep"/> -- one edge, no interior: the identity (or a payload rewrite --
  /// <c>Keep(different)</c> is lawful, though the streaming <c>SelectEdges</c> is the cheaper
  /// home for pure rewrites). <see cref="Through"/> -- one interior node: subdivision, the
  /// reify-the-missing-entity move (a SIP between the stake and the entity it flows to).
  /// <see cref="Chain"/> -- any number of interior nodes. <see cref="Drop"/> (the
  /// <c>default</c> value) -- zero edges: the edge is deleted, and node survival follows the
  /// family's LIVENESS rule exactly as <c>PruneEdges</c>' does (what lies below survives only
  /// where another live path reaches it) -- one edge-removal semantics, not two.</para>
  ///
  /// <para>Interior nodes are always FRESH: values are supplied, identity is new, and no
  /// existing node can be referenced (that would take a value comparison, which the library
  /// never performs). Freshness is what makes the replacement CYCLE-SAFE BY CONSTRUCTION: interior
  /// nodes subdivide existing edges, so an acyclic source cannot produce a cyclic result, and
  /// the result buffer inherits the certificate without revalidation.</para>
  /// </summary>
  public readonly struct DagEdgePath<TNode, TEdge>
  {
    private DagEdgePath(TEdge firstEdge, IReadOnlyList<DagEdgePathLink<TNode, TEdge>> links)
    {
      _HasEdges = true;
      _FirstEdge = firstEdge;
      _Links = links;
    }

    private readonly bool _HasEdges;
    private readonly TEdge _FirstEdge;
    private readonly IReadOnlyList<DagEdgePathLink<TNode, TEdge>> _Links;

    /// <summary>Delete the edge (the empty path; also the <c>default</c> value). Node survival follows the liveness rule.</summary>
    public static DagEdgePath<TNode, TEdge> Drop => default;

    /// <summary>The identity-shaped path: the edge survives with <paramref name="edge"/> as its payload, no interior nodes.</summary>
    public static DagEdgePath<TNode, TEdge> Keep(TEdge edge) =>
      new(edge, null);

    /// <summary>Subdivision: <c>u -(inboundEdge)-> [node] -(outboundEdge)-> v</c>.</summary>
    public static DagEdgePath<TNode, TEdge> Through(TEdge inboundEdge, TNode node, TEdge outboundEdge) =>
      new(inboundEdge, new[] { new DagEdgePathLink<TNode, TEdge>(node, outboundEdge) });

    /// <summary>The general chain: a first payload, then one link per interior node in order.</summary>
    public static DagEdgePath<TNode, TEdge> Chain(TEdge firstEdge, params DagEdgePathLink<TNode, TEdge>[] links)
    {
      if (links == null)
        throw new ArgumentNullException(nameof(links));

      return new(firstEdge, links);
    }

    internal bool IsDrop => !_HasEdges;
    internal TEdge FirstEdge => _FirstEdge;
    internal IReadOnlyList<DagEdgePathLink<TNode, TEdge>> Links =>
      _Links ?? Array.Empty<DagEdgePathLink<TNode, TEdge>>();
  }

  /// <summary>One interior node of a <see cref="DagEdgePath{TNode, TEdge}"/>: the node, then the payload leaving it.</summary>
  public readonly struct DagEdgePathLink<TNode, TEdge>
  {
    public DagEdgePathLink(TNode node, TEdge edge)
    {
      Node = node;
      Edge = edge;
    }

    public readonly TNode Node;
    public readonly TEdge Edge;
  }
}
