using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree's nodes grouped by depth (one array per level), as a lazy async sequence.</summary>
    public static async IAsyncEnumerable<TNode[]> GetLevels<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      var depth = 0;
      var deque = new RefSemiDeque<TNode>();

      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          if (treenumerator.Position.Depth != depth)
          {
            depth++;
            yield return CopyDequeToArray(deque);
          }

          deque.AddLast(treenumerator.Node);
        }

        if (deque.Count > 0)
          yield return CopyDequeToArray(deque);
      }

      TNode[] CopyDequeToArray(RefSemiDeque<TNode> localDeque)
      {
        var result = new TNode[deque.Count];

        for (int i = deque.Count - 1; i >= 0; i--)
          result[i] = deque.RemoveLast();

        return result;
      }
    }
  }
}
