using Copse.Core;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static IEnumerable<TNode> GetLeaves<TNode>(this IDepthFirstTreenumerable<TNode> source)
    {
      using (var treenumerator = source.GetDepthFirstTreenumerator())
      {
        if (!treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          yield break;

        TNode previousNode = treenumerator.Node;
        int previousDepth = treenumerator.Position.Depth;

        while (treenumerator.MoveNext(NodeTraversalStrategies.SkipNode))
        {
          if (previousDepth >= treenumerator.Position.Depth)
            yield return previousNode;

          previousNode = treenumerator.Node;
          previousDepth = treenumerator.Position.Depth;
        }

        yield return previousNode;
      }
    }
  }
}
