using System;

namespace Copse.Dags
{
  // A dag's shape as flat CSR arrays -- the structural half of DagBuffer, split out so result
  // buffers SHARE it by reference (a fold rewrites values, not structure; an edge rewrite
  // shares offsets/targets with fresh payloads). The transpose structure is built lazily once
  // and back-linked, so Transpose().Transpose() round-trips to the same arrays for free -- the
  // sinkfix derivation's whole cost model. Per-group order is a PRESENTATION fact, frozen
  // here: out-edges in dispatch order; the transpose's out-edges (this dag's in-edges) in
  // discovery order, which the slot-order counting fill below reproduces exactly (edges
  // iterated parent-major in topological order, each block in out-edge order).
  internal sealed class DagStructure<TEdge>
  {
    public DagStructure(int[] outOffsets, int[] outTargets, TEdge[] outPayloads)
    {
      OutOffsets = outOffsets;
      OutTargets = outTargets;
      OutPayloads = outPayloads;
    }

    public int[] OutOffsets { get; }
    public int[] OutTargets { get; }
    public TEdge[] OutPayloads { get; }

    public int NodeCount => OutOffsets.Length - 1;
    public int EdgeCount => OutTargets.Length;

    private DagStructure<TEdge> _Transpose;
    private int[] _InOffsets;
    private int[] _InParents;
    private int[] _InEdgeOutSlots;

    /// <summary>
    /// The in-adjacency in THIS structure's ordinal space, built lazily once by the same
    /// slot-order counting fill as the transpose (so per-group order is in-edge DISCOVERY
    /// order): <c>InOffsets</c>/<c>InParents</c> are the CSR pair; <c>InEdgeOutSlots[j]</c>
    /// is in-edge j's slot in the OUT arrays (its payload is <c>OutPayloads[slot]</c> --
    /// payloads are stored once), which is also the per-edge correlation between the two
    /// adjacencies the dispatch passes route through.
    /// </summary>
    public (int[] InOffsets, int[] InParents, int[] InEdgeOutSlots) InAdjacency()
    {
      if (_InOffsets != null)
        return (_InOffsets, _InParents, _InEdgeOutSlots);

      var nodeCount = NodeCount;
      var offsets = new int[nodeCount + 1];
      var parents = new int[EdgeCount];
      var outSlots = new int[EdgeCount];

      for (var slot = 0; slot < OutTargets.Length; slot++)
        offsets[OutTargets[slot] + 1]++;
      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
        offsets[ordinal + 1] += offsets[ordinal];

      var cursor = new int[nodeCount];
      for (var parent = 0; parent < nodeCount; parent++)
      {
        for (var slot = OutOffsets[parent]; slot < OutOffsets[parent + 1]; slot++)
        {
          var child = OutTargets[slot];
          var fillSlot = offsets[child] + cursor[child]++;
          parents[fillSlot] = parent;
          outSlots[fillSlot] = slot;
        }
      }

      _InParents = parents;
      _InEdgeOutSlots = outSlots;
      _InOffsets = offsets;
      return (_InOffsets, _InParents, _InEdgeOutSlots);
    }

    /// <summary>An edge rewrite: same shape, fresh payloads (offsets/targets shared by reference).</summary>
    public DagStructure<TEdge2> WithPayloads<TEdge2>(TEdge2[] payloads) =>
      new(OutOffsets, OutTargets, payloads);

    /// <summary>
    /// The transpose's structure, in the transpose's own ordinal space (transpose ordinal
    /// t = NodeCount − 1 − o: the reverse of a topological order is a topological order of
    /// the transpose). Built once, cached, back-linked.
    /// </summary>
    public DagStructure<TEdge> Transpose()
    {
      if (_Transpose != null)
        return _Transpose;

      var nodeCount = NodeCount;
      var offsets = new int[nodeCount + 1];
      var targets = new int[EdgeCount];
      var payloads = new TEdge[EdgeCount];

      // Pass 1: transpose out-degrees (= this dag's in-degrees), indexed by transpose ordinal.
      for (var slot = 0; slot < OutTargets.Length; slot++)
        offsets[nodeCount - 1 - OutTargets[slot] + 1]++;
      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
        offsets[ordinal + 1] += offsets[ordinal];

      // Pass 2: fill in slot order -- parent-major in topological order, each dispatch block
      // in out-edge order -- which IS in-edge discovery order, the promised per-group order.
      var cursor = new int[nodeCount];
      for (var parent = 0; parent < nodeCount; parent++)
      {
        for (var slot = OutOffsets[parent]; slot < OutOffsets[parent + 1]; slot++)
        {
          var transposeSource = nodeCount - 1 - OutTargets[slot];
          var fillSlot = offsets[transposeSource] + cursor[transposeSource]++;
          targets[fillSlot] = nodeCount - 1 - parent;
          payloads[fillSlot] = OutPayloads[slot];
        }
      }

      _Transpose = new DagStructure<TEdge>(offsets, targets, payloads) { _Transpose = this };
      return _Transpose;
    }
  }
}
