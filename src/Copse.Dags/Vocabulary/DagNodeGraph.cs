using System;

namespace Copse.Dags
{
  /// <summary>
  /// Node replacement's element (NODE REPLACEMENT, design-docs/SUBSTITUTION_TAXONOMY.md):
  /// what one node becomes under <see cref="Dagnumerable.ReplaceNodes"/> -- a small acyclic
  /// graph with implicit wiring. The original's in-edges fan to the replacement's SOURCES; its
  /// out-edges fan from EVERY replacement node -- the lawful multiplicative pair (the
  /// taxonomy's every-node row; each division copy keeps its own edge to each neighbor, which
  /// is exactly what keeps deletion local and the laws intact). The shapes, by factory:
  ///
  /// <para><see cref="Keep"/> -- one node occupying the original's SEAT (its
  /// <c>SourceOrdinal</c> carries; a value rewrite is <c>SelectNodes</c>'s content).
  /// <see cref="Split"/> -- k fresh disconnected nodes: the node-division move (every
  /// alternative inherits every in- and out-edge). <see cref="Chain"/> -- a fresh path.
  /// <see cref="Graph"/> -- the general form: values plus forward internal edges.
  /// <see cref="Drop"/> (the <c>default</c> value) -- the node is deleted; downstream
  /// survival follows the family's LIVENESS rule, exactly as <c>PruneNodesBefore</c>'s does.</para>
  ///
  /// <para>Replacement nodes are FRESH except the seat-keeping <see cref="Keep"/> (no value
  /// comparison, so no existing node can be referenced). The seat rule is FACTORY-based, not
  /// count-based: only <see cref="Keep"/> carries the original's <c>SourceOrdinal</c> --
  /// <c>Split("only")</c> is a single born-here node. Internal edges must run FORWARD
  /// (<c>From &lt; To</c>) -- acyclic by construction, so the result buffer inherits its
  /// source's acyclicity certificate without revalidation, the <c>DagEdgePath</c> posture.</para>
  /// </summary>
  public readonly struct DagNodeGraph<TNode, TEdge>
  {
    private DagNodeGraph(TNode[] values, (int From, int To, TEdge Edge)[] edges, bool keepsSeat)
    {
      _Values = values;
      _Edges = edges;
      _KeepsSeat = keepsSeat;
    }

    private readonly TNode[] _Values;
    private readonly (int From, int To, TEdge Edge)[] _Edges;
    private readonly bool _KeepsSeat;

    /// <summary>Delete the node (the empty replacement; also the <c>default</c> value). Downstream survival follows the liveness rule.</summary>
    public static DagNodeGraph<TNode, TEdge> Drop => default;

    /// <summary>
    /// The seat-keeping identity: the node survives with <paramref name="value"/> as its value
    /// (same seat, same <c>SourceOrdinal</c>; a rewrite is <c>SelectNodes</c>'s content).
    /// </summary>
    public static DagNodeGraph<TNode, TEdge> Keep(TNode value) =>
      new(new[] { value }, null, keepsSeat: true);

    /// <summary>
    /// Node division: <paramref name="values"/> as fresh, disconnected alternatives -- every
    /// one a source AND a sink of the replacement, so every one inherits every in-edge and
    /// every out-edge of the original (payloads duplicated per fan; caller algebra).
    /// </summary>
    public static DagNodeGraph<TNode, TEdge> Split(params TNode[] values)
    {
      if (values == null)
        throw new ArgumentNullException(nameof(values));
      if (values.Length == 0)
        throw new ArgumentException("A split needs at least one alternative; delete with Drop.", nameof(values));

      return new DagNodeGraph<TNode, TEdge>((TNode[])values.Clone(), null, keepsSeat: false);
    }

    /// <summary>A fresh path: <paramref name="first"/>, then one (edge payload, node) link per further node.</summary>
    public static DagNodeGraph<TNode, TEdge> Chain(TNode first, params (TEdge Edge, TNode Node)[] links)
    {
      if (links == null)
        throw new ArgumentNullException(nameof(links));

      var values = new TNode[links.Length + 1];
      values[0] = first;
      var edges = links.Length == 0 ? null : new (int From, int To, TEdge Edge)[links.Length];

      for (var linkIndex = 0; linkIndex < links.Length; linkIndex++)
      {
        values[linkIndex + 1] = links[linkIndex].Node;
        edges[linkIndex] = (linkIndex, linkIndex + 1, links[linkIndex].Edge);
      }

      return new DagNodeGraph<TNode, TEdge>(values, edges, keepsSeat: false);
    }

    /// <summary>
    /// The general replacement: fresh <paramref name="values"/> with internal
    /// <paramref name="edges"/> among them, every edge running FORWARD
    /// (<c>From &lt; To</c> -- acyclic by construction; a backward edge throws).
    /// </summary>
    public static DagNodeGraph<TNode, TEdge> Graph(TNode[] values, params (int From, int To, TEdge Edge)[] edges)
    {
      if (values == null)
        throw new ArgumentNullException(nameof(values));
      if (values.Length == 0)
        throw new ArgumentException("A replacement graph needs at least one node; delete with Drop.", nameof(values));
      if (edges == null)
        throw new ArgumentNullException(nameof(edges));

      foreach (var edge in edges)
      {
        if (edge.From < 0 || edge.From >= values.Length || edge.To < 0 || edge.To >= values.Length)
          throw new ArgumentException($"Edge ({edge.From} -> {edge.To}) leaves the replacement's node range.", nameof(edges));
        if (edge.From >= edge.To)
          throw new ArgumentException(
            $"Edge ({edge.From} -> {edge.To}) does not run forward; internal edges must satisfy From < To (acyclic by construction).",
            nameof(edges));
      }

      return new DagNodeGraph<TNode, TEdge>(
        (TNode[])values.Clone(),
        edges.Length == 0 ? null : ((int From, int To, TEdge Edge)[])edges.Clone(),
        keepsSeat: false);
    }

    internal bool IsDrop => _Values == null;
    internal bool KeepsSeat => _KeepsSeat;
    internal TNode[] ValuesArray => _Values;

    /// <summary>The raw internal edges -- null when there are none (the rebuild's hot loop reads this directly, the <c>ValuesArray</c> posture).</summary>
    internal (int From, int To, TEdge Edge)[] EdgesArray => _Edges;

    /// <summary>The replacement's sources (no internal in-edge), in node order -- where the original's in-edges land.</summary>
    internal int[] SourceIndices()
    {
      // The dominant shapes short-circuit: no internal edges means every node is a source.
      if (_Edges == null)
      {
        var all = new int[_Values.Length];
        for (var nodeIndex = 0; nodeIndex < all.Length; nodeIndex++)
          all[nodeIndex] = nodeIndex;
        return all;
      }

      var hasInternalIn = new bool[_Values.Length];
      foreach (var edge in _Edges)
        hasInternalIn[edge.To] = true;

      var sourceCount = 0;
      foreach (var has in hasInternalIn)
        if (!has)
          sourceCount++;

      var sources = new int[sourceCount];
      var cursor = 0;
      for (var nodeIndex = 0; nodeIndex < hasInternalIn.Length; nodeIndex++)
        if (!hasInternalIn[nodeIndex])
          sources[cursor++] = nodeIndex;

      return sources;
    }
  }
}
