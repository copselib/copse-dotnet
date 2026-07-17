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
    /// Async <c>Where</c> over node VALUES (LINQ polarity: true = keep). Deferred. Filtered
    /// nodes' children are promoted into their parent's slot; the emitted tree's positions are
    /// recomputed accordingly.
    /// </summary>
    public static IAsyncTreenumerable<TNode> Where<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      // A value predicate observes no coordinates, so it composes unconditionally. The stage
      // is the plain path's selector struct: the operator's semantics, stated once.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.Compose(
          new WhereResultSelector<TNode>(predicate).GetResult, relabels: true);

      return new SelectWhereTreenumerable<TNode, TNode, WhereResultSelector<TNode>>(
        source, new WhereResultSelector<TNode>(predicate), containsRelabelingStage: true);
    }

    /// <summary>
    /// Async <c>Where</c> over (node, position) (LINQ polarity: true = keep; the positional
    /// analog of LINQ's indexed overload). Deferred. Each positional predicate sees ITS input
    /// tree's labels, exactly like LINQ's indexed Where re-counts per layer.
    /// </summary>
    public static IAsyncTreenumerable<TNode> Where<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The join rule, applied here because only the operator knows its lambda's flavor: a
      // positional predicate is entitled to its input tree's emitted labels, so it splices
      // only while the chain is label-preserving and otherwise stacks a real layer.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource && !selectWhereSource.ContainsRelabelingStage)
        return selectWhereSource.Compose(
          new PositionalWhereResultSelector<TNode>(predicate).GetResult, relabels: true);

      return new SelectWhereTreenumerable<TNode, TNode, PositionalWhereResultSelector<TNode>>(
        source, new PositionalWhereResultSelector<TNode>(predicate), containsRelabelingStage: true);
    }

    public static IAsyncDepthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<TNode, TNode, WhereResultSelector<TNode>>(
            source.GetAsyncDepthFirstTreenumerator, new WhereResultSelector<TNode>(predicate)));
    }

    public static IAsyncDepthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<TNode, TNode, PositionalWhereResultSelector<TNode>>(
            source.GetAsyncDepthFirstTreenumerator, new PositionalWhereResultSelector<TNode>(predicate)));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<TNode, TNode, WhereResultSelector<TNode>>(
            source.GetAsyncBreadthFirstTreenumerator, new WhereResultSelector<TNode>(predicate)));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<TNode, TNode, PositionalWhereResultSelector<TNode>>(
            source.GetAsyncBreadthFirstTreenumerator, new PositionalWhereResultSelector<TNode>(predicate)));
    }

  }
}
