using Copse.Core;
using Copse.Linq.Treenumerators;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<TNode> Do<TNode>(
      this ITreenumerable<TNode> source,
      Action<NodeVisit<TNode>> onNext)
    {
      if (onNext == null)
        return source;

      return
        TreenumerableFactory
        .Create(
          () => new DoTreenumerator<TNode>(source.GetBreadthFirstTreenumerator, onNext),
          () => new DoTreenumerator<TNode>(source.GetDepthFirstTreenumerator, onNext));
    }

    public static IDepthFirstTreenumerable<TNode> Do<TNode>(
      this IDepthFirstTreenumerable<TNode> source,
      Action<NodeVisit<TNode>> onNext)
    {
      if (onNext == null)
        return source;

      return
        TreenumerableFactory.CreateDepthFirst(
          () => new DoTreenumerator<TNode>(source.GetDepthFirstTreenumerator, onNext));
    }

    public static IBreadthFirstTreenumerable<TNode> Do<TNode>(
      this IBreadthFirstTreenumerable<TNode> source,
      Action<NodeVisit<TNode>> onNext)
    {
      if (onNext == null)
        return source;

      return
        TreenumerableFactory.CreateBreadthFirst(
          () => new DoTreenumerator<TNode>(source.GetBreadthFirstTreenumerator, onNext));
    }
  }
}
