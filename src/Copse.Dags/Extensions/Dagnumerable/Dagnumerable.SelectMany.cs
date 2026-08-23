using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The dag monad's bind -- the pointed node substitution (SELECTMANY_DESIGN.md's verified
    /// semantics, dag-side): every node becomes the <see cref="DagExpansion{TNode, TEdge}"/>
    /// the selector returns; the node's in-edges reach the fragment's sources, its out-edges
    /// re-attach at the fragment's slot. Through a slot as source the in-edges meet the
    /// out-edges directly with payloads composed by <paramref name="edgeComposer"/>
    /// (<c>upstream ∘ downstream</c>) -- the composer's associativity is the bind's law, and
    /// a payload-bearing attachment composes the same way. The quartet derives the
    /// reshapings: <c>Return</c> is <c>SelectNodes</c>, <c>Leaf</c> is <c>PruneNodesAfter</c>,
    /// <c>Drop</c> is <c>PruneNodesBefore</c>, <c>Promote</c> is <c>Where</c> -- pinned
    /// content-exact in the derivation battery. Liveness is the family's one rule: a node is
    /// reached iff it is an original source or some parent's expansion conducts (has a slot);
    /// an unreached node's selector is never consulted and its fragment never exists.
    /// Capture-shaped, like every substitution operator: fresh ordinals need minting.
    /// </summary>
    public static DagBuffer<TResult, TEdge> SelectMany<TNode, TEdge, TResult>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, DagExpansion<TResult, TEdge>> selector,
      Func<TEdge, TEdge, TEdge> edgeComposer)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      if (selector == null)
        throw new ArgumentNullException(nameof(selector));
      if (edgeComposer == null)
        throw new ArgumentNullException(nameof(edgeComposer));

      var buffer = DagBuffer<TNode, TEdge>.From(source);
      var structure = buffer.Structure;
      var nodeCount = structure.NodeCount;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;
      var outPayloads = structure.OutPayloads;

      var inDegrees = new int[nodeCount];
      for (var slot = 0; slot < outTargets.Length; slot++)
        inDegrees[outTargets[slot]]++;

      // The reach pass, in topological order: an original is reached as a source or through a
      // conducting parent; a reached original's expansion conducts iff it has a slot.
      var reached = new bool[nodeCount];
      var hasConductingInbound = new bool[nodeCount];
      var expansions = new DagExpansion<TResult, TEdge>[nodeCount];

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        reached[ordinal] = inDegrees[ordinal] == 0 || hasConductingInbound[ordinal];

        if (!reached[ordinal])
          continue;

        expansions[ordinal] = selector(buffer[ordinal]);

        if (!expansions[ordinal].HasSlot)
          continue;

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
          hasConductingInbound[outTargets[slot]] = true;
      }

      // Placement: each reached original's fragment sits contiguously in its seat, fragment
      // order -- in-edges arrive from earlier originals, fragment edges run forward, out-edges
      // leave to later originals: topological by construction.
      var fragmentStart = new int[nodeCount];
      var resultValues = new List<TResult>();
      var resultSourceOrdinals = new List<int>();

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        fragmentStart[ordinal] = -1;

        if (!reached[ordinal] || expansions[ordinal].IsEmpty)
          continue;

        fragmentStart[ordinal] = resultValues.Count;
        var values = expansions[ordinal].ValuesArray;
        var sourceOrdinal = expansions[ordinal].KeepsSeat ? buffer.SourceOrdinal(ordinal) : -1;

        for (var nodeIndex = 0; nodeIndex < values.Length; nodeIndex++)
        {
          resultValues.Add(values[nodeIndex]);
          resultSourceOrdinals.Add(sourceOrdinal);
        }
      }

      // Inlets, in reverse topological order: where an edge INTO an original lands -- the
      // fragment's sources (payload passed through), plus, through every outside attachment, wherever
      // the original's own out-edges land, the out-edge's payload composed in front and the
      // attachment's (if any) in front of that.
      var inlets = new List<(int Target, bool HasSuffix, TEdge Suffix)>[nodeCount];

      for (var ordinal = nodeCount - 1; ordinal >= 0; ordinal--)
      {
        if (!reached[ordinal])
          continue;

        var expansion = expansions[ordinal];
        var landing = new List<(int Target, bool HasSuffix, TEdge Suffix)>();

        if (!expansion.IsEmpty)
          foreach (var fragmentSource in expansion.SourceIndices())
            landing.Add((fragmentStart[ordinal] + fragmentSource, false, default));

        foreach (var attachment in expansion.Slot.Attachments)
        {
          if (!attachment.IsFromOutside)
            continue;

          for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
          {
            var child = outTargets[slot];

            if (!reached[child])
              continue;

            foreach (var inlet in inlets[child])
            {
              var payload = inlet.HasSuffix ? edgeComposer(outPayloads[slot], inlet.Suffix) : outPayloads[slot];
              landing.Add((inlet.Target, true, attachment.HasPayload ? edgeComposer(attachment.Payload, payload) : payload));
            }
          }
        }

        inlets[ordinal] = landing;
      }

      // The out-blocks, in result order: each fragment node's internal edges first (own
      // children before inherited), then, for every slot attachment it holds, the original's
      // out-edges in out-edge order, each landing on its child's inlets with the attachment's
      // payload (if any) composed in front.
      var resultOffsets = new List<int>(resultValues.Count + 1) { 0 };
      var resultTargets = new List<int>();
      var resultPayloads = new List<TEdge>();

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (fragmentStart[ordinal] < 0)
          continue;

        var expansion = expansions[ordinal];
        var values = expansion.ValuesArray;
        var edges = expansion.EdgesArray;
        var attachments = expansion.Slot.Attachments;

        for (var nodeIndex = 0; nodeIndex < values.Length; nodeIndex++)
        {
          if (edges != null)
            for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
              if (edges[edgeIndex].From != nodeIndex)
                continue;

              resultTargets.Add(fragmentStart[ordinal] + edges[edgeIndex].To);
              resultPayloads.Add(edges[edgeIndex].Edge);
            }

          for (var attachmentIndex = 0; attachmentIndex < attachments.Length; attachmentIndex++)
          {
            var attachment = attachments[attachmentIndex];

            if (attachment.IsFromOutside || attachment.FragmentNode != nodeIndex)
              continue;

            for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
            {
              var child = outTargets[slot];

              if (!reached[child])
                continue;

              foreach (var inlet in inlets[child])
              {
                var payload = inlet.HasSuffix ? edgeComposer(outPayloads[slot], inlet.Suffix) : outPayloads[slot];
                resultTargets.Add(inlet.Target);
                resultPayloads.Add(attachment.HasPayload ? edgeComposer(attachment.Payload, payload) : payload);
              }
            }
          }

          resultOffsets.Add(resultTargets.Count);
        }
      }

      return DagBuffer<TResult, TEdge>.FromParts(
        resultValues.ToArray(),
        new DagStructure<TEdge>(resultOffsets.ToArray(), resultTargets.ToArray(), resultPayloads.ToArray()),
        resultSourceOrdinals.ToArray());
    }
  }
}
