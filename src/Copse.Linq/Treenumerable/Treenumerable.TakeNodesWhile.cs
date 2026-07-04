using Copse.Core;
using Copse.Linq.Treenumerators;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<TNode> TakeNodesWhile<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => TreenumerableFactory.Create(
        () => new TakeNodesUntilTreenumerator<TNode>(
          source.GetBreadthFirstTreenumerator,
          nodeVisit => !predicate(nodeVisit),
          keepFinalNode),
        () => new TakeNodesUntilTreenumerator<TNode>(
          source.GetDepthFirstTreenumerator,
          nodeVisit => !predicate(nodeVisit),
          keepFinalNode));

    public static IDepthFirstTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => source.TakeNodesUntil(nodeVisit => !predicate(nodeVisit), keepFinalNode);

    public static IBreadthFirstTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => source.TakeNodesUntil(nodeVisit => !predicate(nodeVisit), keepFinalNode);
  }
}
