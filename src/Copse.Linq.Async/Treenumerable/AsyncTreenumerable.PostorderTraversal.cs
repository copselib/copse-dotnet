using Copse;
using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree's node values in postorder (children before their parent), as a lazy async sequence.</summary>
    public static async IAsyncEnumerable<TNode> PostorderTraversal<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
    {
      if (source == null)
        yield break;

      var nodes = new RefSemiDeque<TNode>();
      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
      {
        while (await t.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
        {
          while (nodes.Count - 1 >= t.Position.Depth)
            yield return nodes.RemoveLast();
          nodes.AddLast(t.Node);
        }
      }

      while (nodes.Count > 0)
        yield return nodes.RemoveLast();
    }
  }
}
