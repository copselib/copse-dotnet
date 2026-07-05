using Copse.Core;
using Copse.Treenumerators;
using System;

namespace Copse.Treenumerables
{
  // Tree SOURCES (factories), parallel to Enumerable.Empty / Observable.Defer/Using: they MAKE
  // trees rather than transform them, so they belong with the concrete treenumerables in Copse,
  // not with the operators in Copse.Linq. Their entire dependency footprint is the
  // ITreenumerable contract plus IDisposable -- nothing Linq-specific -- which is why the move
  // is clean (see PACKAGE_ARCHITECTURE.md).
  //
  // Each factory comes in three dimension flavors, because the result's dimension follows the
  // dimension of the tree you hand it -- a resource-owning or lazy source over an
  // IDepthFirstTreenumerable has no reason to pretend it can serve breadth-first. The composite
  // form (Defer/Using) is for full trees; DeferDepthFirst/UsingDepthFirst and the breadth-first
  // duals are for the narrow ones (a forward-only serialized stream is the motivating case).
  public static class Tree
  {
    // ----- Defer (lazy factory; Ix's Defer): the factory runs per treenumerator acquisition,
    // so each traversal sees a freshly constructed tree. An impure factory can hand the
    // dimensions different trees, the same contract as any impure source (Memoize pins a shape).

    public static ITreenumerable<TNode> Defer<TNode>(Func<ITreenumerable<TNode>> treenumerableFactory)
      => new DelegatingTreenumerable<TNode>(
        () => treenumerableFactory().GetBreadthFirstTreenumerator(),
        () => treenumerableFactory().GetDepthFirstTreenumerator());

    public static IDepthFirstTreenumerable<TNode> DeferDepthFirst<TNode>(Func<IDepthFirstTreenumerable<TNode>> treenumerableFactory)
      => new DelegatingDepthFirstTreenumerable<TNode>(
        () => treenumerableFactory().GetDepthFirstTreenumerator());

    public static IBreadthFirstTreenumerable<TNode> DeferBreadthFirst<TNode>(Func<IBreadthFirstTreenumerable<TNode>> treenumerableFactory)
      => new DelegatingBreadthFirstTreenumerable<TNode>(
        () => treenumerableFactory().GetBreadthFirstTreenumerator());

    // ----- Using (resource-owning factory; Ix's Using). The ownership rule: each treenumerator
    // acquisition acquires its OWN resource, disposed when that treenumerator is disposed (or if
    // construction throws before a treenumerator exists). ITreenumerator.Dispose is the
    // traversal's release point -- the idiom a stream-fed deserializer needs (see
    // TRAVERSAL_DIMENSION_SPLIT.md). Memoizing a Using tree releases the resource the moment the
    // capture completes (the memo disposes its exhausted feed, which is the treenumerator holding
    // the resource).

    public static ITreenumerable<TNode> Using<TResource, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, ITreenumerable<TNode>> treenumerableFactory)
      where TResource : IDisposable
      => new DelegatingTreenumerable<TNode>(
        () => AcquireTreenumerator(resourceFactory, treenumerableFactory, tree => tree.GetBreadthFirstTreenumerator()),
        () => AcquireTreenumerator(resourceFactory, treenumerableFactory, tree => tree.GetDepthFirstTreenumerator()));

    public static IDepthFirstTreenumerable<TNode> UsingDepthFirst<TResource, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, IDepthFirstTreenumerable<TNode>> treenumerableFactory)
      where TResource : IDisposable
      => new DelegatingDepthFirstTreenumerable<TNode>(
        () => AcquireTreenumerator(resourceFactory, treenumerableFactory, tree => tree.GetDepthFirstTreenumerator()));

    public static IBreadthFirstTreenumerable<TNode> UsingBreadthFirst<TResource, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, IBreadthFirstTreenumerable<TNode>> treenumerableFactory)
      where TResource : IDisposable
      => new DelegatingBreadthFirstTreenumerable<TNode>(
        () => AcquireTreenumerator(resourceFactory, treenumerableFactory, tree => tree.GetBreadthFirstTreenumerator()));

    // Acquire the resource, build the tree from it, take the requested treenumerator, and wrap
    // it so disposing the treenumerator disposes the resource. Generic over the tree type so the
    // full and narrow Using forms share one implementation.
    private static ITreenumerator<TNode> AcquireTreenumerator<TResource, TTree, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, TTree> treenumerableFactory,
      Func<TTree, ITreenumerator<TNode>> getTreenumerator)
      where TResource : IDisposable
    {
      var resource = resourceFactory();

      try
      {
        return new DisposeActionTreenumerator<TNode>(
          getTreenumerator(treenumerableFactory(resource)),
          resource.Dispose);
      }
      catch
      {
        resource.Dispose();
        throw;
      }
    }

    public static ITreenumerable<TNode> Empty<TNode>()
      => EmptyTreenumerable<TNode>.Instance;
  }
}
