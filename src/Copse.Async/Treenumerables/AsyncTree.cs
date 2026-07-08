using Copse.Core.Async;
using System;

namespace Copse.Async.Treenumerables
{
  // Async tree SOURCES (factories), the async analog of Copse.Treenumerables.Tree: they MAKE async
  // trees rather than transform them, so they live with the concrete async treenumerables here, not
  // with the operators in Copse.Linq.Async. Their whole footprint is the IAsyncTreenumerable
  // contract -- nothing Linq-specific.
  //
  // DEFERRED until the async flat-stream layer (its only motivating consumer) lands:
  //   * Using (resource-owning) -- an async resource is released via DisposeAsync, but a treenumerator
  //     is acquired synchronously (GetAsync...Treenumerator is not awaitable), so a construction-time
  //     failure can't await the resource's release. That needs a lazy-acquire cursor whose first
  //     motivating source is the stream-fed async deserializer. Along with it: the narrow
  //     Defer/UsingDepthFirst/BreadthFirst forms (an async forward-only stream is the only source that
  //     affords just one async dimension) and AsyncDisposeActionTreenumerator (Using's release wrapper).
  public static class AsyncTree
  {
    // Defer (lazy factory; Ix's Defer): the factory runs per treenumerator acquisition, so each
    // traversal sees a freshly constructed async tree. An impure factory can hand the two dimensions
    // different trees -- the same contract as any impure source.
    public static IAsyncTreenumerable<TNode> Defer<TNode>(Func<IAsyncTreenumerable<TNode>> treenumerableFactory)
      => new AsyncDelegatingTreenumerable<TNode>(
        () => treenumerableFactory().GetAsyncBreadthFirstTreenumerator(),
        () => treenumerableFactory().GetAsyncDepthFirstTreenumerator());

    public static IAsyncTreenumerable<TNode> Empty<TNode>()
      => AsyncEmptyTreenumerable<TNode>.Instance;
  }
}
