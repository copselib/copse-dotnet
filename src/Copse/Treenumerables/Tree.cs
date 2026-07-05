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
  public static class Tree
  {
    // Lazy tree factory (Ix's Defer): the factory runs per treenumerator acquisition, so each
    // traversal sees a freshly constructed tree. The two dimensions invoke the factory
    // independently -- an impure factory can therefore hand them different trees, the same
    // contract as any impure source (Memoize is what pins a single shape).
    public static ITreenumerable<TNode> Defer<TNode>(Func<ITreenumerable<TNode>> treenumerableFactory)
      => new DelegatingTreenumerable<TNode>(
        () => treenumerableFactory().GetBreadthFirstTreenumerator(),
        () => treenumerableFactory().GetDepthFirstTreenumerator());

    // Resource-owning tree factory (Ix's Using). The ownership rule: each treenumerator
    // acquisition acquires its OWN resource, and that resource is disposed when that
    // treenumerator is disposed (or if construction throws before a treenumerator exists).
    // ITreenumerator.Dispose is the traversal's release point -- the same idiom a stream-fed
    // deserializer needs (see TRAVERSAL_DIMENSION_SPLIT.md).
    //
    // Interactions that fall out of the rule: the two dimensions acquire independent
    // resources; memoizing a Using tree releases the resource the moment the capture completes
    // (the memo disposes its exhausted feed, and the feed is the treenumerator holding the
    // resource).
    public static ITreenumerable<TNode> Using<TResource, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, ITreenumerable<TNode>> treenumerableFactory)
      where TResource : IDisposable
      => new DelegatingTreenumerable<TNode>(
        () => AcquireTreenumerator(resourceFactory, treenumerableFactory, tree => tree.GetBreadthFirstTreenumerator()),
        () => AcquireTreenumerator(resourceFactory, treenumerableFactory, tree => tree.GetDepthFirstTreenumerator()));

    private static ITreenumerator<TNode> AcquireTreenumerator<TResource, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, ITreenumerable<TNode>> treenumerableFactory,
      Func<ITreenumerable<TNode>, ITreenumerator<TNode>> getTreenumerator)
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
