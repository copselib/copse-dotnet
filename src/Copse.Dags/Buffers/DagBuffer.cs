using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  /// <summary>
  /// The capture tier (the re-founding; design-docs/DAG_CONTRACT_DESIGN.md): an owned,
  /// immutable CSR capture of a dag -- <see cref="Values"/> in entry (topological) order with
  /// the DENSE INDEX as the ordinal, out-adjacency as flat parallel arrays preserving
  /// per-parent out-edge order, and a <see cref="SourceOrdinal"/> back-map when captured from
  /// a gapped stream. One type, three roles: <c>Materialize</c>'s return, the folds' result
  /// shape, the flat store -- and the family's walkable buffer (dense ordinals as handles). Composes through the fluent
  /// surface (it IS an <see cref="IDagnumerable{TNode, TEdge}"/>), so materialization breaks
  /// laziness, never fluency. <see cref="Transpose"/> is free in the amortized sense: the
  /// transpose adjacency is built lazily once and back-linked, so transposing back costs an
  /// O(n) value reversal and no adjacency work at all.
  /// </summary>
  public sealed class DagBuffer<TNode, TEdge> : IWalkableDagnumerable<TNode, int, TEdge>
  {
    internal DagBuffer(TNode[] values, DagStructure<TEdge> structure, int[] sourceOrdinals)
    {
      _Values = values;
      Structure = structure;
      _SourceOrdinals = sourceOrdinals;
    }

    private readonly TNode[] _Values;
    private readonly int[] _SourceOrdinals;

    internal DagStructure<TEdge> Structure { get; }

    public int Count => _Values.Length;
    public IReadOnlyList<TNode> Values => _Values;
    public TNode this[int ordinal] => _Values[ordinal];

    /// <summary>
    /// The captured stream's ordinal for this buffer's dense <paramref name="ordinal"/> -- the
    /// correlation key back to the source enumeration (identity when the source was dense).
    /// </summary>
    public int SourceOrdinal(int ordinal) =>
      _SourceOrdinals == null ? ordinal : _SourceOrdinals[ordinal];

    public IDagnumerator<TNode, TEdge> GetDagnumerator() =>
      new TopologicalDagnumerator<TNode, TEdge>(_Values, Structure.OutOffsets, Structure.OutTargets, Structure.OutPayloads);

    private DagBufferTopology<TNode, TEdge> _Topology;

    // The door (the buffer re-parent, dag-side: captures are never address-poor): the unfocused
    // walker over the CSR skeleton, built once, handles = dense ordinals.
    public DagWalker<TNode, int, TEdge> GetDagWalker()
      => new DagWalker<TNode, int, TEdge>(_Topology ??= new DagBufferTopology<TNode, TEdge>(_Values, Structure));

    /// <summary>
    /// The orientation flip -- the operator the retired backward dimension became: the same
    /// nodes and edges with every arrow reversed, presented in the transpose's own topological
    /// order (the reverse of this buffer's). The whole forward operator family now points
    /// upward through it; transpose back to return to this orientation.
    /// </summary>
    public DagBuffer<TNode, TEdge> Transpose()
    {
      var count = _Values.Length;
      var values = new TNode[count];
      for (var ordinal = 0; ordinal < count; ordinal++)
        values[count - 1 - ordinal] = _Values[ordinal];

      int[] sourceOrdinals = null;
      if (_SourceOrdinals != null)
      {
        sourceOrdinals = new int[count];
        for (var ordinal = 0; ordinal < count; ordinal++)
          sourceOrdinals[count - 1 - ordinal] = _SourceOrdinals[ordinal];
      }

      return new DagBuffer<TNode, TEdge>(values, Structure.Transpose(), sourceOrdinals);
    }

    /// <summary>A fold's result: fresh values over this buffer's SHARED structure.</summary>
    internal DagBuffer<TResult, TEdge> WithValues<TResult>(TResult[] values) =>
      new(values, Structure, _SourceOrdinals);

    /// <summary>An edge rewrite's result: same values and shape, fresh payloads.</summary>
    internal DagBuffer<TNode, TEdgeResult> WithPayloads<TEdgeResult>(TEdgeResult[] payloads) =>
      new(_Values, Structure.WithPayloads(payloads), _SourceOrdinals);

    /// <summary>
    /// Builds a buffer from rebuild parts, collapsing an identity ordinal map to null -- the
    /// SourceOrdinal invariant's one home (an identity back-map MUST collapse, or dense and
    /// gapped captures would answer <see cref="SourceOrdinal"/> differently).
    /// </summary>
    internal static DagBuffer<TNode, TEdge> FromParts(TNode[] values, DagStructure<TEdge> structure, int[] sourceOrdinals)
    {
      var dense = true;
      for (var ordinal = 0; ordinal < sourceOrdinals.Length; ordinal++)
        if (sourceOrdinals[ordinal] != ordinal)
        {
          dense = false;
          break;
        }

      return new DagBuffer<TNode, TEdge>(values, structure, dense ? null : sourceOrdinals);
    }

    /// <summary>
    /// Captures a source's live stream in ONE pass (dispatch contiguity makes each adjacency
    /// block contiguous in arrival order -- the edge-grained stream paying for itself),
    /// re-keying possibly-gapped stream ordinals to dense indices.
    /// </summary>
    internal static DagBuffer<TNode, TEdge> From(IDagnumerable<TNode, TEdge> source)
    {
      if (source is DagBuffer<TNode, TEdge> buffer)
        return buffer;

      var values = new List<TNode>();
      var streamOrdinals = new List<int>();
      var denseByStream = new Dictionary<int, int>();
      var outDegrees = new List<int>();
      var edgeTargetStreamOrdinals = new List<int>();
      var edgePayloads = new List<TEdge>();

      using var walk = source.GetDagnumerator();
      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
      {
        if (walk.Mode == DagnumeratorMode.EnteringNode)
        {
          denseByStream[walk.Ordinal] = values.Count;
          values.Add(walk.Node);
          streamOrdinals.Add(walk.Ordinal);
          outDegrees.Add(0);
          continue;
        }

        if (walk.ParentOrdinal < 0)
          continue;

        // The dispatching parent has entered (contiguity), so it is the last dense node; its
        // block's edges append in out-edge order -- CSR slot order, directly.
        outDegrees[denseByStream[walk.ParentOrdinal]]++;
        edgeTargetStreamOrdinals.Add(walk.Ordinal);
        edgePayloads.Add(walk.Edge);
      }

      var offsets = new int[values.Count + 1];
      for (var ordinal = 0; ordinal < values.Count; ordinal++)
        offsets[ordinal + 1] = offsets[ordinal] + outDegrees[ordinal];

      var targets = new int[edgeTargetStreamOrdinals.Count];
      for (var slot = 0; slot < targets.Length; slot++)
        targets[slot] = denseByStream[edgeTargetStreamOrdinals[slot]];

      return FromParts(
        values.ToArray(),
        new DagStructure<TEdge>(offsets, targets, edgePayloads.ToArray()),
        streamOrdinals.ToArray());
    }
  }
}
