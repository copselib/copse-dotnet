using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.TreeTokenizer.DepthFirstTree;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The depth-first token stream -- Node / StartChildGroup / EndChildGroup -- as a lazy async
    /// sequence. Depth increases by one per scheduled child (a StartChildGroup); a decrease of k
    /// closes k child groups; the tail closes back to the roots.
    /// </summary>
    public static async IAsyncEnumerable<DepthFirstTreeToken<TNode>> ToDepthFirstTreeTokenizer<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source)
    {
      var previousDepth = 0;
      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
      {
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (t.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          var depth = t.Position.Depth;

          if (depth != previousDepth)
          {
            if (depth > previousDepth)
              yield return new DepthFirstTreeToken<TNode>(DepthFirstTreeTokenType.StartChildGroup);
            else
              for (var i = 0; i < previousDepth - depth; i++)
                yield return new DepthFirstTreeToken<TNode>(DepthFirstTreeTokenType.EndChildGroup);

            previousDepth = depth;
          }

          yield return new DepthFirstTreeToken<TNode>(t.Node);
        }
      }

      for (var i = 0; i < previousDepth; i++)
        yield return new DepthFirstTreeToken<TNode>(DepthFirstTreeTokenType.EndChildGroup);
    }
  }
}
