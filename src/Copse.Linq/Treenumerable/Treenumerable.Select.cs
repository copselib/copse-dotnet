using Copse.Core;
using Copse.Linq.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<TResult> Select<TSource, TResult>(
      this ITreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
    {
      if (source is ISelectTreenumerable<TSource> innerSelectTreenumerable)
        return innerSelectTreenumerable.Compose(selector);
      else
        return new SelectTreenumerable<TSource, TResult>(source, selector);
    }

    public static IDepthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
      => TreenumerableFactory.CreateDepthFirst(
        () => new Treenumerators.SelectTreenumerator<TSource, TResult>(source.GetDepthFirstTreenumerator, selector));

    public static IBreadthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IBreadthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
      => TreenumerableFactory.CreateBreadthFirst(
        () => new Treenumerators.SelectTreenumerator<TSource, TResult>(source.GetBreadthFirstTreenumerator, selector));
  }
}
