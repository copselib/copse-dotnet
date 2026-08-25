using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>PruneBefore</c> over node VALUES (prune polarity: true = prune): removes each
    /// matching node AND its whole subtree. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneBefore<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      // A value predicate observes no coordinates, so it composes unconditionally. The selector
      // is the plain path's struct: the operator's semantics, stated once.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.Compose<TNode, PruneBeforeResultSelector<TNode>>(
          new PruneBeforeResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereTreenumerable<TNode, TNode, PruneBeforeResultSelector<TNode>>(
        source, new PruneBeforeResultSelector<TNode>(predicate));
    }

    /// <summary>
    /// Async <c>PruneBefore</c> over (node, position) (prune polarity: true = prune). Deferred.
    /// Each positional predicate sees ITS input tree's labels.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneBefore<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The join rule: a positional predicate composes only over a label-preserving chain.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePositional<TNode, PositionalPruneBeforeResultSelector<TNode>>(
          new PositionalPruneBeforeResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereTreenumerable<TNode, TNode, PositionalPruneBeforeResultSelector<TNode>>(
        source, new PositionalPruneBeforeResultSelector<TNode>(predicate));
    }

    public static IAsyncDepthFirstTreenumerable<TNode> PruneBefore<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The narrow probes mirror the composite overload's. A composite-width wrapper arriving
      // through a narrow-typed receiver composes on its own representation -- the successor
      // keeps both dimensions; a narrow chain composes to a narrow successor.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.Compose<TNode, PruneBeforeResultSelector<TNode>>(
          new PruneBeforeResultSelector<TNode>(predicate), relabels: true);

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TNode> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.Compose<TNode, PruneBeforeResultSelector<TNode>>(
          new PruneBeforeResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereDepthFirstTreenumerable<TNode, TNode, PruneBeforeResultSelector<TNode>>(
        source, new PruneBeforeResultSelector<TNode>(predicate));
    }

    public static IAsyncDepthFirstTreenumerable<TNode> PruneBefore<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The join rule, as in the composite positional overload: splice only while the chain
      // is label-preserving.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePositional<TNode, PositionalPruneBeforeResultSelector<TNode>>(
          new PositionalPruneBeforeResultSelector<TNode>(predicate), relabels: true);

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TNode> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.ComposePositional<TNode, PositionalPruneBeforeResultSelector<TNode>>(
          new PositionalPruneBeforeResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereDepthFirstTreenumerable<TNode, TNode, PositionalPruneBeforeResultSelector<TNode>>(
        source, new PositionalPruneBeforeResultSelector<TNode>(predicate));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> PruneBefore<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.Compose<TNode, PruneBeforeResultSelector<TNode>>(
          new PruneBeforeResultSelector<TNode>(predicate), relabels: true);

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TNode> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.Compose<TNode, PruneBeforeResultSelector<TNode>>(
          new PruneBeforeResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereBreadthFirstTreenumerable<TNode, TNode, PruneBeforeResultSelector<TNode>>(
        source, new PruneBeforeResultSelector<TNode>(predicate));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> PruneBefore<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePositional<TNode, PositionalPruneBeforeResultSelector<TNode>>(
          new PositionalPruneBeforeResultSelector<TNode>(predicate), relabels: true);

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TNode> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.ComposePositional<TNode, PositionalPruneBeforeResultSelector<TNode>>(
          new PositionalPruneBeforeResultSelector<TNode>(predicate), relabels: true);

      return new AsyncSelectWhereBreadthFirstTreenumerable<TNode, TNode, PositionalPruneBeforeResultSelector<TNode>>(
        source, new PositionalPruneBeforeResultSelector<TNode>(predicate));
    }
  }
}
