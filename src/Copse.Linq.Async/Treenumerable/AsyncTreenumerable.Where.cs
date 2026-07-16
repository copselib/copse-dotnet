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

      // A value predicate observes no coordinates, so it composes unconditionally.
      if (source is IAsyncComposableTreenumerable<TNode> composableSource)
        return composableSource.Map.Filter(
          nodeContext => new CompositionVerdict<TNode>(
            nodeContext.Node,
            predicate(nodeContext.Node)
              ? NodeTraversalStrategies.TraverseAll
              : NodeTraversalStrategies.SkipNode),
          relabels: true).ToTreenumerable();

      return new ComposableTreenumerable<TNode, TNode, WhereVerdictSelector<TNode>>(
        source, new WhereVerdictSelector<TNode>(predicate), containsRelabelingStage: true);
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
      if (source is IAsyncComposableTreenumerable<TNode> composableSource && !composableSource.Map.ContainsRelabelingStage)
        return composableSource.Map.Filter(
          nodeContext => new CompositionVerdict<TNode>(
            nodeContext.Node,
            predicate(nodeContext.Node, nodeContext.Position)
              ? NodeTraversalStrategies.TraverseAll
              : NodeTraversalStrategies.SkipNode),
          relabels: true).ToTreenumerable();

      return new ComposableTreenumerable<TNode, TNode, PositionalWhereVerdictSelector<TNode>>(
        source, new PositionalWhereVerdictSelector<TNode>(predicate), containsRelabelingStage: true);
    }

    public static IAsyncDepthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<TNode, TNode, WhereVerdictSelector<TNode>>(
            source.GetAsyncDepthFirstTreenumerator, new WhereVerdictSelector<TNode>(predicate)));
    }

    public static IAsyncDepthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<TNode, TNode, PositionalWhereVerdictSelector<TNode>>(
            source.GetAsyncDepthFirstTreenumerator, new PositionalWhereVerdictSelector<TNode>(predicate)));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<TNode, TNode, WhereVerdictSelector<TNode>>(
            source.GetAsyncBreadthFirstTreenumerator, new WhereVerdictSelector<TNode>(predicate)));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<TNode, TNode, PositionalWhereVerdictSelector<TNode>>(
            source.GetAsyncBreadthFirstTreenumerator, new PositionalWhereVerdictSelector<TNode>(predicate)));
    }

  }
}
