using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // THE FOURTH CELL's wrapper (the ancestor composer, SCAN_TIER_DESIGN.md): a rootfix fold
  // plus a composed selector chain over the PAIR (node, accumulate), as ONE machine. The
  // recipe is (source, accumulator, seed, resultSelector); the selector algebra is the
  // lattice's own -- the legs compose over the pair source type exactly as they compose
  // over any source type, so this wrapper joins the general surface with the standard
  // struct Compose forms and every downstream Select/Where lands in ITS legs, not in a
  // stacked layer.
  //
  // Both dimensions are staged machines: DFT rides the accumulate trail
  // (AsyncScanWhereDepthFirstTreenumerator), BFT rides the accumulate tracker -- the
  // rootfix BFT engine's state machinery embedded beside the filter path
  // (AsyncScanWhereBreadthFirstTreenumerator). ONE machine per acquisition, both dims.
  internal sealed class AsyncScanWhereTreenumerable<TInner, TAccumulate, TResult, TResultSelector>
    : IAsyncSelectWhereTreenumerable<TResult>
    where TResultSelector : struct, IResultSelector<NodeAccumulation<TInner, TAccumulate>, TResult>
  {
    public AsyncScanWhereTreenumerable(
      Func<IAsyncTreenumerator<TInner>> innerDepthFirstFactory,
      Func<IAsyncTreenumerator<TInner>> innerBreadthFirstFactory,
      Func<NodeContext<TAccumulate>, NodeContext<TInner>, TAccumulate> accumulator,
      TAccumulate seed,
      TResultSelector resultSelector,
      bool relabels)
    {
      _InnerDepthFirstFactory = innerDepthFirstFactory;
      _InnerBreadthFirstFactory = innerBreadthFirstFactory;
      _Accumulator = accumulator;
      _Seed = seed;
      _ResultSelector = resultSelector;
      Relabels = relabels;
    }

    private readonly Func<IAsyncTreenumerator<TInner>> _InnerDepthFirstFactory;
    private readonly Func<IAsyncTreenumerator<TInner>> _InnerBreadthFirstFactory;
    private readonly Func<NodeContext<TAccumulate>, NodeContext<TInner>, TAccumulate> _Accumulator;
    private readonly TAccumulate _Seed;
    private readonly TResultSelector _ResultSelector;

    public bool Relabels { get; }

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator()
      => new AsyncScanWhereDepthFirstTreenumerator<TInner, TAccumulate, TResult, TResultSelector>(
        _InnerDepthFirstFactory, _Accumulator, _Seed, _ResultSelector);

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator()
      => new AsyncScanWhereBreadthFirstTreenumerator<TInner, TAccumulate, TResult, TResultSelector>(
        _InnerBreadthFirstFactory, _Accumulator, _Seed, _ResultSelector);

    // The composition law under this representation: successors keep the fold and nest the
    // outer leg onto the selector chain -- the chain composes in the TYPE, over the pair.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<TResult, TOuterResult>
    {
      return new AsyncScanWhereTreenumerable<TInner, TAccumulate, TOuterResult, ComposedResultSelector<NodeAccumulation<TInner, TAccumulate>, TResult, TOuterResult, TResultSelector, TOuterSelector>>(
        _InnerDepthFirstFactory,
        _InnerBreadthFirstFactory,
        _Accumulator,
        _Seed,
        new ComposedResultSelector<NodeAccumulation<TInner, TAccumulate>, TResult, TOuterResult, TResultSelector, TOuterSelector>(_ResultSelector, outerSelector),
        Relabels | relabels);
    }

    // The Func form is the struct form with the closure as its one leaf.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
      => Compose<TOuterResult, FuncResultSelector<TResult, TOuterResult>>(
        new FuncResultSelector<TResult, TOuterResult>(resultSelector), relabels);
  }
}
