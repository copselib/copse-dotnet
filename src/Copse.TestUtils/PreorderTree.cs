using Copse.Core;
using Copse.Treenumerables;
using System.Collections.Generic;

namespace Copse.Treenumerables
{
  // A tree snapshot stored as flat pre-order arrays: node i's value is values[i], and its whole
  // subtree occupies the contiguous span [i, i + subtreeSizes[i]). DFS is a linear scan and
  // PruneDescendants is an O(1) span hop. It rides the existing DFS/BFS engine via
  // PreorderChildEnumerator -- no bespoke traversal code, dynamic pruning preserved.
  public sealed class PreorderTree<TNode> : ITreenumerable<TNode>
  {
    private readonly ITreenumerable<TNode> _Tree;

    public PreorderTree(TNode[] values, int[] subtreeSizes)
    {
      // Constructed DIRECTLY, not through the Tree.Create door: this is the engine ORACLE --
      // if a door ever rerouted to different machinery, an oracle built through it would
      // silently stop pinning the engine.
      _Tree = new HierarchicalTreenumerable<TNode, int, PreorderChildEnumerator>(
        nodeAndPosition => new PreorderChildEnumerator(subtreeSizes, nodeAndPosition.Node),
        index => values[index],
        RootIndices(subtreeSizes));
    }

    public ITreenumerator<TNode> GetDepthFirstTreenumerator() => _Tree.GetDepthFirstTreenumerator();

    public ITreenumerator<TNode> GetBreadthFirstTreenumerator() => _Tree.GetBreadthFirstTreenumerator();

    // Roots are the top-level spans: index 0, then hop by each root's subtree size.
    private static IEnumerable<int> RootIndices(int[] subtreeSizes)
    {
      for (int i = 0; i < subtreeSizes.Length; i += subtreeSizes[i])
        yield return i;
    }
  }
}
