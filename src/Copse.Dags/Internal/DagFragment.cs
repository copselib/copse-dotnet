namespace Copse.Dags
{
  // The one fact both replacement shapes (DagExpansion, DagNodeGraph) derive from their forward
  // edges: which fragment nodes have no internal in-edge -- the sources the original's in-edges
  // reach -- in fragment order.
  internal static class DagFragment
  {
    public static int[] SourceIndices<TEdge>(int nodeCount, (int From, int To, TEdge Edge)[] edges)
    {
      if (edges == null)
      {
        var all = new int[nodeCount];
        for (var index = 0; index < all.Length; index++)
          all[index] = index;
        return all;
      }

      var hasInternalIn = new bool[nodeCount];
      foreach (var edge in edges)
        hasInternalIn[edge.To] = true;

      var count = 0;
      for (var index = 0; index < nodeCount; index++)
        if (!hasInternalIn[index])
          count++;

      var sources = new int[count];
      var fill = 0;
      for (var index = 0; index < nodeCount; index++)
        if (!hasInternalIn[index])
          sources[fill++] = index;

      return sources;
    }
  }
}
