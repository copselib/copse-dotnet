using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree's node values in level order (breadth-first), as a lazy async sequence.</summary>
    public static async IAsyncEnumerable<TNode> LevelOrderTraversal<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      if (source == null)
        yield break;

      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (treenumerator.VisitCount == 0)
            yield return treenumerator.Node;
        }
    }
  }
}
