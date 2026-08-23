using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The subdivision undone: node elements become nodes, each edge element becomes the edge
    /// from its one parent to its one child with the element's payload. The PARITY PREDICATE is
    /// the validity condition and is checked, with coordinates: a node element may point only to
    /// edge elements; an edge element has exactly one parent and exactly one child, both node
    /// elements; a source is a node element. What the predicate refuses is exactly what the
    /// family refuses by principle: a promoted edge element is EDGE CONTRACTION (its endpoints
    /// would merge), a leafed edge element is a dangling edge, a promoted node element leaves
    /// edge elements adjacent. Node elements keep the seats they carried; the result's ordinals
    /// are dense in the subdivision's order.
    /// </summary>
    public static DagBuffer<TNode, TEdge> Unsubdivide<TNode, TEdge>(
      this IDagnumerable<DagElement<TNode, TEdge>, Unit> source)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));

      var buffer = DagBuffer<DagElement<TNode, TEdge>, Unit>.From(source);
      var structure = buffer.Structure;
      var elementCount = structure.NodeCount;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;

      var inDegrees = structure.InDegrees();

      var nodeOrdinalOf = new int[elementCount];
      var values = new List<TNode>();
      var sourceOrdinals = new List<int>();

      for (var element = 0; element < elementCount; element++)
      {
        if (buffer[element].IsEdge)
        {
          nodeOrdinalOf[element] = -1;

          if (inDegrees[element] != 1)
            throw new InvalidOperationException(
              $"Element {element} {buffer[element]} is an edge with {inDegrees[element]} parents; an edge element has exactly one (a parentless edge element is a contracted or dangling edge).");
          if (outOffsets[element + 1] - outOffsets[element] != 1)
            throw new InvalidOperationException(
              $"Element {element} {buffer[element]} is an edge with {outOffsets[element + 1] - outOffsets[element]} children; an edge element has exactly one (a childless edge element is a dangling edge).");
          if (buffer[outTargets[outOffsets[element]]].IsEdge)
            throw new InvalidOperationException(
              $"Element {element} {buffer[element]} is an edge pointing at edge element {outTargets[outOffsets[element]]}; parity must alternate (a promoted node element leaves its edges adjacent).");

          continue;
        }

        nodeOrdinalOf[element] = values.Count;
        values.Add(buffer[element].Node);
        sourceOrdinals.Add(buffer.SourceOrdinal(element));

        for (var slot = outOffsets[element]; slot < outOffsets[element + 1]; slot++)
          if (!buffer[outTargets[slot]].IsEdge)
            throw new InvalidOperationException(
              $"Element {element} {buffer[element]} is a node pointing at node element {outTargets[slot]}; parity must alternate (a promoted edge element is edge CONTRACTION, which this family refuses by principle).");
      }

      var resultOffsets = new List<int>(values.Count + 1) { 0 };
      var resultTargets = new List<int>();
      var resultPayloads = new List<TEdge>();

      for (var element = 0; element < elementCount; element++)
      {
        if (buffer[element].IsEdge)
          continue;

        for (var slot = outOffsets[element]; slot < outOffsets[element + 1]; slot++)
        {
          var edgeElement = outTargets[slot];
          resultTargets.Add(nodeOrdinalOf[outTargets[outOffsets[edgeElement]]]);
          resultPayloads.Add(buffer[edgeElement].Edge.Edge);
        }

        resultOffsets.Add(resultTargets.Count);
      }

      return DagBuffer<TNode, TEdge>.FromParts(
        values.ToArray(),
        new DagStructure<TEdge>(resultOffsets.ToArray(), resultTargets.ToArray(), resultPayloads.ToArray()),
        sourceOrdinals.ToArray());
    }
  }
}
