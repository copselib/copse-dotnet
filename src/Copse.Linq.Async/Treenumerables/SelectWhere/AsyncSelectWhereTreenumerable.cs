using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The reified operator chain (design-docs/OPERATOR_COMPOSITION_DESIGN.md, "the result monad"): one wrapper
  // holding the Kleisli-composed result of every composed operator, so chains of any
  // length and order collapse to ONE layer over the source. Plain operators
  // instantiate with their bespoke selector STRUCT (inlined by the JIT -- zero seam cost);
  // composed chains nest those structs in the TYPE via ComposedResultSelector (a user
  // delegate enters only as a FuncResultSelector leaf). Splicing is total: every legality
  // decision was made outer-side.
  internal sealed partial class AsyncSelectWhereTreenumerable<TSource, TResult, TResultSelector> : IAsyncSelectWhereTreenumerable<TResult>
    where TResultSelector : struct, IResultSelector<TSource, TResult>
  {
    public AsyncSelectWhereTreenumerable(
      IAsyncTreenumerable<TSource> source,
      TResultSelector resultSelector,
      bool relabels)
    {
      _Source = source;
      _ResultSelector = resultSelector;
      Relabels = relabels;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;
    private readonly TResultSelector _ResultSelector;

    public bool Relabels { get; }

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncWhereBreadthFirstTreenumerator<TSource, TResult, TResultSelector>(
        _Source.GetAsyncBreadthFirstTreenumerator, _ResultSelector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncWhereDepthFirstTreenumerator<TSource, TResult, TResultSelector>(
        _Source.GetAsyncDepthFirstTreenumerator, _ResultSelector);

    // The composition law (SelectWhereComposition, the algebra's one home) under this
    // representation's successor choice: the general wrapper composes to a general wrapper.
    // The Func form is the struct form with the closure as its one leaf: no closure route,
    // no second home for the law (the closure-arrow spelling was deleted when the simplify
    // pass found every Func door reducible to this forward).
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
      => Compose<TOuterResult, FuncResultSelector<TResult, TOuterResult>>(
        new FuncResultSelector<TResult, TOuterResult>(resultSelector), relabels);

    // The struct-composed successor: the chain nests in the type, every leg inlinable.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<TResult, TOuterResult>
    {
      return new AsyncSelectWhereTreenumerable<TSource, TOuterResult, ComposedResultSelector<TSource, TResult, TOuterResult, TResultSelector, TOuterSelector>>(
        _Source,
        new ComposedResultSelector<TSource, TResult, TOuterResult, TResultSelector, TOuterSelector>(_ResultSelector, outerSelector),
        Relabels | relabels);
    }

    // The context-shaped projection door: the projection rides an inlinable struct leg
    // (the caller has already applied the join rule for positional legs).
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
      => Compose<TOuterResult, SelectResultSelector<TResult, TOuterResult>>(
        new SelectResultSelector<TResult, TOuterResult>(selector), relabels: false);

    // The context-shaped prune-after door: the in-tier-only boundary ruling (2026-08-04,
    // the surviving half) -- the light prune wrapper STACKS over the driver rather than
    // demoting its representation for a layer that costs almost nothing.
    public IAsyncTreenumerable<TResult> ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
      => new AsyncPruneAfterTreenumerable<TResult>(this, predicate);
  }
}
