using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  /// <summary>
  /// What one node becomes under the bind (<see cref="Dagnumerable.SelectMany"/>): a
  /// FRAGMENT (nodes and the forward edges among them) and a <see cref="DagSlot{TEdge}"/>
  /// saying where the original's out-edges re-attach. The original's in-edges reach the
  /// fragment's sources; the out-edges reach the slot. The quartet every reshaping is made of:
  /// <see cref="Return"/> (the node, slot beneath it -- <c>SelectNodes</c>), <see cref="Leaf"/>
  /// (the node, no slot -- <c>PruneNodesAfter</c>), <see cref="Drop"/> (nothing, no slot --
  /// <c>PruneNodesBefore</c>), <see cref="Promote"/> (nothing, slot as source -- <c>Where</c>'s
  /// bypass). <see cref="Of"/> is the general form. Single-node expansions keep the original's
  /// seat (its source ordinal carries); everything else is born here.
  /// </summary>
  public readonly struct DagExpansion<TNode, TEdge>
  {
    private DagExpansion(TNode[] values, (int From, int To, TEdge Edge)[] edges, DagSlot<TEdge> slot, bool keepsSeat)
    {
      _Values = values;
      _Edges = edges;
      Slot = slot;
      KeepsSeat = keepsSeat;
    }

    private readonly TNode[] _Values;
    private readonly (int From, int To, TEdge Edge)[] _Edges;

    public DagSlot<TEdge> Slot { get; }

    /// <summary>True for a single-node expansion: the node occupies the original's seat.</summary>
    public bool KeepsSeat { get; }

    /// <summary>The fragment's nodes, in fragment order (empty for <see cref="Drop"/> and <see cref="Promote"/>).</summary>
    public IReadOnlyList<TNode> Values => _Values ?? Array.Empty<TNode>();

    /// <summary>The fragment's internal edges, From &lt; To.</summary>
    public IReadOnlyList<(int From, int To, TEdge Edge)> Edges => _Edges ?? Array.Empty<(int, int, TEdge)>();

    public bool IsEmpty => _Values == null || _Values.Length == 0;

    public bool HasSlot => !Slot.IsNone;

    internal TNode[] ValuesArray => _Values ?? Array.Empty<TNode>();
    internal (int From, int To, TEdge Edge)[] EdgesArray => _Edges;

    /// <summary>Nothing, no slot: the node and its out-edges die; liveness cascades.</summary>
    public static DagExpansion<TNode, TEdge> Drop => default;

    /// <summary>Nothing, slot as source: the node dissolves and its in-edges meet its out-edges, payloads composed.</summary>
    public static DagExpansion<TNode, TEdge> Promote => new DagExpansion<TNode, TEdge>(null, null, DagSlot<TEdge>.Source, keepsSeat: false);

    /// <summary>The node with the slot beneath it: the identity shape, value rewritten.</summary>
    public static DagExpansion<TNode, TEdge> Return(TNode value)
      => new DagExpansion<TNode, TEdge>(new[] { value }, null, DagSlot<TEdge>.Under(0), keepsSeat: true);

    /// <summary>The node with no slot: kept, its out-edges dropped.</summary>
    public static DagExpansion<TNode, TEdge> Leaf(TNode value)
      => new DagExpansion<TNode, TEdge>(new[] { value }, null, DagSlot<TEdge>.None, keepsSeat: true);

    /// <summary>The general expansion: a fragment, its forward edges, and the slot.</summary>
    public static DagExpansion<TNode, TEdge> Of(TNode[] values, (int From, int To, TEdge Edge)[] edges, DagSlot<TEdge> slot)
    {
      if (values == null)
        throw new ArgumentNullException(nameof(values));
      if (edges == null)
        throw new ArgumentNullException(nameof(edges));

      foreach (var edge in edges)
      {
        if (edge.From < 0 || edge.From >= values.Length || edge.To < 0 || edge.To >= values.Length)
          throw new ArgumentException($"Edge ({edge.From} -> {edge.To}) leaves the fragment's node range.", nameof(edges));
        if (edge.From >= edge.To)
          throw new ArgumentException($"Edge ({edge.From} -> {edge.To}) does not run forward; fragment edges must satisfy From < To.", nameof(edges));
      }

      foreach (var attachment in slot.Attachments)
        if (!attachment.IsFromOutside && (attachment.FragmentNode < 0 || attachment.FragmentNode >= values.Length))
          throw new ArgumentException($"Slot attachment under {attachment.FragmentNode} leaves the fragment's node range.", nameof(slot));

      return new DagExpansion<TNode, TEdge>(
        (TNode[])values.Clone(),
        edges.Length == 0 ? null : ((int, int, TEdge)[])edges.Clone(),
        slot,
        keepsSeat: values.Length == 1);
    }

    /// <summary>The fragment's sources -- nodes with no internal in-edge -- in fragment order.</summary>
    internal int[] SourceIndices()
    {
      var values = ValuesArray;

      if (_Edges == null)
      {
        var all = new int[values.Length];
        for (var index = 0; index < all.Length; index++)
          all[index] = index;
        return all;
      }

      var hasInternalIn = new bool[values.Length];
      foreach (var edge in _Edges)
        hasInternalIn[edge.To] = true;

      var count = 0;
      for (var index = 0; index < values.Length; index++)
        if (!hasInternalIn[index])
          count++;

      var sources = new int[count];
      var fill = 0;
      for (var index = 0; index < values.Length; index++)
        if (!hasInternalIn[index])
          sources[fill++] = index;

      return sources;
    }
  }
}
