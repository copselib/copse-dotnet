using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Copse.Core
{
  /// <summary>
  /// A node's local coordinates: its zero-based index among its siblings, and its depth
  /// (roots are depth 0). A position is not a globally unique address -- it locates a node
  /// only relative to its own family, and nodes in different families can share the same
  /// (siblingIndex, depth) pair -- so it cannot serve as a lookup key. For durable node
  /// identity, use a walkable capture's handles. Positions order by depth first, then by
  /// sibling index.
  /// </summary>
  public readonly struct NodePosition : IEqualityComparer<NodePosition>, IComparable<NodePosition>
  {
    /// <summary>Creates the position (<paramref name="siblingIndex"/>, <paramref name="depth"/>).</summary>
    public NodePosition(int siblingIndex, int depth)
    {
      SiblingIndex = siblingIndex;
      Depth = depth;
    }

    /// <summary>The node's zero-based index among its siblings. Roots are siblings of each
    /// other: a forest's roots have sibling indices 0, 1, 2, …</summary>
    public int SiblingIndex { get; }

    /// <summary>The node's depth: 0 for roots, one more for each level below.</summary>
    public int Depth { get; }

    /// <summary>
    /// The position of the virtual forest root: depth -1, above every real node. This is the
    /// contractual pre-enumeration position -- a treenumerator's <c>Position</c> before its first
    /// <c>MoveNext</c> -- and the "no parent yet" seed position operators use for sentinels and
    /// accumulator roots. Implementations must initialize to this, not <c>default</c> (which reads
    /// as an already-scheduled root and desyncs wrappers that snapshot pre-enumeration state).
    /// </summary>
    public static readonly NodePosition ForestRoot = new NodePosition(0, -1);

    /// <summary>
    /// True when this is the virtual forest root (the pre-enumeration position): its depth is
    /// negative, above every real node. Prefer this over <c>== ForestRoot</c> on per-node hot
    /// paths -- it is a single field compare the JIT folds inline, whereas reading the
    /// <c>static readonly</c> <see cref="ForestRoot"/> is a non-foldable static-field load.
    /// </summary>
    public bool IsForestRoot
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => Depth < 0;
    }

    /// <summary>Renders as "(siblingIndex, depth)".</summary>
    public override string ToString()
      => $"({SiblingIndex}, {Depth})";

    #region Equality Comparison

    public override bool Equals(object obj)
    {
      if (!(obj is NodePosition nodePosition))
        return false;

      return Equals(this, nodePosition);
    }

    public override int GetHashCode()
      => GetHashCode(this);

    public bool Equals(NodePosition left, NodePosition right)
      => left.SiblingIndex == right.SiblingIndex && left.Depth == right.Depth;

    public static bool operator ==(NodePosition left, NodePosition right)
      => left.Equals(left, right);

    public static bool operator !=(NodePosition left, NodePosition right)
      => !left.Equals(left, right);

    public int GetHashCode(NodePosition nodePosition)
      => (nodePosition.SiblingIndex, nodePosition.Depth).GetHashCode();

    #endregion Equality Comparison

    #region Order Comparison

    /// <summary>Orders by depth first, then by sibling index within a depth.</summary>
    public int CompareTo(NodePosition other)
    {
      if (other.Depth < Depth)
        return 1;

      if (other.Depth > Depth)
        return -1;

      if (other.SiblingIndex < SiblingIndex)
        return 1;

      if (other.SiblingIndex > SiblingIndex)
        return -1;

      return 0;
    }

    public static bool operator <(NodePosition left, NodePosition right)
      => left.CompareTo(right) < 0;

    public static bool operator >(NodePosition left, NodePosition right)
      => left.CompareTo(right) > 0;

    public static bool operator <=(NodePosition left, NodePosition right)
      => left.CompareTo(right) <= 0;

    public static bool operator >=(NodePosition left, NodePosition right)
      => left.CompareTo(right) >= 0;

    #endregion Order Comparison
  }
}
