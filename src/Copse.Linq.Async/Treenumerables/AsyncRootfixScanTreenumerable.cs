using Copse.Core.Async;
using Copse.Linq.Async; // the sync transform needs the mapped using to resolve the treenumerator
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The streaming tier's FIRST CITIZEN (SELECT_INTO_CAPTURES_DESIGN.md): RootfixScan's
  // composite result, holding the scan's recipe so a composed Select re-plants the
  // projection INSIDE the engine (the product twins) instead of stacking a wrapper. The
  // recipe is BARE (the fold's accumulator and seed at their own width, the emission mint --
  // the engines pair on the way out), and the plain acquisitions construct the plain
  // engines, selector-free, so the un-composed spelling never pays for the citizenship.
  // Narrow (single-dimension) scan results are NOT citizens (the citizenship is
  // composite-width; narrowing it is CompositeToNarrow-scale work, deferred).
  // THE FOURTH-CELL DOOR (the ancestor composer, SCAN_TIER_DESIGN.md): the citizen also
  // joins the general-splice surface, so a rejecting operator landing on a scan does NOT
  // stack a driver over the engine -- its Compose constructs the fold-carrying driver
  // (AsyncScanWhereTreenumerable) from this recipe, and the whole chain is ONE machine.
  // Probe order matters at the Select seam: bare Selects must keep taking ComposeSelect
  // (the product ENGINE -- zero extra machinery), so Select probes the citizenship BEFORE
  // this surface.
  internal sealed class AsyncRootfixScanTreenumerable<TNode, TAccumulate>
    : IAsyncSelectTreenumerable<NodeAccumulation<TNode, TAccumulate>>,
      IAsyncSelectWhereTreenumerable<NodeAccumulation<TNode, TAccumulate>>
  {
    public AsyncRootfixScanTreenumerable(
      Func<IAsyncTreenumerator<TNode>> innerDepthFirstFactory,
      Func<IAsyncTreenumerator<TNode>> innerBreadthFirstFactory,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
    {
      _InnerDepthFirstFactory = innerDepthFirstFactory;
      _InnerBreadthFirstFactory = innerBreadthFirstFactory;
      _Accumulator = accumulator;
      _Seed = seed;
    }

    private readonly Func<IAsyncTreenumerator<TNode>> _InnerDepthFirstFactory;
    private readonly Func<IAsyncTreenumerator<TNode>> _InnerBreadthFirstFactory;
    private readonly Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> _Accumulator;
    private readonly TAccumulate _Seed;

    public IAsyncTreenumerator<NodeAccumulation<TNode, TAccumulate>> GetAsyncDepthFirstTreenumerator()
      => new AsyncRootfixScanDepthFirstTreenumerator<TNode, TAccumulate>(
        _InnerDepthFirstFactory, _Accumulator, _Seed);

    public IAsyncTreenumerator<NodeAccumulation<TNode, TAccumulate>> GetAsyncBreadthFirstTreenumerator()
      => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, TAccumulate>(
        _InnerBreadthFirstFactory, _Accumulator, _Seed);

    public IAsyncSelectTreenumerable<TResult> ComposeSelect<TResult>(Func<NodeAccumulation<TNode, TAccumulate>, TResult> selector)
      => new AsyncRootfixScanProductTreenumerable<TNode, TAccumulate, TResult>(
        _InnerDepthFirstFactory, _InnerBreadthFirstFactory, _Accumulator, _Seed,
        pairingContext => selector(pairingContext.Node));

    // The general surface: a scan never relabels (it decorates), and a splicing operator's
    // leg lands in the fold-carrying driver.
    public bool Relabels => false;

    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<NodeAccumulation<TNode, TAccumulate>, TOuterResult>
      => new AsyncScanWhereTreenumerable<TNode, TAccumulate, TOuterResult, TOuterSelector>(
        _InnerDepthFirstFactory, _InnerBreadthFirstFactory, _Accumulator, _Seed, outerSelector, relabels);

    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<NodeAccumulation<TNode, TAccumulate>>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
      => Compose<TOuterResult, FuncResultSelector<NodeAccumulation<TNode, TAccumulate>, TOuterResult>>(
        new FuncResultSelector<NodeAccumulation<TNode, TAccumulate>, TOuterResult>(resultSelector), relabels);
  }
}
