using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree's leaf nodes in preorder, as a lazy async sequence (depth-first dimension).</summary>
    public static async IAsyncEnumerable<TNode> GetLeaves<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        if (!await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          yield break;

        TNode previousNode = treenumerator.Node;
        int previousDepth = treenumerator.Position.Depth;

        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (previousDepth >= treenumerator.Position.Depth)
            yield return previousNode;

          previousNode = treenumerator.Node;
          previousDepth = treenumerator.Position.Depth;
        }

        yield return previousNode;
      }
    }

    /// <summary>
    /// The tree's leaf nodes in LEVEL order (breadth-first dimension). "Leaf" is exactly "first visit
    /// not followed by a child schedule" -- one pending slot of state.
    /// </summary>
    public static async IAsyncEnumerable<TNode> GetLeaves<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        var hasPending = false;
        var pending = default(TNode);

        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
          {
            // The pending first-visited node just scheduled a child: not a leaf.
            hasPending = false;
          }
          else if (treenumerator.VisitCount == 1)
          {
            if (hasPending)
              yield return pending;

            pending = treenumerator.Node;
            hasPending = true;
          }
        }

        if (hasPending)
          yield return pending;
      }
    }

    /// <summary>
    /// With both narrow overloads present, a full <see cref="IAsyncTreenumerable{TNode}"/> needs its own
    /// overload to resolve (neither narrow one is better); it keeps the depth-first behavior.
    /// </summary>
    public static IAsyncEnumerable<TNode> GetLeaves<TNode>(this IAsyncTreenumerable<TNode> source, CancellationToken cancellationToken = default)
      => GetLeaves((IAsyncDepthFirstTreenumerable<TNode>)source, cancellationToken);
  }
}
