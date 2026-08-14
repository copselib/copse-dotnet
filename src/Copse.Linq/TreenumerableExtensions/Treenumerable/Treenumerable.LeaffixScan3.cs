using Copse;
using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    // EXPERIMENT, part three (2026-08-14): the incumbent's signature with a receiver-smart
    // front door -- the shipping-shape candidate. Hand-written sync-only, not in the
    // manifest; it lives or dies with its siblings.
    /// <summary>
    /// LeaffixScan with the receiver sniff (the <c>Materialize</c> / LINQ <c>Count</c>
    /// idiom): a source that is already a non-level-order capture folds IN PLACE over its
    /// own adjacency (the span fast path, or probes for a foreign walkable) -- no second
    /// capture; every other source takes the incumbent's streaming engine verbatim. Same
    /// semantics, same deferred build, same result shape as <c>LeaffixScan</c> everywhere.
    /// A level-order capture streams: its ordinals are not preorder, and the streaming
    /// engine consumes it depth-first correctly (the cross-decode tax the dispatch build
    /// already accepted).
    /// </summary>
    public static ITreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan3<TSource, TAccumulate>(
      this IDepthFirstTreenumerable<TSource> source,
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
    {
      if (source is ITreenumerableBuffer<TSource> buffer && buffer.NativeLayout != BufferLayout.LevelOrder)
        return buffer.LeaffixScan2(seed, edgeAccumulator, nodeAccumulator);

      return new TreenumerableBuffer<ScanResult<TSource, TAccumulate>>(
        Tree.Lazy(() => PreorderDispatch(source, FullSurvey(SeededDualFoldSurvey(seed, edgeAccumulator, nodeAccumulator)))),
        BufferLayout.Preorder);
    }
  }
}
