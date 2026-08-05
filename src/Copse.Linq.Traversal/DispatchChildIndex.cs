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
  }
}
