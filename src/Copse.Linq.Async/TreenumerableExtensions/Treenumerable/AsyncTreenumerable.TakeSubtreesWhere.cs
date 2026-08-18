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
    /// <para>THE SCAN SPELLING (2026-08-17, the layering north star's first landing --
    /// design-docs/SELECT_INTO_CAPTURES_DESIGN.md section 5): "keep this node" is the rootfix
    /// fold fact <c>kept(parent) || predicate(node)</c>, so the operator IS
    /// <c>RootfixScan(false, fold).Where(pair =&gt; pair.Accumulate).Select(pair =&gt; pair.Node)</c>
    /// -- and the outermost rule falls out of the fold (inside a kept region the disjunction
    /// short-circuits; the predicate result is simply irrelevant there, so suppression needs
    /// no flag). The former buffer arm is retired -- its "the result's BFT cannot stream"
    /// rationale was disproven by the general Where machinery, whose breadth-first wrapper
    /// produces the re-rooted forest's true level order by pulling its inner ahead through
    /// its queue.</para>
    ///
    /// <para>DIMENSION-DISPATCHED BEHIND THE CITIZENSHIP (the honest-streaming-baseline
    /// rule): the result is a streaming-tier citizen whose recipe is (source, predicate);
    /// each acquisition constructs that dimension's leanest streaming machinery --
    /// depth-first the bespoke O(1)-state pass-through wrapper (the scan chain measured
    /// ~2.3x it for the same work), breadth-first the Where machinery in subtree mode (the
    /// subtree stage: kept-region membership read off the skip prefix the machinery already
    /// carries -- no scan engine, no pair). Because the dispatch lives BEHIND the
    /// citizenship, the operator is not a composition seam: a following Select composes
    /// (the product variant), a following Where joins the one driver over the citizen.</para>
    ///
    /// <para>Streaming semantics follow: the predicate re-fires per drain (the re-enumeration
    /// contract -- Materialize is the consumer's pin). BREAKING (pre-beta): this overload
    /// returned an <see cref="IAsyncTreenumerableBuffer{TValue}"/> through 2026-08-17 --
    /// consumers who relied on the capture add <c>.Materialize()</c>.</para>
    /// </summary>
    public static IAsyncTreenumerable<TNode> TakeSubtreesWhere<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
      => new AsyncTakeSubtreesWhereTreenumerable<TNode>(source, ToContextPredicate(predicate));

    /// <summary>
    /// The positional flavor (the Select/Where arity-split grammar): the node's value and its
    /// SOURCE position. The citizen's recipe is a context predicate, so both flavors are the
    /// same citizen -- this one just reads the position off the context.
    /// </summary>
    public static IAsyncTreenumerable<TNode> TakeSubtreesWhere<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
      => new AsyncTakeSubtreesWhereTreenumerable<TNode>(source, ToContextPredicate(predicate));

    /// <summary>
    /// The depth-first streaming form: a matched subtree is one CONTIGUOUS segment of the
    /// depth-first visit stream, so the narrow arm keeps its bespoke pass-through wrapper
    /// with an in-subtree flag and O(1) state -- strictly leaner than the scan chain's
    /// O(depth), and narrow results are outside the citizenship anyway (narrowing deferred).
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
    /// The breadth-first-only source overload: the scan chain STREAMS the narrow dimension
    /// (scan, filter, and projection all carry breadth-first narrow overloads), so the old
    /// disclosure-rule escalation -- Materialize the source, walk the capture -- is retired
    /// with the buffer arms. BREAKING (pre-beta): returned a buffer through 2026-08-17.
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> TakeSubtreesWhere<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        throw new ArgumentNullException(nameof(predicate));

      return source
        .RootfixScan(false, (kept, node) => kept || predicate(node))
        .Where(pair => pair.Accumulate)
        .Select(pair => pair.Node);
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> TakeSubtreesWhere<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        throw new ArgumentNullException(nameof(predicate));

      return AsyncTree.CreateBreadthFirst(
          () => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, bool>(
            source.GetAsyncBreadthFirstTreenumerator,
            (parentContext, nodeContext) => parentContext.Node || predicate(nodeContext.Node, nodeContext.Position),
            false))
        .Where(pair => pair.Accumulate)
        .Select(pair => pair.Node);
    }

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
      => AsyncTree.CreateDepthFirst(
        () => new AsyncTakeSubtreesWhereTreenumerator<TNode>(
          source.GetAsyncDepthFirstTreenumerator,
          predicate));
  }
}
