using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree's node values in preorder (depth-first schedule order), as a lazy async sequence.</summary>
    public static async IAsyncEnumerable<TNode> PreorderTraversal<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
    {
      if (source == null)
        yield break;

      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
          yield return t.Node;
    }
  }
}
