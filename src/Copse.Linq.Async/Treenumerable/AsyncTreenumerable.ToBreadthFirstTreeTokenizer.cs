using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.TreeTokenizer.BreadthFirstTree;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The breadth-first token stream -- Node / FamilySeparator (between siblings of one parent) /
    /// GenerationSeparator (between levels) -- as a lazy async sequence. Separators are produced on
    /// the first visit of a node and flushed just before the next scheduled node.
    /// </summary>
    public static async IAsyncEnumerable<BreadthFirstTreeToken<TNode>> ToBreadthFirstTreeTokenizer<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      var currentLevelDepth = -1;
      var started = false;
      var separators = new Queue<BreadthFirstTreeToken<TNode>>();

      var t = source.GetAsyncBreadthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
      {
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (!started)
          {
            yield return new BreadthFirstTreeToken<TNode>(t.Node);
            started = true;
            continue;
          }

          if (t.Mode == TreenumeratorMode.SchedulingNode)
          {
            while (separators.Count > 0)
              yield return separators.Dequeue();

            yield return new BreadthFirstTreeToken<TNode>(t.Node);
          }
          else if (t.VisitCount == 1)
          {
            if (t.Position.Depth == currentLevelDepth)
            {
              separators.Enqueue(new BreadthFirstTreeToken<TNode>(BreadthFirstTreeTokenType.FamilySeparator));
            }
            else
            {
              separators.Enqueue(new BreadthFirstTreeToken<TNode>(BreadthFirstTreeTokenType.GenerationSeparator));
              currentLevelDepth++;
            }
          }
        }
      }

      // A trailing generation separator terminates the stream (its level never gets nodes).
      while (separators.Count > 0)
      {
        var separator = separators.Dequeue();
        yield return separator;
        if (separator.Type == BreadthFirstTreeTokenType.GenerationSeparator)
          break;
      }
    }
  }
}
