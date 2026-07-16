using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>Where</c> over node VALUES (LINQ polarity: true = keep). Deferred. The
    /// value-only flavor is the fusable one: adjacent value-only Wheres collapse into a single
    /// filtering pass by predicate combination, and a value-only Where over a Select collapses
    /// into the projection-carrying filter driver -- neither predicate observes positions, so
    /// the collapse is invisible (docs/OPERATOR_FUSION_DESIGN.md).
    /// </summary>
    public static IAsyncTreenumerable<TNode> Where<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncFusableTreenumerable<TNode> fusableSource)
      {
        var fused = fusableSource.FuseWhere(predicate);

        if (fused != null)
          return fused;
      }

      return new AsyncWhereTreenumerable<TNode>(source, predicate);
    }

    /// <summary>
    /// Async <c>Where</c> over (node, position) (LINQ polarity: true = keep; the positional
    /// analog of LINQ's indexed overload). Deferred. Each positional predicate sees ITS input
    /// tree's labels, so positional Wheres never fuse with their own kind -- a Where boundary
    /// relabels (depth compression, sibling renumbering) exactly like LINQ's indexed Where
    /// re-counts. A positional Where over a Select still fuses (projection never moves nodes).
    /// </summary>
    public static IAsyncTreenumerable<TNode> Where<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncFusableTreenumerable<TNode> fusableSource)
      {
        var fused = fusableSource.FusePositionalWhere(predicate);

        if (fused != null)
          return fused;
      }

      return
        AsyncTreenumerableFactory.Create(
          () => new AsyncWhereBreadthFirstTreenumerator<TNode, TNode>(
            source.GetAsyncBreadthFirstTreenumerator,
            AsyncIdentitySelector<TNode>.Instance,
            nodeContext => predicate(nodeContext.Node, nodeContext.Position),
            NodeTraversalStrategies.SkipNode),
          () => new AsyncWhereDepthFirstTreenumerator<TNode, TNode>(
            source.GetAsyncDepthFirstTreenumerator,
            AsyncIdentitySelector<TNode>.Instance,
            nodeContext => predicate(nodeContext.Node, nodeContext.Position),
            NodeTraversalStrategies.SkipNode));
    }

    public static IAsyncDepthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<TNode, TNode>(
            source.GetAsyncDepthFirstTreenumerator,
            AsyncIdentitySelector<TNode>.Instance,
            nodeContext => predicate(nodeContext.Node),
            NodeTraversalStrategies.SkipNode));
    }

    public static IAsyncDepthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<TNode, TNode>(
            source.GetAsyncDepthFirstTreenumerator,
            AsyncIdentitySelector<TNode>.Instance,
            nodeContext => predicate(nodeContext.Node, nodeContext.Position),
            NodeTraversalStrategies.SkipNode));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<TNode, TNode>(
            source.GetAsyncBreadthFirstTreenumerator,
            AsyncIdentitySelector<TNode>.Instance,
            nodeContext => predicate(nodeContext.Node),
            NodeTraversalStrategies.SkipNode));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<TNode, TNode>(
            source.GetAsyncBreadthFirstTreenumerator,
            AsyncIdentitySelector<TNode>.Instance,
            nodeContext => predicate(nodeContext.Node, nodeContext.Position),
            NodeTraversalStrategies.SkipNode));
    }
  }
}
