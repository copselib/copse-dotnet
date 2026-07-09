using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Async.Treenumerables
{
  // Tree SOURCES (factories), parallel to Enumerable.Empty / Observable.Defer/Using: they MAKE
  // trees rather than transform them, so they belong with the concrete treenumerables, not with
  // the operators in Copse.Linq. Their entire dependency footprint is the treenumerable contract
  // plus disposal -- nothing Linq-specific (see PACKAGE_ARCHITECTURE.md).
  //
  // Each factory comes in three dimension flavors, because the result's dimension follows the
  // dimension of the tree you hand it -- a resource-owning or lazy source over a depth-first-only
  // tree has no reason to pretend it can serve breadth-first. The composite form (Defer/Using) is
  // for full trees; DeferDepthFirst/UsingDepthFirst and the breadth-first duals are for the
  // narrow ones (a forward-only serialized stream is the motivating case).
  //
  // This is the codegen source of truth for the sync Tree (the checked-in .g.cs twin). The
  // ASYNC-acquisition Using overloads (Func<ValueTask<TResource>>) are async-only: their
  // transcription would collapse onto the sync-acquire overloads' twins, and a sync treenumerator
  // has no awaitable frame to acquire in anyway.
  public static class AsyncTree
  {
    // ----- Defer (lazy factory; Ix's Defer): the factory runs per treenumerator acquisition,
    // so each traversal sees a freshly constructed tree. An impure factory can hand the
    // dimensions different trees, the same contract as any impure source (Memoize pins a shape).
    public static IAsyncTreenumerable<TNode> Defer<TNode>(Func<IAsyncTreenumerable<TNode>> treenumerableFactory)
      => new AsyncDelegatingTreenumerable<TNode>(
        () => treenumerableFactory().GetAsyncBreadthFirstTreenumerator(),
        () => treenumerableFactory().GetAsyncDepthFirstTreenumerator());

    public static IAsyncDepthFirstTreenumerable<TNode> DeferDepthFirst<TNode>(Func<IAsyncDepthFirstTreenumerable<TNode>> treenumerableFactory)
      => new AsyncDelegatingDepthFirstTreenumerable<TNode>(
        () => treenumerableFactory().GetAsyncDepthFirstTreenumerator());

    public static IAsyncBreadthFirstTreenumerable<TNode> DeferBreadthFirst<TNode>(Func<IAsyncBreadthFirstTreenumerable<TNode>> treenumerableFactory)
      => new AsyncDelegatingBreadthFirstTreenumerable<TNode>(
        () => treenumerableFactory().GetAsyncBreadthFirstTreenumerator());

    // ----- Using (resource-owning factory; Ix's Using). The ownership rule: each treenumerator
    // acquisition acquires its OWN resource, disposed when that treenumerator is disposed (or if
    // construction throws before a treenumerator exists). ITreenumerator.Dispose is the
    // traversal's release point -- the idiom a stream-fed deserializer needs (see
    // TRAVERSAL_DIMENSION_SPLIT.md). Memoizing a Using tree releases the resource the moment the
    // capture completes (the memo disposes its exhausted feed, which is the treenumerator holding
    // the resource).
    // codegen: begin async-only
    //
    // The constraint is IDisposable -- readers (the flagship Using resource) never grew
    // IAsyncDisposable -- but on the async surface a resource that ALSO implements
    // IAsyncDisposable gets its async disposal preferred on the release path (see
    // AsyncResourceDisposal). The construction-FAILURE path releases synchronously (it runs in
    // the sync-signature acquisition frame).
    // codegen: end async-only
    public static IAsyncTreenumerable<TNode> Using<TResource, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, IAsyncTreenumerable<TNode>> treenumerableFactory)
      where TResource : IDisposable
      => new AsyncDelegatingTreenumerable<TNode>(
        () => AcquireTreenumerator(resourceFactory, treenumerableFactory, tree => tree.GetAsyncBreadthFirstTreenumerator()),
        () => AcquireTreenumerator(resourceFactory, treenumerableFactory, tree => tree.GetAsyncDepthFirstTreenumerator()));

    public static IAsyncDepthFirstTreenumerable<TNode> UsingDepthFirst<TResource, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, IAsyncDepthFirstTreenumerable<TNode>> treenumerableFactory)
      where TResource : IDisposable
      => new AsyncDelegatingDepthFirstTreenumerable<TNode>(
        () => AcquireTreenumerator(resourceFactory, treenumerableFactory, tree => tree.GetAsyncDepthFirstTreenumerator()));

    public static IAsyncBreadthFirstTreenumerable<TNode> UsingBreadthFirst<TResource, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, IAsyncBreadthFirstTreenumerable<TNode>> treenumerableFactory)
      where TResource : IDisposable
      => new AsyncDelegatingBreadthFirstTreenumerable<TNode>(
        () => AcquireTreenumerator(resourceFactory, treenumerableFactory, tree => tree.GetAsyncBreadthFirstTreenumerator()));

    // codegen: begin async-only
    // There is deliberately NO async-ACQUISITION Using (Func<ValueTask<TResource>>). The
    // compositional structure hinges on the single treenumerable contract, whose getters are
    // sync-signature (the same choice the BCL made for IAsyncEnumerable.GetAsyncEnumerator), so
    // an awaited acquisition has nowhere to stand except a first-pull deferral cursor -- and
    // every design pass over that shape fought the contract (overload resolution, acquisition
    // timing, disposal constraints). No motivating consumer exists: the flagship Using resource
    // (a reader feeding the stream deserializer) is acquired synchronously. If a real async
    // acquirer ever appears, the design is additive; see the feature/async-prototype history.
    // codegen: end async-only

    // Acquire the resource, build the tree from it, take the requested treenumerator, and wrap
    // it so disposing the treenumerator disposes the resource. Generic over the tree type so the
    // full and narrow Using forms share one implementation.
    private static IAsyncTreenumerator<TNode> AcquireTreenumerator<TResource, TTree, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, TTree> treenumerableFactory,
      Func<TTree, IAsyncTreenumerator<TNode>> getTreenumerator)
      where TResource : IDisposable
    {
      var resource = resourceFactory();
      try
      {
        return new AsyncDisposeActionTreenumerator<TNode>(
          getTreenumerator(treenumerableFactory(resource)),
          // codegen: begin async-only
          () => AsyncResourceDisposal.DisposeAsync(resource));
          // codegen: end async-only
          // codegen: begin sync-only
          // resource.Dispose);
          // codegen: end sync-only
      }
      catch
      {
        resource.Dispose();
        throw;
      }
    }

    public static IAsyncTreenumerable<TNode> Empty<TNode>()
      => AsyncEmptyTreenumerable<TNode>.Instance;
  }
}
