using Copse.Linq.Async.Stores;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The leaf-to-root accumulations (LeaffixScan collapsed to its roots), as a lazy async
    /// sequence -- one <see cref="ScanResult{TSource, TAccumulate}"/> per root tree: the root's
    /// value paired with the fold of the accumulator up from that tree's leaves (the canonical
    /// pairing, docs/SCANRESULT_DESIGN.md; callback flavors keep their NodeContext shapes until
    /// the signature workstream). Every node's accumulation starts at <paramref name="nodeSelector"/> and each
    /// child's completed accumulation is folded in by <paramref name="accumulator"/>, one child
    /// at a time in sibling order (a leaf's value is its seed unchanged; the sibling-complete
    /// shape belongs to LeaffixDispatch). Lazy per root -- a root is emitted the moment its
    /// subtree completes, and the flat buffers are then reused for the next root, so peak memory
    /// is the largest root subtree (not the whole forest) and a consumer that stops early
    /// traverses fewer roots. Zero per-node alloc: the fold writes straight into the parent's
    /// running slot.
    /// </summary>
    public static async IAsyncEnumerable<ScanResult<TSource, TAccumulate>> LeaffixAggregate<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<NodeContext<TSource>, TAccumulate, TAccumulate, TAccumulate> accumulator,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var accumulations = new List<TAccumulate>();
      var path = new Stack<PendingNode<TSource>>();
      var currentRoot = default(TSource);

      void Close()
      {
        var pending = path.Pop();

        if (path.Count > 0)
        {
          var parent = path.Peek();
          accumulations[parent.Index] = accumulator(parent.Context, accumulations[parent.Index], accumulations[pending.Index]);
        }
      }

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          var depth = treenumerator.Position.Depth;

          while (path.Count > depth)
            Close();

          if (depth == 0 && accumulations.Count > 0)
          {
            yield return new ScanResult<TSource, TAccumulate>(currentRoot, accumulations[0]);
            accumulations.Clear();
          }

          var nodeContext = treenumerator.ToNodeContext();

          if (depth == 0)
            currentRoot = nodeContext.Node;

          path.Push(new PendingNode<TSource>(accumulations.Count, nodeContext));
          accumulations.Add(nodeSelector(nodeContext));
        }
      }

      while (path.Count > 0)
        Close();

      if (accumulations.Count > 0)
        yield return new ScanResult<TSource, TAccumulate>(currentRoot, accumulations[0]);
    }

    /// <summary>
    /// The breadth-first-only entry -- a DOCUMENTED capture, the disclosure rule's amended
    /// carve-out for enumerable returns (LAZINESS_AND_BUFFERING_POLICY.md): leaffix folds
    /// children before parents, which a level-order arrival cannot afford, so the source is
    /// captured ONCE into a level-order store (on first enumeration) and the fold walks the
    /// capture's child spans directly -- an index-chasing depth-first walk, no visit stream
    /// ever decoded between the encodings. The cost class is the dimension's own: breadth-first
    /// arrival interleaves every tree in the forest, so no root's subtree closes until the
    /// whole forest drains -- peak memory is the capture, and the first value arrives only
    /// after it (the fold buffers are then reused per root, as in the depth-first entry).
    /// Per-root laziness is a depth-first affordance.
    /// </summary>
    public static async IAsyncEnumerable<ScanResult<TSource, TAccumulate>> LeaffixAggregate<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<NodeContext<TSource>, TAccumulate, TAccumulate, TAccumulate> accumulator,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      // The capture is the memo's chunked level-order buffer, completed in one pass -- chunked
      // growth with NO flat-array hand-off (the factory's ToArray tripled transient allocation
      // here when measured; the buffer IS already a completed store). The feed retires inside
      // CompleteAsync; disposal after the fold is vacuous but tidy.
      var capture = new AsyncMemoizeLevelOrderStore<TSource>(source.GetAsyncBreadthFirstTreenumerator);
      await using (capture.ConfigureAwait(false))
      {
        await capture.CompleteAsync().ConfigureAwait(false);

        // The depth-first entry's preorder-shaped fold (same Close, same fold-into-parent),
        // driven by index chasing over the capture's contiguous child spans instead of
        // a visit stream. Contexts are reconstructed from the spans: depth is the walk
        // stack's, sibling index is the offset inside the parent's span (roots: the root
        // ordinal).
        var accumulations = new List<TAccumulate>();
        var path = new Stack<PendingNode<TSource>>();

        void Close()
        {
          var pending = path.Pop();

          if (path.Count > 0)
          {
            var parent = path.Peek();
            accumulations[parent.Index] = accumulator(parent.Context, accumulations[parent.Index], accumulations[pending.Index]);
          }
        }

        // Children are pushed in reverse span order so they pop in preorder.
        var walk = new Stack<(int Index, int Depth, int SiblingIndex)>();

        for (var root = 0; root < capture.BufferedRootCount; root++)
        {
          cancellationToken.ThrowIfCancellationRequested();

          walk.Push((root, 0, root));

          while (walk.Count > 0)
          {
            var frame = walk.Pop();

            while (path.Count > frame.Depth)
              Close();

            var nodeContext = new NodeContext<TSource>(
              capture.GetValue(frame.Index), new NodePosition(frame.SiblingIndex, frame.Depth));

            path.Push(new PendingNode<TSource>(accumulations.Count, nodeContext));
            accumulations.Add(nodeSelector(nodeContext));

            var firstChildIndex = capture.GetFirstChildIndex(frame.Index);
            var childCount = capture.GetChildCount(frame.Index);

            for (var childOffset = childCount - 1; childOffset >= 0; childOffset--)
              walk.Push((firstChildIndex + childOffset, frame.Depth + 1, childOffset));
          }

          while (path.Count > 0)
            Close();

          yield return new ScanResult<TSource, TAccumulate>(capture.GetValue(root), accumulations[0]);
          accumulations.Clear();
        }
      }
    }

    /// <summary>
    /// Disambiguation overload for full trees; keeps the depth-first consumption -- the
    /// per-root-lazy entry.
    /// </summary>
    public static IAsyncEnumerable<ScanResult<TSource, TAccumulate>> LeaffixAggregate<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<NodeContext<TSource>, TAccumulate, TAccumulate, TAccumulate> accumulator,
      CancellationToken cancellationToken = default)
      => LeaffixAggregate((IAsyncDepthFirstTreenumerable<TSource>)source, nodeSelector, accumulator, cancellationToken);

    // The value-only accumulator flavor (arity-split, like Select/Where): a pure combine --
    // (runningAccumulate, childAccumulate) -- for folds that never read the folding node.
    public static IAsyncEnumerable<ScanResult<TSource, TAccumulate>> LeaffixAggregate<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator,
      CancellationToken cancellationToken = default)
      => LeaffixAggregate(source, nodeSelector, ContextBlindAccumulator<TSource, TAccumulate>(accumulator), cancellationToken);

    public static IAsyncEnumerable<ScanResult<TSource, TAccumulate>> LeaffixAggregate<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator,
      CancellationToken cancellationToken = default)
      => LeaffixAggregate(source, nodeSelector, ContextBlindAccumulator<TSource, TAccumulate>(accumulator), cancellationToken);

    public static IAsyncEnumerable<ScanResult<TSource, TAccumulate>> LeaffixAggregate<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator,
      CancellationToken cancellationToken = default)
      => LeaffixAggregate((IAsyncDepthFirstTreenumerable<TSource>)source, nodeSelector, ContextBlindAccumulator<TSource, TAccumulate>(accumulator), cancellationToken);

    private static Func<NodeContext<TSource>, TAccumulate, TAccumulate, TAccumulate> ContextBlindAccumulator<TSource, TAccumulate>(
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator)
      => (_, accumulate, childAccumulate) => accumulator(accumulate, childAccumulate);
  }
}
