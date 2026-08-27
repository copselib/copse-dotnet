using Copse.Core;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The forest's root nodes, as a lazy async sequence. Drives with PruneSubtree so each
    /// root is scheduled once and its subtree skipped. Deferred sequence -&gt; keeps the sync name (returns
    /// <see cref="IAsyncEnumerable{TNode}"/>).
    /// </summary>
    public static async IAsyncEnumerable<TNode> GetRoots<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.PruneSubtree).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          yield return treenumerator.Node;
        }
      }
    }
  }
}
