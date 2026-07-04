using System;
using System.Collections.Generic;

namespace Copse.Core
{
  public readonly struct NodePosition : IEqualityComparer<NodePosition>, IComparable<NodePosition>
  {
    public NodePosition(int siblingIndex, int depth)
    {
      SiblingIndex = siblingIndex;
      Depth = depth;
    }

    public int SiblingIndex { get; }
    public int Depth { get; }

    /// <summary>
    /// The position of the virtual forest root: depth -1, above every real node. This is the
    /// contractual pre-enumeration position -- a treenumerator's <c>Position</c> before its first
    /// <c>MoveNext</c> -- and the "no parent yet" seed position operators use for sentinels and
    /// accumulator roots. Implementations must initialize to this, not <c>default</c> (which reads
    /// as an already-scheduled root and desyncs wrappers that snapshot pre-enumeration state).
    /// </summary>
    public static readonly NodePosition ForestRoot = new NodePosition(0, -1);

    public override string ToString()
      => $"({SiblingIndex}, {Depth})";

    #region Arithmetic

    public static NodePosition operator +(NodePosition left, NodePosition right)
      => new NodePosition(left.SiblingIndex + right.SiblingIndex, left.Depth + right.Depth);

    public static NodePosition operator -(NodePosition left, NodePosition right)
      => new NodePosition(left.SiblingIndex - right.SiblingIndex, left.Depth - right.Depth);

    #endregion Arithmetic

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
