using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>PruneSubtreesWhere</c> over node VALUES (prune polarity: true = prune): removes each
    /// matching node AND its whole subtree. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneSubtreesWhere<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      // A value predicate observes no coordinates, so it composes unconditionally. The selector
      // is the plain path's struct: the operator's semantics, stated once.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.Compose<TNode, PruneSubtreesWhereResultSelector<TNode>>(
          new PruneSubtreesWhereResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereTreenumerable<TNode, TNode, PruneSubtreesWhereResultSelector<TNode>>(
        source, new PruneSubtreesWhereResultSelector<TNode>(predicate));
    }

    /// <summary>
    /// Async <c>PruneSubtreesWhere</c> over (node, position) (prune polarity: true = prune). Deferred.
    /// Each positional predicate sees ITS input tree's labels.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneSubtreesWhere<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The join rule: a positional predicate composes only over a label-preserving chain.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePositional<TNode, PositionalPruneSubtreesWhereResultSelector<TNode>>(
          new PositionalPruneSubtreesWhereResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereTreenumerable<TNode, TNode, PositionalPruneSubtreesWhereResultSelector<TNode>>(
        source, new PositionalPruneSubtreesWhereResultSelector<TNode>(predicate));
    }

    /// <summary>
    /// Async <c>PruneSubtreesWhere</c> over node VALUES (prune polarity: true = prune): removes each
    /// matching node AND its whole subtree. Deferred.
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TNode> PruneSubtreesWhere<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The narrow probes mirror the composite overload's. A composite-width wrapper arriving
      // through a narrow-typed receiver composes on its own representation -- the successor
      // keeps both dimensions; a narrow chain composes to a narrow successor.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.Compose<TNode, PruneSubtreesWhereResultSelector<TNode>>(
          new PruneSubtreesWhereResultSelector<TNode>(predicate), relabels: true);

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TNode> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.Compose<TNode, PruneSubtreesWhereResultSelector<TNode>>(
          new PruneSubtreesWhereResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereDepthFirstTreenumerable<TNode, TNode, PruneSubtreesWhereResultSelector<TNode>>(
        source, new PruneSubtreesWhereResultSelector<TNode>(predicate));
    }

    /// <summary>
    /// Async <c>PruneSubtreesWhere</c> over (node, position) (prune polarity: true = prune). Deferred.
    /// Each positional predicate sees ITS input tree's labels.
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TNode> PruneSubtreesWhere<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The join rule, as in the composite positional overload: splice only while the chain
      // is label-preserving.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePositional<TNode, PositionalPruneSubtreesWhereResultSelector<TNode>>(
          new PositionalPruneSubtreesWhereResultSelector<TNode>(predicate), relabels: true);

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TNode> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.ComposePositional<TNode, PositionalPruneSubtreesWhereResultSelector<TNode>>(
          new PositionalPruneSubtreesWhereResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereDepthFirstTreenumerable<TNode, TNode, PositionalPruneSubtreesWhereResultSelector<TNode>>(
        source, new PositionalPruneSubtreesWhereResultSelector<TNode>(predicate));
    }

    /// <summary>
    /// Async <c>PruneSubtreesWhere</c> over node VALUES (prune polarity: true = prune): removes each
    /// matching node AND its whole subtree. Deferred.
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> PruneSubtreesWhere<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.Compose<TNode, PruneSubtreesWhereResultSelector<TNode>>(
          new PruneSubtreesWhereResultSelector<TNode>(predicate), relabels: true);

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TNode> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.Compose<TNode, PruneSubtreesWhereResultSelector<TNode>>(
          new PruneSubtreesWhereResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereBreadthFirstTreenumerable<TNode, TNode, PruneSubtreesWhereResultSelector<TNode>>(
        source, new PruneSubtreesWhereResultSelector<TNode>(predicate));
    }

    /// <summary>
    /// Async <c>PruneSubtreesWhere</c> over (node, position) (prune polarity: true = prune). Deferred.
    /// Each positional predicate sees ITS input tree's labels.
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> PruneSubtreesWhere<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePositional<TNode, PositionalPruneSubtreesWhereResultSelector<TNode>>(
          new PositionalPruneSubtreesWhereResultSelector<TNode>(predicate), relabels: true);

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TNode> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.ComposePositional<TNode, PositionalPruneSubtreesWhereResultSelector<TNode>>(
          new PositionalPruneSubtreesWhereResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereBreadthFirstTreenumerable<TNode, TNode, PositionalPruneSubtreesWhereResultSelector<TNode>>(
        source, new PositionalPruneSubtreesWhereResultSelector<TNode>(predicate));
    }
  }
}
