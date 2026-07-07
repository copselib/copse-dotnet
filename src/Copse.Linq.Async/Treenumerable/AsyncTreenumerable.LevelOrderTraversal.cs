using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree's node values in level order (breadth-first), as a lazy async sequence.</summary>
    public static async IAsyncEnumerable<TNode> LevelOrderTraversal<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      if (source == null)
        yield break;

      var t = source.GetAsyncBreadthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          if (t.VisitCount == 0)
            yield return t.Node;
    }
  }
}
