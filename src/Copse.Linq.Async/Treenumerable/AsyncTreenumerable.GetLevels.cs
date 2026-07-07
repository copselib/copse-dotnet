using Copse;
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
      var t = source.GetAsyncBreadthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
      {
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (t.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          if (t.Position.Depth != depth)
          {
            depth++;
            yield return CopyDequeToArray(deque);
          }

          deque.AddLast(t.Node);
        }

        if (deque.Count > 0)
          yield return CopyDequeToArray(deque);
      }

      TNode[] CopyDequeToArray(RefSemiDeque<TNode> localDeque)
      {
        var result = new TNode[localDeque.Count];

        for (int i = localDeque.Count - 1; i >= 0; i--)
          result[i] = localDeque.RemoveLast();

        return result;
      }
    }
  }
}
