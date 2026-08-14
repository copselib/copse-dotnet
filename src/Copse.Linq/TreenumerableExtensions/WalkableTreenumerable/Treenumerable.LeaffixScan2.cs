using Copse;
using Copse.Core;
using Copse.Linq.Stores;
using Copse.Linq.Treenumerables;
using Copse.Stores;
using Copse.Treenumerables;
using System;
using System.Linq;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    // EXPERIMENT (2026-08-14, Jason's challenge): LeaffixScan reimplemented over the walker
    // tier, to test whether the tier does real operator work. Hand-written sync-only,
    // deliberately NOT in the codegen manifest -- it lives or dies by the verdict.
    //
    // The receiver is the thesis: a WALKABLE receiver already paid for adjacency, so the
    // scan folds the capture's probes in place instead of re-capturing the tree from its own
    // visit stream (which is what streaming LeaffixScan does to ANY source, captures
    // included). The incumbent's engine is the same reverse-ordinal fold on raw private
    // arrays (RunLeaffixDispatchPass); this spelling says it through the public walker
    // vocabulary -- stances, steps, foci -- and shares the incumbent's result shape
    // (LazyPreorderStore of ScanResults, subtree sizes computed by the same fold).
    //
    // Receiver assumption: handles are the capture's PREORDER ordinals (Materialize's
    // default); the descending-handle order then completes every child before its parent.
    /// <summary>
    /// The walkable-receiver leaffix scan (seed flavor): identical semantics to
    /// <c>LeaffixScan(seed, edgeAccumulator, nodeAccumulator)</c> -- the seed arrives at
    /// each leaf from the virtual fringe, the edge accumulator left-folds the children's
    /// completed accumulations in sibling order, the node accumulator folds the node in
    /// once -- computed directly over the receiver's adjacency probes, with no second
    /// capture of the source. Deferred like the incumbent: the fold runs at the result's
    /// first treenumerator acquisition.
    /// </summary>
    public static ITreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan2<TSource, TAccumulate>(
      this ITreenumerableBuffer<TSource> source,
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
    {
      var surveyed = new LazyPreorderStore<ScanResult<TSource, TAccumulate>>(
        // The receiver-smart split (Materialize's `is` idiom, one level deeper): a
        // preorder-settled concrete buffer hands over its raw store and the fold is span
        // arithmetic -- no probes, no child index, not even the incumbent's positions
        // build. Any other walkable capture (a memo, a foreign implementation) takes the
        // probe fold: the same algorithm in the public vocabulary.
        () => source is TreenumerableBuffer<TSource> concreteBuffer && concreteBuffer.TryGetPreorderStore(out var store)
          ? SpanLeaffix(store, seed, edgeAccumulator, nodeAccumulator)
          : WalkerLeaffix(source, seed, edgeAccumulator, nodeAccumulator));

      return new TreenumerableBuffer<ScanResult<TSource, TAccumulate>>(
        new PreorderTreenumerable<ScanResult<TSource, TAccumulate>, LazyPreorderStore<ScanResult<TSource, TAccumulate>>>(surveyed),
        BufferLayout.Preorder);
    }

    // The fast path: the same reverse-ordinal fold as raw span arithmetic over the store's
    // own facts (Count / GetValue / GetSubtreeSize). Children need no index in preorder --
    // the first child is handle + 1, each next sibling is a subtree-size hop, and the
    // node's span end fences the walk. This is the incumbent's engine minus its child-index
    // and positions builds, reading the skeleton the receiver already owns.
    private static PreorderArrayStore<ScanResult<TSource, TAccumulate>> SpanLeaffix<TSource, TAccumulate>(
      PreorderArrayStore<TSource> store,
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
    {
      var nodeCount = store.Count;

      var accumulates = new TAccumulate[nodeCount];
      var subtreeSizes = new int[nodeCount];
      var results = new ScanResult<TSource, TAccumulate>[nodeCount];

      for (var handle = nodeCount - 1; handle >= 0; handle--)
      {
        var subtreeSize = store.GetSubtreeSize(handle);
        var reduced = seed;

        if (subtreeSize > 1)
        {
          var spanEnd = handle + subtreeSize;
          var child = handle + 1;

          reduced = accumulates[child];

          for (child += store.GetSubtreeSize(child); child < spanEnd; child += store.GetSubtreeSize(child))
            reduced = edgeAccumulator(reduced, accumulates[child]);
        }

        var node = store.GetValue(handle);

        accumulates[handle] = nodeAccumulator(reduced, node);
        subtreeSizes[handle] = subtreeSize;
        results[handle] = new ScanResult<TSource, TAccumulate>(node, accumulates[handle]);
      }

      return new PreorderArrayStore<ScanResult<TSource, TAccumulate>>(results, subtreeSizes);
    }

    // The whole fold, in walker vocabulary: stand at each handle in reverse preorder
    // (children complete before their parent by construction), step through the children
    // reducing their finished accumulations, fold the node in, record the subtree size the
    // result store needs. One pass, no capture, no walk state.
    private static PreorderArrayStore<ScanResult<TSource, TAccumulate>> WalkerLeaffix<TSource, TAccumulate>(
      ITreenumerableBuffer<TSource> source,
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
    {
      var nodeCount = source.GetHandles().Count();

      var accumulates = new TAccumulate[nodeCount];
      var subtreeSizes = new int[nodeCount];
      var results = new ScanResult<TSource, TAccumulate>[nodeCount];

      for (var handle = nodeCount - 1; handle >= 0; handle--)
      {
        var stance = source.GetTreeWalkerAt(handle);

        var reduced = seed;
        var subtreeSize = 1;
        var step = stance.MoveToChild(0);

        for (var childIndex = 1; step.HasWalker; childIndex++)
        {
          var child = step.Walker.Focus;

          reduced = childIndex == 1 ? accumulates[child] : edgeAccumulator(reduced, accumulates[child]);
          subtreeSize += subtreeSizes[child];

          step = stance.MoveToChild(childIndex);
        }

        var node = stance.GetValue();

        accumulates[handle] = nodeAccumulator(reduced, node);
        subtreeSizes[handle] = subtreeSize;
        results[handle] = new ScanResult<TSource, TAccumulate>(node, accumulates[handle]);
      }

      return new PreorderArrayStore<ScanResult<TSource, TAccumulate>>(results, subtreeSizes);
    }
  }
}
