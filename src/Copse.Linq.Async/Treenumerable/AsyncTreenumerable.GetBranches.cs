using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
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

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        if (!await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          yield break;

        branch.Add(treenumerator.ToNodeContext());

        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          var depth = treenumerator.Position.Depth;

          if (depth > branch.Count - 1)
          {
            branch.Add(treenumerator.ToNodeContext());
          }
          else
          {
            yield return branch.Select(nodeContext => nodeContext.Node).ToArray();

            branch.RemoveRange(depth, branch.Count - depth);
            branch.Add(treenumerator.ToNodeContext());
          }
        }

        if (branch.Count > 0)
          yield return branch.Select(nodeContext => nodeContext.Node).ToArray();
      }
    }
  }
}
