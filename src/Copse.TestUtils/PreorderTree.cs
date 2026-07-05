using System.Collections.Generic;

namespace Copse.Treenumerables
{
  // A tree snapshot stored as flat pre-order arrays: node i's value is values[i], and its whole
  // subtree occupies the contiguous span [i, i + subtreeSizes[i]). DFS is a linear scan and
  // SkipDescendants is an O(1) span hop. It rides the existing DFS/BFS engine via
  // PreorderChildEnumerator -- no bespoke traversal code, dynamic pruning preserved.
  public sealed class PreorderTree<TValue>
    : Treenumerable<TValue, int, PreorderChildEnumerator>
  {
    public PreorderTree(TValue[] values, int[] subtreeSizes)
      : base(
          nodeContext => new PreorderChildEnumerator(subtreeSizes, nodeContext.Node),
          index => values[index],
          RootIndices(subtreeSizes))
    {
    }

    // Roots are the top-level spans: index 0, then hop by each root's subtree size.
    private static IEnumerable<int> RootIndices(int[] subtreeSizes)
    {
      for (int i = 0; i < subtreeSizes.Length; i += subtreeSizes[i])
        yield return i;
    }
  }
}
