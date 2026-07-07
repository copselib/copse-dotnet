using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree's leaf nodes in preorder, as a lazy async sequence (depth-first dimension).</summary>
    public static async IAsyncEnumerable<TNode> GetLeaves<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
    {
      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
      {
        if (!await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          yield break;

        var previousNode = t.Node;
        var previousDepth = t.Position.Depth;

        while (await t.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
        {
          if (previousDepth >= t.Position.Depth)
            yield return previousNode;

          previousNode = t.Node;
          previousDepth = t.Position.Depth;
        }

        yield return previousNode;
      }
    }

    /// <summary>
    /// The tree's leaf nodes in LEVEL order (breadth-first dimension). "Leaf" is exactly "first visit
    /// not followed by a child schedule" -- one pending slot of state.
    /// </summary>
    public static async IAsyncEnumerable<TNode> GetLeaves<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      var t = source.GetAsyncBreadthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
      {
        var hasPending = false;
        var pending = default(TNode);

        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (t.Mode == TreenumeratorMode.SchedulingNode)
          {
            // The pending first-visited node just scheduled a child: not a leaf.
            hasPending = false;
          }
          else if (t.VisitCount == 1)
          {
            if (hasPending)
              yield return pending;

            pending = t.Node;
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
    public static IAsyncEnumerable<TNode> GetLeaves<TNode>(this IAsyncTreenumerable<TNode> source)
      => GetLeaves((IAsyncDepthFirstTreenumerable<TNode>)source);
  }
}
