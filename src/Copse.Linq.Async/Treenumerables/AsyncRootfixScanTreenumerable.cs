using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq.Async
{
  // The streaming tier's FIRST CITIZEN (SELECT_INTO_CAPTURES_DESIGN.md): RootfixScan's
  // composite result, holding the scan's recipe so a composed Select re-plants the
  // projection INSIDE the engine (the product twins) instead of stacking a wrapper. The
  // plain acquisitions construct exactly what the operator constructed before this type
  // existed -- same engines, same adapters, same delegates -- so the un-composed spelling
  // never pays for the citizenship. Narrow (single-dimension) scan results are NOT citizens
  // (the citizenship is composite-width; narrowing it is CompositeToNarrow-scale work,
  // deferred).
  internal sealed class AsyncRootfixScanTreenumerable<TNode, TAccumulate>
    : IAsyncSelectComposableTreenumerable<NodeAccumulation<TNode, TAccumulate>>
  {
    public AsyncRootfixScanTreenumerable(
      Func<IAsyncTreenumerator<TNode>> innerDepthFirstFactory,
      Func<IAsyncTreenumerator<TNode>> innerBreadthFirstFactory,
      Func<NodeContext<NodeAccumulation<TNode, TAccumulate>>, NodeContext<TNode>, NodeAccumulation<TNode, TAccumulate>> accumulator,
      NodeAccumulation<TNode, TAccumulate> seed)
    {
      _InnerDepthFirstFactory = innerDepthFirstFactory;
      _InnerBreadthFirstFactory = innerBreadthFirstFactory;
      _Accumulator = accumulator;
      _Seed = seed;
    }

    private readonly Func<IAsyncTreenumerator<TNode>> _InnerDepthFirstFactory;
    private readonly Func<IAsyncTreenumerator<TNode>> _InnerBreadthFirstFactory;
    private readonly Func<NodeContext<NodeAccumulation<TNode, TAccumulate>>, NodeContext<TNode>, NodeAccumulation<TNode, TAccumulate>> _Accumulator;
    private readonly NodeAccumulation<TNode, TAccumulate> _Seed;

    public IAsyncTreenumerator<NodeAccumulation<TNode, TAccumulate>> GetAsyncDepthFirstTreenumerator()
      => new AsyncRootfixScanDepthFirstTreenumerator<TNode, NodeAccumulation<TNode, TAccumulate>>(
        _InnerDepthFirstFactory, _Accumulator, _Seed);

    public IAsyncTreenumerator<NodeAccumulation<TNode, TAccumulate>> GetAsyncBreadthFirstTreenumerator()
      => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, NodeAccumulation<TNode, TAccumulate>>(
        _InnerBreadthFirstFactory, _Accumulator, _Seed);

    public IAsyncSelectComposableTreenumerable<TResult> ComposeSelect<TResult>(Func<NodeAccumulation<TNode, TAccumulate>, TResult> selector)
      => new AsyncRootfixScanProductTreenumerable<TNode, TAccumulate, TResult>(
        _InnerDepthFirstFactory, _InnerBreadthFirstFactory, _Accumulator, _Seed, selector);
  }
}
