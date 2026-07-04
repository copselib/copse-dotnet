using Copse.Core;
using Copse.Linq.Treenumerators;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<TNode> TakeNodesUntil<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => TreenumerableFactory.Create(
        () => new TakeNodesUntilTreenumerator<TNode>(
          source.GetBreadthFirstTreenumerator,
          predicate,
          keepFinalNode),
        () => new TakeNodesUntilTreenumerator<TNode>(
          source.GetDepthFirstTreenumerator,
          predicate,
          keepFinalNode));

    public static IDepthFirstTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => TreenumerableFactory.CreateDepthFirst(
        () => new TakeNodesUntilTreenumerator<TNode>(
          source.GetDepthFirstTreenumerator,
          predicate,
          keepFinalNode));

    public static IBreadthFirstTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => TreenumerableFactory.CreateBreadthFirst(
        () => new TakeNodesUntilTreenumerator<TNode>(
          source.GetBreadthFirstTreenumerator,
          predicate,
          keepFinalNode));
  }
}
