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
  // DIMENSION DISPATCH (interim, 2026-08-18): the depth-first acquisition is the staged
  // machine (AsyncScanWhereDepthFirstTreenumerator -- fold trail + filter in one pull
  // loop); the breadth-first acquisition is the two-machine spelling (the scan product
  // engine feeding a plain filter driver) until the BFT staged machine lands -- its
  // accumulate trail needs the skip-prefix's re-anchoring discipline plus rejected-node
  // replay, a separate build. The dispatch is invisible to the algebra (the
  // TakeSubtreesWhere lesson: machinery is an acquisition-time fact).
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
      => new AsyncWhereBreadthFirstTreenumerator<NodeAccumulation<TInner, TAccumulate>, TResult, TResultSelector>(
        new AsyncRootfixScanTreenumerable<TInner, TAccumulate>(
          _InnerDepthFirstFactory, _InnerBreadthFirstFactory, _Accumulator, _Seed)
          .GetAsyncBreadthFirstTreenumerator,
        _ResultSelector);

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
