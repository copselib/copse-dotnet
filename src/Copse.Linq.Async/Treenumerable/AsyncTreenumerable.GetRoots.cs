using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The forest's root nodes, as a lazy async sequence. Drives with SkipNodeAndDescendants so each
    /// root is scheduled once and its subtree skipped. Deferred sequence -&gt; keeps the sync name (returns
    /// <see cref="IAsyncEnumerable{TNode}"/>, the async analog of the sync <c>IEnumerable</c> result).
    /// </summary>
    public static async IAsyncEnumerable<TNode> GetRoots<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
    {
      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNodeAndDescendants).ConfigureAwait(false))
          yield return treenumerator.Node;
      }
    }
  }
}
