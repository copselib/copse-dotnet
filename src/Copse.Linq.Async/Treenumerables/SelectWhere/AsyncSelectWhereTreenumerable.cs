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
      _Relabels = relabels;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;
    private readonly TResultSelector _ResultSelector;

    // PRIVATE (the door move): monotone -- the OR of every operator that has joined this
    // chain. PROVEN ALWAYS TRUE for this class (a probe throwing on relabels=false ran the
    // full 24,596-test battery clean): a driver exists only because Where or PruneBefore
    // built it, and both relabel by nature. The field survives to feed successors.
    private readonly bool _Relabels;

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncWhereBreadthFirstTreenumerator<TSource, TResult, TResultSelector>(
        _Source.GetAsyncBreadthFirstTreenumerator, _ResultSelector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncWhereDepthFirstTreenumerator<TSource, TResult, TResultSelector>(
        _Source.GetAsyncDepthFirstTreenumerator, _ResultSelector);

    // The composition law under this representation's successor choice: the general
    // wrapper composes to a general wrapper, and the chain nests in the type -- every leg
    // inlinable.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<TResult, TOuterResult>
    {
      return new AsyncSelectWhereTreenumerable<TSource, TOuterResult, ComposedResultSelector<TSource, TResult, TOuterResult, TResultSelector, TOuterSelector>>(
        _Source,
        new ComposedResultSelector<TSource, TResult, TOuterResult, TResultSelector, TOuterSelector>(_ResultSelector, outerSelector),
        _Relabels | relabels);
    }

    // The context-shaped projection door: the projection rides an inlinable struct leg
    // (the caller has already applied the join rule for positional legs).
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
      => Compose<TOuterResult, SelectResultSelector<TResult, TOuterResult>>(
        new SelectResultSelector<TResult, TOuterResult>(selector), relabels: false);

    // ---- The position-reading doors: this driver STACKS ----
    //
    // A spliced leg is evaluated against this driver's INNER coordinates, which are not the
    // ones it publishes -- promotion has moved them. So a position-reading leg goes into a
    // wrapper over this driver, where it reads published labels by construction. No flag is
    // consulted: a driver exists because something rejected, and rejection is what moves
    // labels (see _Relabels, proven always true).
    public IAsyncTreenumerable<TOuterResult> ComposePositional<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
      => new AsyncSelectTreenumerable<TResult, TOuterResult>(this, selector);

    public IAsyncTreenumerable<TOuterResult> ComposePositional<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<TResult, TOuterResult>
      => new AsyncSelectWhereTreenumerable<TResult, TOuterResult, TOuterSelector>(this, outerSelector, relabels);

    // The context-shaped prune-after door: the in-tier-only boundary ruling (2026-08-04,
    // the surviving half) -- the light prune wrapper STACKS over the driver rather than
    // demoting its representation for a layer that costs almost nothing.
    public IAsyncTreenumerable<TResult> ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
      => new AsyncPruneAfterTreenumerable<TResult>(this, predicate);
  }
}
