using Copse.Async.ChildEnumerators;
using Copse.Async.Treenumerables;
using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using System;
using System.Collections.Generic;

namespace Copse.Async
{
  // This is the codegen source of truth for the sync Tree (the checked-in .g.cs twin).
  /// <summary>
  /// The tree sources: factories that make trees, the way <c>Enumerable.Empty</c> and Ix's
  /// <c>Defer</c>/<c>Using</c> make sequences. Each factory comes in three dimension flavors,
  /// because the result's dimension follows the tree you hand it: the composite form for full
  /// trees, and <c>…DepthFirst</c>/<c>…BreadthFirst</c> forms for sources that afford only one
  /// traversal order (a forward-only serialized stream is the motivating case).
  /// </summary>
  public static class AsyncTree
  {
    /// <summary>A tree built fresh for every traversal: <paramref name="treenumerableFactory"/>
    /// runs per treenumerator acquisition, like Ix's <c>Defer</c>. An impure factory can hand
    /// different traversals different trees; <c>Memoize</c> pins one.</summary>
    public static IAsyncTreenumerable<TNode> Defer<TNode>(Func<IAsyncTreenumerable<TNode>> treenumerableFactory)
      => new AsyncDelegatingTreenumerable<TNode>(
        () => treenumerableFactory().GetAsyncBreadthFirstTreenumerator(),
        () => treenumerableFactory().GetAsyncDepthFirstTreenumerator());

    /// <summary>The depth-first-narrow form of <see cref="Defer{TNode}"/>.</summary>
    public static IAsyncDepthFirstTreenumerable<TNode> DeferDepthFirst<TNode>(Func<IAsyncDepthFirstTreenumerable<TNode>> treenumerableFactory)
      => new AsyncDelegatingDepthFirstTreenumerable<TNode>(
        () => treenumerableFactory().GetAsyncDepthFirstTreenumerator());

    /// <summary>The breadth-first-narrow form of <see cref="Defer{TNode}"/>.</summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> DeferBreadthFirst<TNode>(Func<IAsyncBreadthFirstTreenumerable<TNode>> treenumerableFactory)
      => new AsyncDelegatingBreadthFirstTreenumerable<TNode>(
        () => treenumerableFactory().GetAsyncBreadthFirstTreenumerator());

    /// <summary>A tree built once: <paramref name="treenumerableFactory"/> runs at the first
    /// treenumerator acquisition, and every later traversal, in either dimension, walks the
    /// same tree object. This pins the tree's identity, not its data -- <c>Memoize</c> is the
    /// next rung up, pinning the traversal data itself.
    ///
    /// <para>For expensive construction, not resource acquisition: a pinned tree has no
    /// release point, so a factory that acquires a resource belongs to
    /// <see cref="Using{TResource, TNode}"/>. Construction is not synchronized --
    /// single-consumer, like the traversals it feeds.</para></summary>
    public static IAsyncTreenumerable<TNode> Lazy<TNode>(Func<IAsyncTreenumerable<TNode>> treenumerableFactory)
    {
      var lazyTree = new Lazy<IAsyncTreenumerable<TNode>>(treenumerableFactory);

      return new AsyncDelegatingTreenumerable<TNode>(
        () => lazyTree.Value.GetAsyncBreadthFirstTreenumerator(),
        () => lazyTree.Value.GetAsyncDepthFirstTreenumerator());
    }

    /// <summary>The dimension-observing form of <see cref="Lazy{TNode}(Func{IAsyncTreenumerable{TNode}})"/>:
    /// the factory is told which traversal order was demanded first, so a one-time construction
    /// with a representation choice (a capture that could lay out preorder or level-order) can
    /// favor its first consumer. The constructed tree is still pinned for both
    /// dimensions.</summary>
    public static IAsyncTreenumerable<TNode> Lazy<TNode>(Func<TreeTraversalStrategy, IAsyncTreenumerable<TNode>> treenumerableFactory)
    {
      IAsyncTreenumerable<TNode> constructedTree = null;

      IAsyncTreenumerable<TNode> GetOrConstruct(TreeTraversalStrategy firstDimension)
        => constructedTree ?? (constructedTree = treenumerableFactory(firstDimension));

      return new AsyncDelegatingTreenumerable<TNode>(
        () => GetOrConstruct(TreeTraversalStrategy.BreadthFirst).GetAsyncBreadthFirstTreenumerator(),
        () => GetOrConstruct(TreeTraversalStrategy.DepthFirst).GetAsyncDepthFirstTreenumerator());
    }

    // The narrow duals need no dimension-observing form: with one dimension there is nothing
    // to observe.
    /// <summary>The depth-first-narrow form of <see cref="Lazy{TNode}(Func{IAsyncTreenumerable{TNode}})"/>.</summary>
    public static IAsyncDepthFirstTreenumerable<TNode> LazyDepthFirst<TNode>(Func<IAsyncDepthFirstTreenumerable<TNode>> treenumerableFactory)
    {
      var lazyTree = new Lazy<IAsyncDepthFirstTreenumerable<TNode>>(treenumerableFactory);

      return new AsyncDelegatingDepthFirstTreenumerable<TNode>(
        () => lazyTree.Value.GetAsyncDepthFirstTreenumerator());
    }

