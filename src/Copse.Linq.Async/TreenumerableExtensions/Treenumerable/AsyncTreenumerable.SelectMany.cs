using Copse.Linq.Treenumerators;
using Copse;
using Copse.Core;
using Copse.Linq;
using Copse.Linq.Treenumerables;
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

    /// <summary>
    /// The projection form: every node maps to a forest that replaces it, and the node's own
    /// children -- each projected the same way -- re-hang under the forest's last root, after
    /// that root's own children (<see cref="SlotPlacement.UnderLastRoot"/>: <c>Return</c>'s
    /// rule, the one placement under which <c>Return</c> is the monad's unit, so
    /// <c>nested.SelectMany(tree => tree)</c> is the monad's flatten). An empty forest
    /// promotes the children into the vacated position. Equivalent to the expansion form
    /// with <c>AsyncExpansion.Of(selector(node), SlotPlacement.UnderLastRoot)</c> -- the
    /// expansion form remains the door to the other placements and the slotless arms.
    /// Streams depth-first.
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TResult> SelectMany<TSource, TResult>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, IAsyncDepthFirstTreenumerable<TResult>> selector)
      => source.SelectMany(node => AsyncExpansion.Of(selector(node), SlotPlacement.UnderLastRoot));

    /// <summary>
    /// The projection form's composite: the depth-first dimension streams; the breadth-first
    /// dimension is a DOCUMENTED CAPTURE -- each breadth-first acquisition captures the
    /// depth-first result (preorder) and replays it breadth-first.
    /// </summary>
    public static IAsyncTreenumerable<TResult> SelectMany<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, IAsyncDepthFirstTreenumerable<TResult>> selector)
      => source.SelectMany(node => AsyncExpansion.Of(selector(node), SlotPlacement.UnderLastRoot));
  }
}
