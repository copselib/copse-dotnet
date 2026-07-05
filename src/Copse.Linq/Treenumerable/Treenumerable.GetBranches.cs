using Copse.Core;
using Copse.Linq.Extensions;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static IEnumerable<TNode[]> GetBranches<TNode>(this IDepthFirstTreenumerable<TNode> source)
    {
      var branch = new List<NodeContext<TNode>>();

      using (var treenumerator = source.GetDepthFirstTreenumerator())
      {
        if (!treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          yield break;

        branch.Add(treenumerator.ToNodeContext());

        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
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