    /// <summary>The breadth-first-narrow form of <see cref="Lazy{TNode}(Func{IAsyncTreenumerable{TNode}})"/>.</summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> LazyBreadthFirst<TNode>(Func<IAsyncBreadthFirstTreenumerable<TNode>> treenumerableFactory)
    {
      var lazyTree = new Lazy<IAsyncBreadthFirstTreenumerable<TNode>>(treenumerableFactory);

      return new AsyncDelegatingBreadthFirstTreenumerable<TNode>(
        () => lazyTree.Value.GetAsyncBreadthFirstTreenumerator());
    }

    /// <summary>A tree over an owned resource, like Ix's <c>Using</c>: each traversal acquires
    /// its own resource from <paramref name="resourceFactory"/> and disposes it when the
    /// traversal's treenumerator is disposed (or immediately, if construction throws before a
    /// treenumerator exists). Memoizing a Using tree releases the resource as soon as the
    /// capture completes.</summary>
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

    /// <summary>The depth-first-narrow form of <see cref="Using{TResource, TNode}"/>.</summary>
    public static IAsyncDepthFirstTreenumerable<TNode> UsingDepthFirst<TResource, TNode>(
      Func<TResource> resourceFactory,
      Func<TResource, IAsyncDepthFirstTreenumerable<TNode>> treenumerableFactory)
      where TResource : IDisposable
      => new AsyncDelegatingDepthFirstTreenumerable<TNode>(
        () => AcquireTreenumerator(resourceFactory, treenumerableFactory, tree => tree.GetAsyncDepthFirstTreenumerator()));

    /// <summary>The breadth-first-narrow form of <see cref="Using{TResource, TNode}"/>.</summary>
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

    /// <summary>The empty forest: no roots, no visits.</summary>
    public static IAsyncTreenumerable<TNode> Empty<TNode>()
      => AsyncEmptyTreenumerable<TNode>.Instance;

    /// <summary>A treenumerable from its two treenumerator factories directly -- the lowest-
    /// level way in: a treenumerable is nothing more than a pair of cursor factories, and
    /// Create wraps yours. Each factory must produce a fresh cursor per call.</summary>
    public static IAsyncTreenumerable<TNode> Create<TNode>(
      Func<IAsyncTreenumerator<TNode>> breadthFirstTreenumeratorFactory,
      Func<IAsyncTreenumerator<TNode>> depthFirstTreenumeratorFactory)
      => new AsyncDelegatingTreenumerable<TNode>(
        breadthFirstTreenumeratorFactory,
        depthFirstTreenumeratorFactory);

    /// <summary>The depth-first-narrow form of <see cref="Create{TNode}"/>.</summary>
    public static IAsyncDepthFirstTreenumerable<TNode> CreateDepthFirst<TNode>(
      Func<IAsyncTreenumerator<TNode>> depthFirstTreenumeratorFactory)
      => new AsyncDelegatingDepthFirstTreenumerable<TNode>(depthFirstTreenumeratorFactory);

    /// <summary>The breadth-first-narrow form of <see cref="Create{TNode}"/>.</summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> CreateBreadthFirst<TNode>(
      Func<IAsyncTreenumerator<TNode>> breadthFirstTreenumeratorFactory)
      => new AsyncDelegatingBreadthFirstTreenumerable<TNode>(breadthFirstTreenumeratorFactory);

    /// <summary>A treenumerable that traverses any <see cref="IAsyncTreeTopology{TNode, THandle}"/>
    /// by probing it -- the bridge for third-party structures: implement the four-probe
    /// topology interface over your native tree and this affords both traversal orders.
    /// Values are read through <c>GetValueAsync</c> during the walk.</summary>
    public static IAsyncTreenumerable<TNode> FromTopology<TNode, THandle>(
      IAsyncTreeTopology<TNode, THandle> topology)
      => new AsyncTreenumerable<TNode, HandleAndValue<THandle, TNode>, AsyncTopologyChildEnumerator<TNode, THandle>>(
        nodeContext => new AsyncTopologyChildEnumerator<TNode, THandle>(topology, nodeContext.Node.Handle),
        labeledNode => labeledNode.Value,
        RootsFrom(topology));

    private static async IAsyncEnumerable<HandleAndValue<THandle, TNode>> RootsFrom<TNode, THandle>(
      IAsyncTreeTopology<TNode, THandle> topology)
    {
      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootResult = await topology.TryGetRootAtAsync(rootIndex).ConfigureAwait(false);

        if (!rootResult.HasValue)
          yield break;

        var value = await topology.GetValueAsync(rootResult.Value.Node).ConfigureAwait(false);

        yield return new HandleAndValue<THandle, TNode>(rootResult.Value.Node, value);
      }
    }
  }
}
