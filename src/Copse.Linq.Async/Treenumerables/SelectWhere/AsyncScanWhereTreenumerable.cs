using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // THE FOURTH CELL's wrapper (the ancestor composer, SCAN_TIER_DESIGN.md): a rootfix fold
  // plus a composed selector chain over the PAIR (node, accumulate), as ONE machine. The
  // recipe is (inner factory pair, accumulator, seed, resultSelector); the selector algebra is the
  // lattice's own -- the legs compose over the pair source type exactly as they compose
  // over any source type, so this wrapper joins the general surface with the standard
  // struct Compose forms and every downstream Select/Where lands in ITS legs, not in a
  // stacked layer.
  //
  // Both dimensions are staged machines: DFT rides the accumulate trail
  // (AsyncScanWhereDepthFirstTreenumerator), BFT rides the accumulate tracker -- the
  // rootfix BFT engine's state machinery embedded beside the filter path
  // (AsyncScanWhereBreadthFirstTreenumerator). ONE machine per acquisition, both dims.
  internal sealed class AsyncScanWhereTreenumerable<TSource, TAccumulate, TResult, TResultSelector>
    : IAsyncSelectWhereTreenumerable<TResult>
    where TResultSelector : struct, IResultSelector<NodeAccumulation<TSource, TAccumulate>, TResult>
  {
    public AsyncScanWhereTreenumerable(
      Func<IAsyncTreenumerator<TSource>> innerDepthFirstFactory,
      Func<IAsyncTreenumerator<TSource>> innerBreadthFirstFactory,
      Func<NodeContext<TAccumulate>, NodeContext<TSource>, TAccumulate> accumulator,
      TAccumulate seed,
      TResultSelector resultSelector,
      bool relabels)
    {
      _InnerDepthFirstFactory = innerDepthFirstFactory;
      _InnerBreadthFirstFactory = innerBreadthFirstFactory;
      _Accumulator = accumulator;
      _Seed = seed;
      _ResultSelector = resultSelector;
      _Relabels = relabels;
    }

    private readonly Func<IAsyncTreenumerator<TSource>> _InnerDepthFirstFactory;
    private readonly Func<IAsyncTreenumerator<TSource>> _InnerBreadthFirstFactory;
    private readonly Func<NodeContext<TAccumulate>, NodeContext<TSource>, TAccumulate> _Accumulator;
    private readonly TAccumulate _Seed;
    private readonly TResultSelector _ResultSelector;

    // PRIVATE, and genuinely dynamic here: a rootfix citizen's blind door builds a
    // fold-carrying driver with relabels FALSE (a scan where nothing rejects moves no
    // labels), so unlike the general driver this class must actually ask itself.
    private readonly bool _Relabels;

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator()
      => new AsyncScanWhereDepthFirstTreenumerator<TSource, TAccumulate, TResult, TResultSelector>(
        _InnerDepthFirstFactory, _Accumulator, _Seed, _ResultSelector);

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator()
      => new AsyncScanWhereBreadthFirstTreenumerator<TSource, TAccumulate, TResult, TResultSelector>(
        _InnerBreadthFirstFactory, _Accumulator, _Seed, _ResultSelector);

    // The composition law under this representation: successors keep the fold and nest the
    // outer leg onto the selector chain -- the chain composes in the TYPE, over the pair.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<TResult, TOuterResult>
    {
      return new AsyncScanWhereTreenumerable<TSource, TAccumulate, TOuterResult, ComposedResultSelector<NodeAccumulation<TSource, TAccumulate>, TResult, TOuterResult, TResultSelector, TOuterSelector>>(
        _InnerDepthFirstFactory,
        _InnerBreadthFirstFactory,
        _Accumulator,
        _Seed,
        new ComposedResultSelector<NodeAccumulation<TSource, TAccumulate>, TResult, TOuterResult, TResultSelector, TOuterSelector>(_ResultSelector, outerSelector),
        _Relabels | relabels);
    }

    // The context-shaped projection door: the projection nests as a struct leg onto the
    // selector chain, over the pair (this machine is not in the narrow fan-out, so all
    // four doors live in this file).
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
      => Compose<TOuterResult, SelectResultSelector<TResult, TOuterResult>>(
        new SelectResultSelector<TResult, TOuterResult>(selector), relabels: false);

    // The position-reading doors: this machine inherits relabeling from whatever joined it,
    // so it answers from its own flag -- splice while nothing here moves a label, otherwise
    // stack so the leg reads published labels.
    public IAsyncTreenumerable<TOuterResult> ComposePositional<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
      => _Relabels
        ? new AsyncSelectTreenumerable<TResult, TOuterResult>(this, selector)
        : Compose(selector);

    public IAsyncTreenumerable<TOuterResult> ComposePositional<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<TResult, TOuterResult>
      => _Relabels
        ? new AsyncSelectWhereTreenumerable<TResult, TOuterResult, TOuterSelector>(this, outerSelector, relabels)
        : Compose<TOuterResult, TOuterSelector>(outerSelector, relabels);

    // The public projection door: the same leg, value-flavored, returning the composed
    // fold-carrying machine (which is itself a citizen through the general surface).
    public IAsyncSelectTreenumerable<TOuterResult> ComposeSelect<TOuterResult>(Func<TResult, TOuterResult> selector)
    {
      return new AsyncScanWhereTreenumerable<TSource, TAccumulate, TOuterResult, ComposedResultSelector<NodeAccumulation<TSource, TAccumulate>, TResult, TOuterResult, TResultSelector, SelectResultSelector<TResult, TOuterResult>>>(
        _InnerDepthFirstFactory,
        _InnerBreadthFirstFactory,
        _Accumulator,
        _Seed,
        new ComposedResultSelector<NodeAccumulation<TSource, TAccumulate>, TResult, TOuterResult, TResultSelector, SelectResultSelector<TResult, TOuterResult>>(
          _ResultSelector, new SelectResultSelector<TResult, TOuterResult>(nodeContext => selector(nodeContext.Node))),
        _Relabels);
    }

    // The prune-after doors: the in-tier-only boundary ruling -- the light prune wrapper
    // stacks over the fold-carrying machine.
    public IAsyncTreenumerable<TResult> ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
      => new AsyncPruneAfterTreenumerable<TResult>(this, predicate);

    public IAsyncPruneAfterTreenumerable<TResult> ComposePruneAfter(Func<TResult, bool> predicate)
      => new AsyncPruneAfterTreenumerable<TResult>(this, nodeContext => predicate(nodeContext.Node));
  }
}
