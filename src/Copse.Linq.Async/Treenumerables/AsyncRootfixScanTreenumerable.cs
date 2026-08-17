using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq.Async
{
  // The streaming tier's FIRST CITIZEN (SELECT_INTO_CAPTURES_DESIGN.md): RootfixScan's
  // composite result, holding the scan's recipe so a composed Select re-plants the
  // projection INSIDE the engine (the product twins) instead of stacking a wrapper. The
  // recipe is BARE (the fold's accumulator and seed at their own width, the emission mint --
  // the engines pair on the way out), and the plain acquisitions construct the plain
  // engines, selector-free, so the un-composed spelling never pays for the citizenship.
  // Narrow (single-dimension) scan results are NOT citizens (the citizenship is
  // composite-width; narrowing it is CompositeToNarrow-scale work, deferred).
  internal sealed class AsyncRootfixScanTreenumerable<TNode, TAccumulate>
    : IAsyncSelectComposableTreenumerable<NodeAccumulation<TNode, TAccumulate>>
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

    public IAsyncSelectComposableTreenumerable<TResult> ComposeSelect<TResult>(Func<NodeAccumulation<TNode, TAccumulate>, TResult> selector)
      => new AsyncRootfixScanProductTreenumerable<TNode, TAccumulate, TResult>(
        _InnerDepthFirstFactory, _InnerBreadthFirstFactory, _Accumulator, _Seed, selector);
  }
}
