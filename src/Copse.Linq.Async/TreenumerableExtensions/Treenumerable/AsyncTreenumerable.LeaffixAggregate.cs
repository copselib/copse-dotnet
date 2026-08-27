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
    /// sequence -- one <see cref="NodeAccumulation{TNode, TAccumulate}"/> per root tree: the root's
    /// value paired with the dual fold up from that tree's fringe (the canonical pairing,
    /// design-docs/SCANRESULT_DESIGN.md; value-flavored on the dual shape). The mechanism is
    /// LeaffixScan's: <paramref name="edgeAccumulator"/> reduces each family's completed
    /// accumulations in sibling order (first child as the start),
    /// <paramref name="nodeAccumulator"/> folds the node in once --
    /// <c>value(n) = nodeAccumulator(edgeReduce(children), n)</c> -- and at the fringe
    /// <paramref name="leafNodeSelector"/> sets each leaf's accumulation directly, the node
    /// accumulator bypassed (selector flavors only -- THE VIRTUAL-ROOT RULE, see
    /// LeaffixScan's doc; a formula-shaped fringe is
    /// <c>leaf =&gt; nodeAccumulator(x, leaf)</c>). Lazy per root -- a root is emitted the
    /// moment its subtree closes, and the flat buffers are then reused for the next root, so
    /// peak memory is the largest root subtree (not the whole forest) and a consumer that
    /// stops early traverses fewer roots. Zero per-node alloc: the fold writes straight into
    /// the flat slots.
    /// </summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> LeaffixAggregate<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TNode, TAccumulate> nodeAccumulator,
      CancellationToken cancellationToken = default)
      => LeaffixAggregateCore(source, nodeContext => leafNodeSelector(nodeContext.Node), edgeAccumulator, nodeAccumulator, cancellationToken);

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the leaf's value and its position.</summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> LeaffixAggregate<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TNode, TAccumulate> nodeAccumulator,
      CancellationToken cancellationToken = default)
      => LeaffixAggregateCore(source, nodeContext => leafNodeSelector(nodeContext.Node, nodeContext.Position), edgeAccumulator, nodeAccumulator, cancellationToken);

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
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> LeaffixAggregate<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TNode, TAccumulate> nodeAccumulator,
      CancellationToken cancellationToken = default)
      => LeaffixAggregateBreadthFirstCore(source, nodeContext => leafNodeSelector(nodeContext.Node), edgeAccumulator, nodeAccumulator, cancellationToken);

    /// <summary>
    /// The leaf-to-root accumulations (LeaffixScan collapsed to its roots), as a lazy async
    /// sequence -- one <see cref="NodeAccumulation{TNode, TAccumulate}"/> per root tree: the root's
    /// value paired with the dual fold up from that tree's fringe (the canonical pairing,
    /// design-docs/SCANRESULT_DESIGN.md; value-flavored on the dual shape). The mechanism is
    /// LeaffixScan's: <paramref name="edgeAccumulator"/> reduces each family's completed
    /// accumulations in sibling order (first child as the start),
    /// <paramref name="nodeAccumulator"/> folds the node in once --
    /// <c>value(n) = nodeAccumulator(edgeReduce(children), n)</c> -- and at the fringe
    /// <paramref name="leafNodeSelector"/> sets each leaf's accumulation directly, the node
    /// accumulator bypassed (selector flavors only -- THE VIRTUAL-ROOT RULE, see
    /// LeaffixScan's doc; a formula-shaped fringe is
    /// <c>leaf =&gt; nodeAccumulator(x, leaf)</c>). Lazy per root -- a root is emitted the
    /// moment its subtree closes, and the flat buffers are then reused for the next root, so
    /// peak memory is the largest root subtree (not the whole forest) and a consumer that
    /// stops early traverses fewer roots. Zero per-node alloc: the fold writes straight into
    /// the flat slots.
    /// </summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> LeaffixAggregate<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TNode, TAccumulate> nodeAccumulator,
      CancellationToken cancellationToken = default)
      => LeaffixAggregateBreadthFirstCore(source, nodeContext => leafNodeSelector(nodeContext.Node, nodeContext.Position), edgeAccumulator, nodeAccumulator, cancellationToken);

    /// <summary>Disambiguation overloads for full trees; keep the depth-first consumption -- the per-root-lazy entry.</summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> LeaffixAggregate<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TNode, TAccumulate> nodeAccumulator,
      CancellationToken cancellationToken = default)
      => LeaffixAggregate((IAsyncDepthFirstTreenumerable<TNode>)source, leafNodeSelector, edgeAccumulator, nodeAccumulator, cancellationToken);

    /// <summary>
    /// The leaf-to-root accumulations (LeaffixScan collapsed to its roots), as a lazy async
    /// sequence -- one <see cref="NodeAccumulation{TNode, TAccumulate}"/> per root tree: the root's
    /// value paired with the dual fold up from that tree's fringe (the canonical pairing,
    /// design-docs/SCANRESULT_DESIGN.md; value-flavored on the dual shape). The mechanism is
    /// LeaffixScan's: <paramref name="edgeAccumulator"/> reduces each family's completed
    /// accumulations in sibling order (first child as the start),
    /// <paramref name="nodeAccumulator"/> folds the node in once --
    /// <c>value(n) = nodeAccumulator(edgeReduce(children), n)</c> -- and at the fringe
    /// <paramref name="leafNodeSelector"/> sets each leaf's accumulation directly, the node
    /// accumulator bypassed (selector flavors only -- THE VIRTUAL-ROOT RULE, see
    /// LeaffixScan's doc; a formula-shaped fringe is
    /// <c>leaf =&gt; nodeAccumulator(x, leaf)</c>). Lazy per root -- a root is emitted the
    /// moment its subtree closes, and the flat buffers are then reused for the next root, so
    /// peak memory is the largest root subtree (not the whole forest) and a consumer that
    /// stops early traverses fewer roots. Zero per-node alloc: the fold writes straight into
    /// the flat slots.
    /// </summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> LeaffixAggregate<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TNode, TAccumulate> nodeAccumulator,
      CancellationToken cancellationToken = default)
      => LeaffixAggregate((IAsyncDepthFirstTreenumerable<TNode>)source, leafNodeSelector, edgeAccumulator, nodeAccumulator, cancellationToken);

    // The depth-first core, per-root lazy. The dual fold is NODE-LAST, so a slot holds the
    // RUNNING EDGE-REDUCTION of its closed children until the node itself closes (the parallel
    // hasChildren list distinguishes "no child closed yet" from any real accumulate -- no
    // sentinel value is stolen from TAccumulate's range).
    private static async IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> LeaffixAggregateCore<TNode, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TAccumulate> leafValue,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TNode, TAccumulate> nodeAccumulator,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var accumulations = new List<TAccumulate>();
      var path = new Stack<PendingNode<TNode>>();
      var currentRoot = default(TNode);

      // Both branch facts are INDEX ARITHMETIC, not stored state -- the alternatives cost
      // per-node bookkeeping (a parallel has-children list) or chain-deep fat frames
      // (flag-carrying frames): a closing node has children iff
      // anything was scheduled after it before its close, and a closing child is its parent's
      // FIRST iff it sits immediately after the parent in preorder (children close in sibling
      // order, and closes fire before the next sibling schedules).
      void Close()
      {
        var pending = path.Pop();

        var closed =
          accumulations.Count > pending.Index + 1
          ? nodeAccumulator(accumulations[pending.Index], pending.Context.Node)
          : leafValue(pending.Context);

        accumulations[pending.Index] = closed;

        if (path.Count > 0)
        {
          var parent = path.Peek();

          accumulations[parent.Index] =
            pending.Index > parent.Index + 1
            ? edgeAccumulator(accumulations[parent.Index], closed)
            : closed;
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
            yield return new NodeAccumulation<TNode, TAccumulate>(currentRoot, accumulations[0]);
            accumulations.Clear();
          }

          var nodeContext = treenumerator.ToNodeContext();

          if (depth == 0)
            currentRoot = nodeContext.Node;

          path.Push(new PendingNode<TNode>(accumulations.Count, nodeContext));
          accumulations.Add(default);
        }
      }

      while (path.Count > 0)
        Close();

      if (accumulations.Count > 0)
        yield return new NodeAccumulation<TNode, TAccumulate>(currentRoot, accumulations[0]);
    }

    // The breadth-first core: the capture is the memo's chunked level-order buffer, completed
    // in one pass -- chunked growth with NO flat-array hand-off (the factory's ToArray tripled
    // transient allocation here when measured; the buffer IS already a completed store). The
    // fold is the depth-first core's (same Close, node-last), driven by index chasing over the
    // capture's contiguous child spans instead of a visit stream; contexts are reconstructed
    // from the spans (depth from the walk stack, sibling index from the span offset).
    private static async IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> LeaffixAggregateBreadthFirstCore<TNode, TAccumulate>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TAccumulate> leafValue,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TNode, TAccumulate> nodeAccumulator,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var capture = new AsyncMemoizeLevelOrderStore<TNode>(source.GetAsyncBreadthFirstTreenumerator);
      await using (capture.ConfigureAwait(false))
      {
        await capture.CompleteAsync().ConfigureAwait(false);

        var accumulations = new List<TAccumulate>();
        var path = new Stack<PendingNode<TNode>>();

        // Index arithmetic, as in the depth-first core: the walk schedules in preorder and
        // closes before scheduling the next sibling, so both branch facts derive from indices.
        void Close()
        {
          var pending = path.Pop();

          var closed =
            accumulations.Count > pending.Index + 1
            ? nodeAccumulator(accumulations[pending.Index], pending.Context.Node)
            : leafValue(pending.Context);

          accumulations[pending.Index] = closed;

          if (path.Count > 0)
          {
            var parent = path.Peek();

            accumulations[parent.Index] =
              pending.Index > parent.Index + 1
              ? edgeAccumulator(accumulations[parent.Index], closed)
              : closed;
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

            var nodeContext = new NodeContext<TNode>(
              capture.GetNode(frame.Index), new NodePosition(frame.SiblingIndex, frame.Depth));

            path.Push(new PendingNode<TNode>(accumulations.Count, nodeContext));
            accumulations.Add(default);

            var firstChildIndex = capture.GetFirstChildIndex(frame.Index);
            var childCount = capture.GetChildCount(frame.Index);

            for (var childOffset = childCount - 1; childOffset >= 0; childOffset--)
              walk.Push((firstChildIndex + childOffset, frame.Depth + 1, childOffset));
          }

          while (path.Count > 0)
            Close();

          yield return new NodeAccumulation<TNode, TAccumulate>(capture.GetNode(root), accumulations[0]);
          accumulations.Clear();
        }
      }
    }
  }
}
