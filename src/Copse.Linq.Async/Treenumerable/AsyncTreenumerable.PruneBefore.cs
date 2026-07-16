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

      // Prune when true; a whole-subtree removal is just another verdict stage, so it splices
      // onto any fused chain (the .Where(...).PruneBefore(...) direction included).
      if (source is IAsyncFusableTreenumerable<T> fusableSource)
        return fusableSource.FuseStage(
          nodeContext => predicate(nodeContext)
            ? FusionVerdict<T>.Reject(NodeTraversalStrategies.SkipNodeAndDescendants)
            : FusionVerdict<T>.Accept(nodeContext.Node),
          stageRelabels: true);

      return FusedTreenumerable.Create<T, T, PruneBeforeVerdictSelector<T>>(
        source, new PruneBeforeVerdictSelector<T>(predicate), containsRelabelingStage: true);
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<T, T, PruneBeforeVerdictSelector<T>>(
            source.GetAsyncDepthFirstTreenumerator, new PruneBeforeVerdictSelector<T>(predicate)));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<T, T, PruneBeforeVerdictSelector<T>>(
            source.GetAsyncBreadthFirstTreenumerator, new PruneBeforeVerdictSelector<T>(predicate)));
    }

  }
}
