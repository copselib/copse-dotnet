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
    /// Async <c>PruneBefore</c>: removes each matching node AND its whole subtree (prune
    /// polarity: true = prune). Deferred. Signature still context-shaped; migrates to the
    /// (node) / (node, position) pair with the prune signature workstream
    /// (docs/OPERATOR_FUSION_DESIGN.md).
    /// </summary>
    public static IAsyncTreenumerable<T> PruneBefore<T>(
      this IAsyncTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return new FusedTreenumerable<T, T>(source, PruneBeforeVerdict(predicate), containsRelabelingStage: true);
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<T, T>(
            source.GetAsyncDepthFirstTreenumerator, PruneBeforeVerdict(predicate)));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<T, T>(
            source.GetAsyncBreadthFirstTreenumerator, PruneBeforeVerdict(predicate)));
    }

    // PruneBefore's predicate means "prune when true"; the verdict vocabulary makes the removal
    // semantics explicit where the old Where-with-inverted-predicate hid them.
    private static Func<NodeContext<T>, FusionVerdict<T>> PruneBeforeVerdict<T>(Func<NodeContext<T>, bool> predicate)
      => nodeContext =>
        predicate(nodeContext)
          ? FusionVerdict<T>.Reject(NodeTraversalStrategies.SkipNodeAndDescendants)
          : FusionVerdict<T>.Accept(nodeContext.Node);
  }
}
