using Copse.Async;
using Copse.Async.Stores;
using Copse.Async.Treenumerables;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using Copse.Linq.Async.Stores;
using Copse.Linq.Async.Treenumerables;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Selects the subtrees rooted at the matching nodes: each match re-roots as a root of the
    /// result forest, its subtree intact -- depth compresses by the match's depth, descendants
    /// keep their sibling indices, and the result's roots take the matches' source preorder
    /// order (sibling indices 0, 1, 2, ...). OUTERMOST MATCH WINS, as a rule (ratified
    /// 2026-08-06): inside a matched subtree the predicate never fires, so a nested match is
    /// simply part of its outer match's tree -- a tree cannot share substructure, so nested
    /// matches must be suppressed, not absorbed. (The dag analog, TakeSubgraphsWhere on
    /// experimental/dag, needs no such rule -- there the closure union makes outermost
    /// emergent; this operator is its tree restriction.) Per-match extraction is a single-node
    /// predicate; there is no upward variant on trees (a subtree-toward-the-root is a branch,
    /// not a tree).
    ///
    /// <para>This overload serves the full composite, so it returns an
    /// <see cref="IAsyncTreenumerableBuffer{TValue}"/>: the result forest's breadth-first
    /// dimension cannot stream (matches start at different source depths, so result level k
    /// interleaves source levels -- the reorder wall), and the buffer disclosure is the honest
    /// cost. The build walks the source DEPTH-FIRST once and stores only the matched subtrees
    /// -- O(result) storage, not O(source). Deferred: construction is pinned to the first
    /// treenumerator acquisition. Depth-first-only consumers who want the O(1)-state streaming
    /// form take the <see cref="IAsyncDepthFirstTreenumerable{TValue}"/> overload via the
    /// narrow cast (the dimension-choice idiom).</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> TakeSubtreesWhere<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
      => TakeSubtreesWhereBuffer(source, ToContextPredicate(predicate));

    /// <summary>The positional flavor (the Select/Where arity-split grammar): the node's value and its SOURCE position.</summary>
    public static IAsyncTreenumerableBuffer<TNode> TakeSubtreesWhere<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
      => TakeSubtreesWhereBuffer(source, ToContextPredicate(predicate));

    /// <summary>
    /// The depth-first streaming form: a matched subtree is one CONTIGUOUS segment of the
    /// depth-first visit stream, so the narrow arm is a pass-through wrapper with an
    /// in-subtree flag and O(1) state -- no capture, fully lazy.
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TNode> TakeSubtreesWhere<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
      => TakeSubtreesWhereCore(source, ToContextPredicate(predicate));

    public static IAsyncDepthFirstTreenumerable<TNode> TakeSubtreesWhere<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
      => TakeSubtreesWhereCore(source, ToContextPredicate(predicate));

    /// <summary>
    /// The breadth-first-only source overload -- the DISCLOSURE RULE's escalation written
    /// once, here: the depth-first walk the build needs cannot come from a level-order
    /// arrival, so the source is captured (Materialize) and the build walks the capture's
    /// depth-first replay. Cost class: the capture is O(source); the result store is
    /// O(result).
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> TakeSubtreesWhere<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
      => TakeSubtreesWhereBufferFromBreadthFirst(source, ToContextPredicate(predicate));

    public static IAsyncTreenumerableBuffer<TNode> TakeSubtreesWhere<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
      => TakeSubtreesWhereBufferFromBreadthFirst(source, ToContextPredicate(predicate));

    private static Func<NodeContext<TNode>, bool> ToContextPredicate<TNode>(Func<TNode, bool> predicate)
    {
      if (predicate == null)
        throw new ArgumentNullException(nameof(predicate));

      return nodeContext => predicate(nodeContext.Node);
    }

    private static Func<NodeContext<TNode>, bool> ToContextPredicate<TNode>(Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        throw new ArgumentNullException(nameof(predicate));

      return nodeContext => predicate(nodeContext.Node, nodeContext.Position);
    }

    private static IAsyncDepthFirstTreenumerable<TNode> TakeSubtreesWhereCore<TNode>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => AsyncTreenumerableFactory.CreateDepthFirst(
        () => new AsyncTakeSubtreesWhereTreenumerator<TNode>(
          source.GetAsyncDepthFirstTreenumerator,
          predicate));

    private static IAsyncTreenumerableBuffer<TNode> TakeSubtreesWhereBuffer<TNode>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => new AsyncTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(() =>
        {
          var store = new AsyncLazyPreorderStore<TNode>(() => BuildTakeSubtreesWhereAsync(source, predicate));
          return new AsyncPreorderTreenumerable<TNode, AsyncLazyPreorderStore<TNode>>(store);
        }),
        BufferLayout.Preorder);

    private static IAsyncTreenumerableBuffer<TNode> TakeSubtreesWhereBufferFromBreadthFirst<TNode>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => new AsyncTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(() =>
        {
          var store = new AsyncLazyPreorderStore<TNode>(() => BuildTakeSubtreesWhereFromBreadthFirstAsync(source, predicate));
          return new AsyncPreorderTreenumerable<TNode, AsyncLazyPreorderStore<TNode>>(store);
        }),
        BufferLayout.Preorder);

    // The level-order arrival cannot afford the depth-first walk the build needs, so the
    // source is captured once and the build walks the capture's depth-first replay.
    private static async ValueTask<AsyncPreorderArrayStore<TNode>> BuildTakeSubtreesWhereFromBreadthFirstAsync<TNode>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      var capture = await source.MaterializeAsync().ConfigureAwait(false);

      return await BuildTakeSubtreesWhereAsync(capture, predicate).ConfigureAwait(false);
    }

    // One depth-first pass, scheduling visits only: outside a match every node is tested;
    // inside, contexts append to the flat preorder arrays and subtree sizes close by the same
    // depth arithmetic as the streaming wrapper's flag (the outermost rule: no re-testing
    // inside). Storage is the RESULT's size -- unmatched regions never land.
    private static async ValueTask<AsyncPreorderArrayStore<TNode>> BuildTakeSubtreesWhereAsync<TNode>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      var values = new List<TNode>();
      var subtreeSizes = new List<int>();
      var openIndexes = new Stack<int>(); // open ancestors within the current matched subtree
      var matchDepth = -1;

      void Close()
      {
        var openIndex = openIndexes.Pop();

        subtreeSizes[openIndex] = values.Count - openIndex;
      }

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          var depth = treenumerator.Position.Depth;

          if (matchDepth >= 0)
          {
            if (depth > matchDepth)
            {
              // Still inside: close completed deeper nodes, then take this one.
              while (openIndexes.Count > depth - matchDepth)
                Close();

              openIndexes.Push(values.Count);
              values.Add(treenumerator.Node);
              subtreeSizes.Add(0);
              continue;
            }

            // Left the subtree; this node is outside and falls through to the test.
            while (openIndexes.Count > 0)
              Close();

            matchDepth = -1;
          }

          if (!predicate(treenumerator.ToNodeContext()))
            continue;

          matchDepth = depth;
          openIndexes.Push(values.Count);
          values.Add(treenumerator.Node);
          subtreeSizes.Add(0);
        }
      }

      while (openIndexes.Count > 0)
        Close();

      return new AsyncPreorderArrayStore<TNode>(values.ToArray(), subtreeSizes.ToArray());
    }
  }
}
