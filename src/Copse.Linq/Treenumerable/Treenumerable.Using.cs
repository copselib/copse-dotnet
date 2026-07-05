using Copse.Core;
using Copse.Linq.Treenumerators;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    // Resource-owning tree factory (Ix's Using). The ownership rule: each treenumerator
    // acquisition acquires its OWN resource, and that resource is disposed when that
    // treenumerator is disposed (or if construction throws before a treenumerator exists).
    // ITreenumerator.Dispose is the traversal's release point -- the same idiom a stream-fed
    // deserializer needs (see TRAVERSAL_DIMENSION_SPLIT.md), piloted here.
    //
    // Interactions that fall out of the rule: the two dimensions acquire independent
    // resources; memoizing a Using tree releases the resource the moment the capture
    // completes (the memo disposes its exhausted feed, and the feed is the treenumerator
    // holding the resource).
    public static ITreenumerable<TNode> Using<TResource, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, ITreenumerable<TNode>> treenumerableFactory)
      where TResource : IDisposable
      => TreenumerableFactory.Create(
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
  }
}
