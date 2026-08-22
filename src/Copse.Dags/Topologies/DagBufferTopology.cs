namespace Copse.Dags
{
  // The buffer's topology: the CSR skeleton answering every probe from arrays (the tree family's
  // PreorderArrayTopology, dualized -- and simpler, because a dag skeleton IS adjacency: the
  // out-edge CSR is the child axis directly, the lazily built in-adjacency is the parent axis,
  // and the sources are the in-degree-zero ordinals, in ordinal order). Handles are the buffer's
  // dense ordinals. A child probe is three array reads, a parent probe four (the payload lives
  // once, in the out arrays, reached through the in-edge's out-slot). No per-node objects.
  internal sealed class DagBufferTopology<TNode, TEdge> : IDagTopology<TNode, int, TEdge>
  {
    public DagBufferTopology(TNode[] values, DagStructure<TEdge> structure)
    {
      _Values = values;
      _Structure = structure;
    }

    private readonly TNode[] _Values;
    private readonly DagStructure<TEdge> _Structure;
    private int[] _InOffsets;
    private int[] _InParents;
    private int[] _InEdgeOutSlots;
    private int[] _SourceOrdinals;

    public TNode GetValue(int handle) => _Values[handle];

    public DagStep<int, TEdge> TryGetParentAt(int handle, int inEdgeIndex)
    {
      EnsureInAdjacency();

      var firstSlot = _InOffsets[handle];

      if (inEdgeIndex < 0 || inEdgeIndex >= _InOffsets[handle + 1] - firstSlot)
        return default;

      var slot = firstSlot + inEdgeIndex;

      return new DagStep<int, TEdge>(_InParents[slot], _Structure.OutPayloads[_InEdgeOutSlots[slot]], inEdgeIndex);
    }

    public DagStep<int, TEdge> TryGetChildAt(int handle, int outEdgeIndex)
    {
      var outOffsets = _Structure.OutOffsets;
      var firstSlot = outOffsets[handle];

      if (outEdgeIndex < 0 || outEdgeIndex >= outOffsets[handle + 1] - firstSlot)
        return default;

      var slot = firstSlot + outEdgeIndex;

      return new DagStep<int, TEdge>(_Structure.OutTargets[slot], _Structure.OutPayloads[slot], outEdgeIndex);
    }

    public DagStep<int, TEdge> TryGetSourceAt(int sourceIndex)
    {
      EnsureInAdjacency();

      if (sourceIndex < 0 || sourceIndex >= _SourceOrdinals.Length)
        return default;

      return new DagStep<int, TEdge>(_SourceOrdinals[sourceIndex], default, sourceIndex);
    }

    private void EnsureInAdjacency()
    {
      if (_InOffsets != null)
        return;

      var (inOffsets, inParents, inEdgeOutSlots) = _Structure.InAdjacency();
      var sourceCount = 0;

      for (var ordinal = 0; ordinal < _Values.Length; ordinal++)
        if (inOffsets[ordinal + 1] == inOffsets[ordinal])
          sourceCount++;

      var sourceOrdinals = new int[sourceCount];
      var sourceIndex = 0;

      for (var ordinal = 0; ordinal < _Values.Length; ordinal++)
        if (inOffsets[ordinal + 1] == inOffsets[ordinal])
          sourceOrdinals[sourceIndex++] = ordinal;

      _InParents = inParents;
      _InEdgeOutSlots = inEdgeOutSlots;
      _SourceOrdinals = sourceOrdinals;
      _InOffsets = inOffsets;
    }
  }
}
