using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The edge subdivision: every edge reified as a payload-bearing node between its endpoints
    /// -- <c>parent → [edge] → child</c> -- over <see cref="Unit"/> edges. The bijection the
    /// arrival protocol stated (the flat (edge, node) model IS node-only traversal of the
    /// subdivision), made an operator: on this carrier the node operators ARE the edge
    /// operators, and the bind on edge elements is <c>SelectEdges</c> / <c>PruneEdges</c> /
    /// <c>ReplaceEdges</c>. Parity alternates by construction (node elements point only to edge
    /// elements, each edge element has one in and one out), which is what
    /// <see cref="Unsubdivide"/> requires back. Edge elements sit after their parent in the
    /// parent's out-edge order; node elements keep their seats, edge elements are born here.
    /// Capture-shaped: the subdivision mints ordinals.
    /// </summary>
    public static DagBuffer<DagElement<TNode, TEdge>, Unit> Subdivide<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));

      var buffer = DagBuffer<TNode, TEdge>.From(source);
      var structure = buffer.Structure;
      var nodeCount = structure.NodeCount;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;
      var outPayloads = structure.OutPayloads;

      // Node n sits at n + (edges before its block); its edge elements follow it. Children are
      // later originals, so every edge element precedes its child: topological by construction.
      var values = new DagElement<TNode, TEdge>[nodeCount + outTargets.Length];
      var sourceOrdinals = new int[values.Length];
      var resultOffsets = new int[values.Length + 1];
      var resultTargets = new int[2 * outTargets.Length];
      var resultPayloads = new Unit[resultTargets.Length];
      var inEdgeIndexOfOutSlot = structure.InEdgeIndexOfOutSlot();
      var fill = 0;

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        var nodeElement = ordinal + outOffsets[ordinal];
        values[nodeElement] = DagElement<TNode, TEdge>.OfNode(buffer[ordinal]);
        sourceOrdinals[nodeElement] = buffer.SourceOrdinal(ordinal);

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
          resultTargets[fill++] = nodeElement + 1 + (slot - outOffsets[ordinal]);
        resultOffsets[nodeElement + 1] = fill;

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
        {
          var child = outTargets[slot];
          var edgeElement = nodeElement + 1 + (slot - outOffsets[ordinal]);
          values[edgeElement] = DagElement<TNode, TEdge>.OfEdge(
            new DagEdgeContext<TNode, TEdge>(buffer[ordinal], buffer[child], outPayloads[slot], inEdgeIndexOfOutSlot[slot]));
          sourceOrdinals[edgeElement] = -1;
          resultTargets[fill++] = child + outOffsets[child];
          resultOffsets[edgeElement + 1] = fill;
        }
      }

      return DagBuffer<DagElement<TNode, TEdge>, Unit>.FromParts(
        values,
        new DagStructure<Unit>(resultOffsets, resultTargets, resultPayloads),
        sourceOrdinals);
    }
  }
}
