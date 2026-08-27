using Copse.Core;

namespace Copse.Linq
{
  // The dispatch builds' shared child-index (CSR over the preorder encoding): each parent's
  // children's preorder indices as one contiguous slice. Two O(n) hop passes and ~2n ints buy
  // the survey views (DispatchTargets, DispatchSources) their honestly-O(1) Count and indexer.
  // Shared by the sync builds and their async analogs (InternalsVisibleTo).
  internal static class DispatchChildIndex
  {
    internal static (int[] Offsets, int[] Indices) Build(int[] subtreeSizes)
    {
      var nodeCount = subtreeSizes.Length;

      var offsets = new int[nodeCount + 1];
      for (var parentIndex = 0; parentIndex < nodeCount; parentIndex++)
      {
        var hopEnd = parentIndex + subtreeSizes[parentIndex];
        for (var childIndex = parentIndex + 1; childIndex < hopEnd; childIndex += subtreeSizes[childIndex])
          offsets[parentIndex + 1]++;
      }

      for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        offsets[nodeIndex + 1] += offsets[nodeIndex];

      // Parents are filled in ascending order, which IS offset order, so one cursor suffices.
      var indices = new int[offsets[nodeCount]];
      var cursor = 0;
      for (var parentIndex = 0; parentIndex < nodeCount; parentIndex++)
      {
        var hopEnd = parentIndex + subtreeSizes[parentIndex];
        for (var childIndex = parentIndex + 1; childIndex < hopEnd; childIndex += subtreeSizes[childIndex])
          indices[cursor++] = childIndex;
      }

      return (offsets, indices);
    }

    // The positions-producing variant: the second hop pass
    // visits parents in ascending preorder, so every parent's position is already written when
    // its children fill in -- sibling index is the slice offset, depth is the parent's plus
    // one, roots seed at the whole-subtree hops. One exact-size array, no capture side channel,
    // no transients; the leaffix fold reads coordinates without carrying a walk. (The rootfix
    // pass derives coordinates statelessly instead -- its forward loop has the ancestor stack
    // for free; the leaffix fold runs in reverse, where no cheap stack exists, and a
    // close-stack walk was built and MEASURED OUT: O(depth) entries are O(n) on chains, which
    // cost more than this array buys back.)
    internal static (int[] Offsets, int[] Indices, NodePosition[] Positions) BuildWithPositions(int[] subtreeSizes)
    {
      var nodeCount = subtreeSizes.Length;

      var offsets = new int[nodeCount + 1];
      for (var parentIndex = 0; parentIndex < nodeCount; parentIndex++)
      {
        var hopEnd = parentIndex + subtreeSizes[parentIndex];
        for (var childIndex = parentIndex + 1; childIndex < hopEnd; childIndex += subtreeSizes[childIndex])
          offsets[parentIndex + 1]++;
      }

      for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        offsets[nodeIndex + 1] += offsets[nodeIndex];

      var positions = new NodePosition[nodeCount];
      var rootSibling = 0;
      for (var rootIndex = 0; rootIndex < nodeCount; rootIndex += subtreeSizes[rootIndex])
        positions[rootIndex] = new NodePosition(rootSibling++, 0);

      var indices = new int[offsets[nodeCount]];
      var cursor = 0;
      for (var parentIndex = 0; parentIndex < nodeCount; parentIndex++)
      {
        var childDepth = positions[parentIndex].Depth + 1;
        var siblingIndex = 0;
        var hopEnd = parentIndex + subtreeSizes[parentIndex];
        for (var childIndex = parentIndex + 1; childIndex < hopEnd; childIndex += subtreeSizes[childIndex])
        {
          indices[cursor++] = childIndex;
          positions[childIndex] = new NodePosition(siblingIndex++, childDepth);
        }
      }

      return (offsets, indices, positions);
    }
  }
}
