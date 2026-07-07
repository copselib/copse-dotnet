using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree's root-to-leaf paths (each as a node array), as a lazy async sequence.</summary>
    public static async IAsyncEnumerable<TNode[]> GetBranches<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
    {
      var branch = new List<NodeContext<TNode>>();
      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
      {
        if (!await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          yield break;

        branch.Add(new NodeContext<TNode>(t.Node, t.Position));

        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (t.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          var depth = t.Position.Depth;

          if (depth > branch.Count - 1)
          {
            branch.Add(new NodeContext<TNode>(t.Node, t.Position));
          }
          else
          {
            yield return branch.Select(nodeContext => nodeContext.Node).ToArray();

            branch.RemoveRange(depth, branch.Count - depth);
            branch.Add(new NodeContext<TNode>(t.Node, t.Position));
          }
        }

        if (branch.Count > 0)
          yield return branch.Select(nodeContext => nodeContext.Node).ToArray();
      }
    }
  }
}
