using Copse.Async;
using Copse.Core;
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
    /// <c>Drop</c> is PruneBefore's, <c>Leaf</c> is PruneAfter's). Streams depth-first:
    /// nothing is pulled ahead of its emission, and a dropped subtree is never pulled.
    /// Laws and semantics: design-docs/SELECTMANY_DESIGN.md.
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TResult> SelectMany<TSource, TResult>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, AsyncExpansion<TResult>> selector)
      => AsyncTree.CreateDepthFirst(
        () => new AsyncSelectManyDepthFirstTreenumerator<TSource, TResult, AsyncFuncExpansionSelector<TSource, TResult>>(
          source.GetAsyncDepthFirstTreenumerator,
          new AsyncFuncExpansionSelector<TSource, TResult>(selector)));

    /// <summary>
    /// The composite form. The depth-first dimension streams; the breadth-first dimension
    /// is a DOCUMENTED CAPTURE -- each breadth-first acquisition captures the depth-first
    /// result (preorder) and replays it breadth-first.
    /// </summary>
    public static IAsyncTreenumerable<TResult> SelectMany<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, AsyncExpansion<TResult>> selector)
    {
      // The left door (SELECTMANY_DESIGN.md Addendum V): a collapsed chain, a prune-after
      // layer, or a projection wrapper surrenders its raw inner and its arrow, and ONE bind
      // machine runs over the inner with the arrow folded ahead of the selector -- the four
      // reshapings are bind's own special values, so the fold is pointwise. The consumer
      // recurses: if the surrendered inner can surrender too, the arrows compose (the
      // lattice's own Kleisli composition) and the fold continues down to the raw source.
      if (source is IAsyncResultSource<TSource> resultSource)
        return resultSource.CaptureThrough(new AsyncBindConsumer<TSource, TResult>(selector));

      if (source is IAsyncProjectionSource<TSource> projectionSource)
        return projectionSource.CaptureThrough(new AsyncBindConsumer<TSource, TResult>(selector));

      return new AsyncSelectManyTreenumerable<TSource, TResult, AsyncFuncExpansionSelector<TSource, TResult>>(
        source.GetAsyncDepthFirstTreenumerator,
        new AsyncFuncExpansionSelector<TSource, TResult>(selector));
    }

    // The fold's step: an arrow into the selector's domain over some inner source. Recurse
    // while the inner can surrender; otherwise the bind over the inner with the arrow nested
    // in the type ahead of the selector.
    private static IAsyncTreenumerable<TResult> BindThroughArrow<TInner, TMid, TResult, TArrow>(
      IAsyncTreenumerable<TInner> innerSource,
      TArrow arrow,
      Func<TMid, AsyncExpansion<TResult>> selector)
      where TArrow : struct, IResultSelector<TInner, TMid>
    {
      if (innerSource is IAsyncResultSource<TInner> resultSource)
        return resultSource.CaptureThrough(new AsyncBindComposingConsumer<TInner, TMid, TResult, TArrow>(arrow, selector));

      if (innerSource is IAsyncProjectionSource<TInner> projectionSource)
        return projectionSource.CaptureThrough(new AsyncBindComposingConsumer<TInner, TMid, TResult, TArrow>(arrow, selector));

      return new AsyncSelectManyTreenumerable<TInner, TResult, AsyncFoldedExpansionSelector<TInner, TMid, TResult, TArrow>>(
        innerSource.GetAsyncDepthFirstTreenumerator,
        new AsyncFoldedExpansionSelector<TInner, TMid, TResult, TArrow>(arrow, selector));
    }

    // The first hop: the source's own arrow, nothing to compose with yet.
    private sealed class AsyncBindConsumer<TMid, TResult>
      : IAsyncResultConsumer<TMid, IAsyncTreenumerable<TResult>>,
        IAsyncProjectionConsumer<TMid, IAsyncTreenumerable<TResult>>
    {
      public AsyncBindConsumer(Func<TMid, AsyncExpansion<TResult>> selector)
      {
        _Selector = selector;
      }

      private readonly Func<TMid, AsyncExpansion<TResult>> _Selector;

      public IAsyncTreenumerable<TResult> Consume<TInner, TArrow>(IAsyncTreenumerable<TInner> innerSource, TArrow arrow)
        where TArrow : struct, IResultSelector<TInner, TMid>
        => BindThroughArrow<TInner, TMid, TResult, TArrow>(innerSource, arrow, _Selector);

      public IAsyncTreenumerable<TResult> Consume<TInner>(IAsyncTreenumerable<TInner> innerSource, Func<NodeContext<TInner>, TMid> projector)
        => BindThroughArrow<TInner, TMid, TResult, SelectResultSelector<TInner, TMid>>(
          innerSource, new SelectResultSelector<TInner, TMid>(projector), _Selector);
    }

    // Every later hop: the surrendered arrow composes INSIDE the one carried so far.
    private sealed class AsyncBindComposingConsumer<TOuterInner, TMid, TResult, TOuterArrow>
      : IAsyncResultConsumer<TOuterInner, IAsyncTreenumerable<TResult>>,
        IAsyncProjectionConsumer<TOuterInner, IAsyncTreenumerable<TResult>>
      where TOuterArrow : struct, IResultSelector<TOuterInner, TMid>
    {
      public AsyncBindComposingConsumer(TOuterArrow outerArrow, Func<TMid, AsyncExpansion<TResult>> selector)
      {
        _OuterArrow = outerArrow;
        _Selector = selector;
      }

      private readonly TOuterArrow _OuterArrow;
      private readonly Func<TMid, AsyncExpansion<TResult>> _Selector;

      public IAsyncTreenumerable<TResult> Consume<TInner, TArrow>(IAsyncTreenumerable<TInner> innerSource, TArrow arrow)
        where TArrow : struct, IResultSelector<TInner, TOuterInner>
        => BindThroughArrow<TInner, TMid, TResult, ComposedResultSelector<TInner, TOuterInner, TMid, TArrow, TOuterArrow>>(
          innerSource,
          new ComposedResultSelector<TInner, TOuterInner, TMid, TArrow, TOuterArrow>(arrow, _OuterArrow),
          _Selector);

      public IAsyncTreenumerable<TResult> Consume<TInner>(IAsyncTreenumerable<TInner> innerSource, Func<NodeContext<TInner>, TOuterInner> projector)
        => BindThroughArrow<TInner, TMid, TResult, ComposedResultSelector<TInner, TOuterInner, TMid, SelectResultSelector<TInner, TOuterInner>, TOuterArrow>>(
          innerSource,
          new ComposedResultSelector<TInner, TOuterInner, TMid, SelectResultSelector<TInner, TOuterInner>, TOuterArrow>(
            new SelectResultSelector<TInner, TOuterInner>(projector), _OuterArrow),
          _Selector);
    }
  }
}
