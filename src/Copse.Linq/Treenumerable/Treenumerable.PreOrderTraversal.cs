using Copse.Core;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static IEnumerable<TNode> PreOrderTraversal<TNode>(this IDepthFirstTreenumerable<TNode> source)
    {
      if (source == null)
        yield break;

      using (var treenumerator = source.GetDepthFirstTreenumerator())
        while (treenumerator.MoveNext(NodeTraversalStrategies.SkipNode))
          yield return treenumerator.Node;
    }
  }
}
