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
    /// sequence -- one value per root tree, the fold of the accumulator up from that tree's leaves.
    /// Each node accumulates over its children's accumulations (or the leaf selector at a leaf).
    /// Lazy per root -- a root is emitted the moment its subtree completes, and the flat buffers are
    /// then reused for the next root, so peak memory is the largest root subtree (not the whole
    /// forest) and a consumer that stops early traverses fewer roots. Zero per-node alloc: children
    /// are read via the no-copy ChildAccumulations view (see LeaffixScan).
    /// </summary>
    public static async IAsyncEnumerable<TAccumulate> LeaffixAggregate<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> accumulator,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var accumulations = new List<TAccumulate>();
      var subtreeSizes = new List<int>();
      var path = new Stack<PendingNode<TSource>>();

      void Close()
      {
        var pending = path.Pop();
        var index = pending.Index;
        subtreeSizes[index] = accumulations.Count - index;
        accumulations[index] =
          subtreeSizes[index] == 1
          ? leafNodeSelector(pending.Context)
          : accumulator(pending.Context, new ChildAccumulations<TAccumulate>(accumulations, subtreeSizes, index));
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
            yield return accumulations[0];
            accumulations.Clear();
            subtreeSizes.Clear();
          }

          path.Push(new PendingNode<TSource>(accumulations.Count, treenumerator.ToNodeContext()));
          accumulations.Add(default);
          subtreeSizes.Add(0);
        }
      }

      while (path.Count > 0)
        Close();

      if (accumulations.Count > 0)
        yield return accumulations[0];
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
    public static async IAsyncEnumerable<TAccumulate> LeaffixAggregate<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> accumulator,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
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

        // The depth-first entry's preorder-shaped fold (same Close, same ChildAccumulations
        // view), driven by index chasing over the capture's contiguous child spans instead of
        // a visit stream. Contexts are reconstructed from the spans: depth is the walk
        // stack's, sibling index is the offset inside the parent's span (roots: the root
        // ordinal).
        var accumulations = new List<TAccumulate>();
        var subtreeSizes = new List<int>();
        var path = new Stack<PendingNode<TSource>>();

        void Close()
        {
          var pending = path.Pop();
          var index = pending.Index;

          subtreeSizes[index] = accumulations.Count - index;
          accumulations[index] =
            subtreeSizes[index] == 1
            ? leafNodeSelector(pending.Context)
            : accumulator(pending.Context, new ChildAccumulations<TAccumulate>(accumulations, subtreeSizes, index));
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

            path.Push(new PendingNode<TSource>(
              accumulations.Count,
              new NodeContext<TSource>(capture.GetValue(frame.Index), new NodePosition(frame.SiblingIndex, frame.Depth))));
            accumulations.Add(default);
            subtreeSizes.Add(0);

            var firstChildIndex = capture.GetFirstChildIndex(frame.Index);
            var childCount = capture.GetChildCount(frame.Index);

            for (var childOffset = childCount - 1; childOffset >= 0; childOffset--)
              walk.Push((firstChildIndex + childOffset, frame.Depth + 1, childOffset));
          }

          while (path.Count > 0)
            Close();

          yield return accumulations[0];
          accumulations.Clear();
          subtreeSizes.Clear();
        }
      }
    }

    /// <summary>
    /// Disambiguation overload for full trees; keeps the depth-first consumption -- the
    /// per-root-lazy entry.
    /// </summary>
    public static IAsyncEnumerable<TAccumulate> LeaffixAggregate<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> accumulator,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      CancellationToken cancellationToken = default)
      => LeaffixAggregate((IAsyncDepthFirstTreenumerable<TSource>)source, accumulator, leafNodeSelector, cancellationToken);
  }
}
