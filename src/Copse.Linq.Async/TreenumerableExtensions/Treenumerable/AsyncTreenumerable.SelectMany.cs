using Copse.Async;
using Copse.Core.Async;
using Copse.Linq.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The tree monad's bind: every node is replaced in place by its expansion's forest, and
    /// the node's own children -- each replaced the same way -- re-hang at the expansion's
    /// slot (see <see cref="AsyncExpansion{TResult}"/> for the placements and the four
    /// special values: <c>Return</c> is Select's unit, <c>Promote</c> is Where's drop arm,
    /// <c>Drop</c> is PruneSubtreesWhere's, <c>Leaf</c> is PruneDescendantsWhere's). Streams depth-first:
    /// nothing is pulled ahead of its emission, and a dropped subtree is never pulled.
    /// Laws and semantics: design-docs/SELECTMANY_DESIGN.md.
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TResult> SelectMany<TSource, TResult>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, AsyncExpansion<TResult>> selector)
      => AsyncTree.CreateDepthFirst(
        () => new AsyncSelectManyDepthFirstTreenumerator<TSource, TResult>(source.GetAsyncDepthFirstTreenumerator, selector));

    /// <summary>
    /// The composite form. The depth-first dimension streams; the breadth-first dimension
    /// is a DOCUMENTED CAPTURE -- each breadth-first acquisition captures the depth-first
    /// result (preorder) and replays it breadth-first.
    /// </summary>
    public static IAsyncTreenumerable<TResult> SelectMany<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, AsyncExpansion<TResult>> selector)
    {
      var depthFirst = ((IAsyncDepthFirstTreenumerable<TSource>)source).SelectMany(selector);

      return AsyncTree.Create(
        () => depthFirst.Materialize().GetAsyncBreadthFirstTreenumerator(),
        depthFirst.GetAsyncDepthFirstTreenumerator);
    }
  }
}
