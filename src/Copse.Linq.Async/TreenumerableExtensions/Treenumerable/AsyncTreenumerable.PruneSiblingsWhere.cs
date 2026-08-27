using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>PruneSiblingsWhere</c> over node VALUES (prune polarity: true = prune): each
    /// matching node stays -- visits, descendants, and position untouched -- and its later
    /// siblings are removed. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneSiblingsWhere<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      // A value predicate observes no coordinates, so it composes unconditionally. The matched
      // node stays and only its later siblings go, so no surviving label moves: never relabels.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.Compose<TNode, PruneSiblingsWhereResultSelector<TNode>>(
          new PruneSiblingsWhereResultSelector<TNode>(predicate), relabels: false);

      return new AsyncSelectWhereTreenumerable<TNode, TNode, PruneSiblingsWhereResultSelector<TNode>>(
        source, new PruneSiblingsWhereResultSelector<TNode>(predicate));
    }

    /// <summary>
    /// Async <c>PruneSiblingsWhere</c> over (node, position) (prune polarity: true = prune).
    /// Deferred. Each positional predicate sees ITS input tree's labels.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneSiblingsWhere<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The join rule: a positional predicate composes only over a label-preserving chain.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePositional<TNode, PositionalPruneSiblingsWhereResultSelector<TNode>>(
          new PositionalPruneSiblingsWhereResultSelector<TNode>(predicate), relabels: false);

      return new AsyncSelectWhereTreenumerable<TNode, TNode, PositionalPruneSiblingsWhereResultSelector<TNode>>(
        source, new PositionalPruneSiblingsWhereResultSelector<TNode>(predicate));
    }

    public static IAsyncDepthFirstTreenumerable<TNode> PruneSiblingsWhere<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The narrow probes mirror the composite overload's. A composite-width wrapper arriving
      // through a narrow-typed receiver composes on its own representation -- the successor
      // keeps both dimensions; a narrow chain composes to a narrow successor.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.Compose<TNode, PruneSiblingsWhereResultSelector<TNode>>(
          new PruneSiblingsWhereResultSelector<TNode>(predicate), relabels: false);

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TNode> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.Compose<TNode, PruneSiblingsWhereResultSelector<TNode>>(
          new PruneSiblingsWhereResultSelector<TNode>(predicate), relabels: false);

      return new AsyncSelectWhereDepthFirstTreenumerable<TNode, TNode, PruneSiblingsWhereResultSelector<TNode>>(
        source, new PruneSiblingsWhereResultSelector<TNode>(predicate));
    }

    public static IAsyncDepthFirstTreenumerable<TNode> PruneSiblingsWhere<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The join rule, as in the composite positional overload: splice only while the chain
      // is label-preserving.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePositional<TNode, PositionalPruneSiblingsWhereResultSelector<TNode>>(
          new PositionalPruneSiblingsWhereResultSelector<TNode>(predicate), relabels: false);

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TNode> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.ComposePositional<TNode, PositionalPruneSiblingsWhereResultSelector<TNode>>(
          new PositionalPruneSiblingsWhereResultSelector<TNode>(predicate), relabels: false);

      return new AsyncSelectWhereDepthFirstTreenumerable<TNode, TNode, PositionalPruneSiblingsWhereResultSelector<TNode>>(
        source, new PositionalPruneSiblingsWhereResultSelector<TNode>(predicate));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> PruneSiblingsWhere<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.Compose<TNode, PruneSiblingsWhereResultSelector<TNode>>(
          new PruneSiblingsWhereResultSelector<TNode>(predicate), relabels: false);

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TNode> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.Compose<TNode, PruneSiblingsWhereResultSelector<TNode>>(
          new PruneSiblingsWhereResultSelector<TNode>(predicate), relabels: false);

      return new AsyncSelectWhereBreadthFirstTreenumerable<TNode, TNode, PruneSiblingsWhereResultSelector<TNode>>(
        source, new PruneSiblingsWhereResultSelector<TNode>(predicate));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> PruneSiblingsWhere<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePositional<TNode, PositionalPruneSiblingsWhereResultSelector<TNode>>(
          new PositionalPruneSiblingsWhereResultSelector<TNode>(predicate), relabels: false);

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TNode> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.ComposePositional<TNode, PositionalPruneSiblingsWhereResultSelector<TNode>>(
          new PositionalPruneSiblingsWhereResultSelector<TNode>(predicate), relabels: false);

      return new AsyncSelectWhereBreadthFirstTreenumerable<TNode, TNode, PositionalPruneSiblingsWhereResultSelector<TNode>>(
        source, new PositionalPruneSiblingsWhereResultSelector<TNode>(predicate));
    }
  }
}
