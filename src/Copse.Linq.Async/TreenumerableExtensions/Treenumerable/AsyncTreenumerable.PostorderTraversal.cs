using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree's node values in postorder (children before their parent), as a lazy async sequence.</summary>
    public static async IAsyncEnumerable<TNode> PostorderTraversal<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      if (source == null)
        yield break;

      var nodes = new RefSemiDeque<TNode>();

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          while (nodes.Count - 1 >= treenumerator.Position.Depth)
            yield return nodes.RemoveLast();

          nodes.AddLast(treenumerator.Node);
        }
      }

      while (nodes.Count > 0)
        yield return nodes.RemoveLast();
    }
  }
}
