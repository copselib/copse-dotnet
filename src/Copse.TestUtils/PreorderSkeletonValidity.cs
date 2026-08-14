using System;

namespace Copse.TestUtils
{
  /// <summary>
  /// The preorder skeleton's validity predicate, as code (the foundation restatement,
  /// 2026-08-14): a subtree-sizes array is a legal preorder encoding iff every size is at
  /// least 1 and no node's span crosses its enclosing span's end. Because preorder indices
  /// are consecutive, those two facts imply the rest -- children start at parent + 1, each
  /// next child starts exactly where the previous span ended, so sibling spans PARTITION
  /// the parent's interior and root spans partition [0, n).
  ///
  /// <para>This is the skeleton representation's half of the conditional-laws bargain: the
  /// comonad and monad laws hold PER REPRESENTATION, conditional on that representation's
  /// validity -- probes are bound by the adjacency protocol (the conformance battery),
  /// skeletons by this arithmetic. The battery checks the family's encodings; providers own
  /// theirs, and this checker is the definition of what they owe.</para>
  /// </summary>
  public static class PreorderSkeletonValidity
  {
    /// <summary>Throws with a coordinate-bearing message on the first violation.</summary>
    public static void AssertValid(int nodeCount, Func<int, int> subtreeSizeAt)
    {
      // The open-span stack: ends of every span the cursor is currently inside.
      var openSpanEnds = new System.Collections.Generic.Stack<int>();

      for (var index = 0; index < nodeCount; index++)
      {
        while (openSpanEnds.Count > 0 && openSpanEnds.Peek() == index)
          openSpanEnds.Pop();

        var size = subtreeSizeAt(index);

        if (size < 1)
          throw new InvalidOperationException($"Skeleton invalid: subtree size {size} at index {index} (every size must be >= 1).");

        var spanEnd = index + size;
        var enclosingEnd = openSpanEnds.Count > 0 ? openSpanEnds.Peek() : nodeCount;

        if (spanEnd > enclosingEnd)
          throw new InvalidOperationException($"Skeleton invalid: span [{index}, {spanEnd}) at index {index} crosses its enclosing span's end {enclosingEnd}.");

        openSpanEnds.Push(spanEnd);
      }
    }

    public static void AssertValid(int[] subtreeSizes)
      => AssertValid(subtreeSizes.Length, index => subtreeSizes[index]);
  }
}
