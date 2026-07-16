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

      if (source is IAsyncFusableTreenumerable<TNode> fusableSource)
      {
        var fused = fusableSource.FuseWhere(predicate);

        if (fused != null)
          return fused;
      }

      return new FusedTreenumerable<TNode, TNode>(source, WhereVerdict(predicate), containsRelabelingStage: true);
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

      if (source is IAsyncFusableTreenumerable<TNode> fusableSource)
      {
        var fused = fusableSource.FusePositionalWhere(predicate);

        if (fused != null)
          return fused;
      }

      return new FusedTreenumerable<TNode, TNode>(source, PositionalWhereVerdict(predicate), containsRelabelingStage: true);
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
            source.GetAsyncDepthFirstTreenumerator, WhereVerdict(predicate)));
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
            source.GetAsyncDepthFirstTreenumerator, PositionalWhereVerdict(predicate)));
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
            source.GetAsyncBreadthFirstTreenumerator, WhereVerdict(predicate)));
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
            source.GetAsyncBreadthFirstTreenumerator, PositionalWhereVerdict(predicate)));
    }

    private static Func<NodeContext<TNode>, FusionVerdict<TNode>> WhereVerdict<TNode>(Func<TNode, bool> predicate)
      => nodeContext =>
        predicate(nodeContext.Node)
          ? FusionVerdict<TNode>.Accept(nodeContext.Node)
          : FusionVerdict<TNode>.Reject(NodeTraversalStrategies.SkipNode);

    private static Func<NodeContext<TNode>, FusionVerdict<TNode>> PositionalWhereVerdict<TNode>(Func<TNode, NodePosition, bool> predicate)
      => nodeContext =>
        predicate(nodeContext.Node, nodeContext.Position)
          ? FusionVerdict<TNode>.Accept(nodeContext.Node)
          : FusionVerdict<TNode>.Reject(NodeTraversalStrategies.SkipNode);
  }
}
