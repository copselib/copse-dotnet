using Copse.Linq.Treenumerators;
using Copse;
using Copse.Core;
using Copse.Linq;
using Copse.Linq.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Selects the subtrees rooted at the matching nodes: each match re-roots as a root of the
    /// result forest, its subtree intact -- depth compresses by the match's depth, descendants
    /// keep their sibling indices, and the result's roots take the matches' source preorder
    /// order (sibling indices 0, 1, 2, ...). OUTERMOST MATCH WINS: inside a matched subtree
    /// the predicate never fires, so a nested match is simply part of its outer match's tree
    /// -- a tree cannot share substructure, so nested matches are suppressed, not absorbed.
    /// Per-match extraction is a single-node predicate; there is no upward variant on trees
    /// (a subtree-toward-the-root is a branch, not a tree).
    ///
    /// <para>Streaming: the predicate re-fires per drain (the re-enumeration contract);
    /// <c>Materialize()</c> is the consumer's pin. The result is a streaming-tier citizen,
    /// not a composition seam -- a following Select or Where joins one driver over it.</para>
    /// </summary>
    // The scan spelling (design-docs/SELECT_INTO_CAPTURES_DESIGN.md section 5): "keep this
    // node" is the rootfix fold fact kept(parent) || predicate(node), so the operator IS
    // RootfixScan(false, fold).Where(pair => pair.Accumulate).Select(pair => pair.Node) --
    // the outermost rule falls out of the fold (inside a kept region the disjunction
    // short-circuits). No buffer arm is needed: the general Where machinery's breadth-first
    // wrapper produces the re-rooted forest's true level order. Dimension-dispatched behind
    // the citizenship: depth-first acquires the bespoke O(1)-state pass-through wrapper
    // (measured ~2.3x the scan chain for the same work), breadth-first the Where machinery
    // in subtree mode (kept-region membership read off the skip prefix it already carries).
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

    /// <summary>
    /// Selects the subtrees rooted at the matching nodes: each match re-roots as a root of the
    /// result forest, its subtree intact -- depth compresses by the match's depth, descendants
    /// keep their sibling indices, and the result's roots take the matches' source preorder
    /// order (sibling indices 0, 1, 2, ...). OUTERMOST MATCH WINS: inside a matched subtree
    /// the predicate never fires, so a nested match is simply part of its outer match's tree
    /// -- a tree cannot share substructure, so nested matches are suppressed, not absorbed.
    /// Per-match extraction is a single-node predicate; there is no upward variant on trees
    /// (a subtree-toward-the-root is a branch, not a tree).
    ///
    /// <para>Streaming: the predicate re-fires per drain (the re-enumeration contract);
    /// <c>Materialize()</c> is the consumer's pin. The result is a streaming-tier citizen,
    /// not a composition seam -- a following Select or Where joins one driver over it.</para>
    /// </summary>
    // The scan spelling (design-docs/SELECT_INTO_CAPTURES_DESIGN.md section 5): "keep this
    // node" is the rootfix fold fact kept(parent) || predicate(node), so the operator IS
    // RootfixScan(false, fold).Where(pair => pair.Accumulate).Select(pair => pair.Node) --
    // the outermost rule falls out of the fold (inside a kept region the disjunction
    // short-circuits). No buffer arm is needed: the general Where machinery's breadth-first
    // wrapper produces the re-rooted forest's true level order. Dimension-dispatched behind
    // the citizenship: depth-first acquires the bespoke O(1)-state pass-through wrapper
    // (measured ~2.3x the scan chain for the same work), breadth-first the Where machinery
    // in subtree mode (kept-region membership read off the skip prefix it already carries).
    public static IAsyncDepthFirstTreenumerable<TNode> TakeSubtreesWhere<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
      => TakeSubtreesWhereCore(source, ToContextPredicate(predicate));

    /// <summary>
    /// The breadth-first-only source overload: the scan chain streams the narrow dimension
    /// (scan, filter, and projection all carry breadth-first narrow overloads), so no
    /// capture is required.
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

    /// <summary>
    /// Selects the subtrees rooted at the matching nodes: each match re-roots as a root of the
    /// result forest, its subtree intact -- depth compresses by the match's depth, descendants
    /// keep their sibling indices, and the result's roots take the matches' source preorder
    /// order (sibling indices 0, 1, 2, ...). OUTERMOST MATCH WINS: inside a matched subtree
    /// the predicate never fires, so a nested match is simply part of its outer match's tree
    /// -- a tree cannot share substructure, so nested matches are suppressed, not absorbed.
    /// Per-match extraction is a single-node predicate; there is no upward variant on trees
    /// (a subtree-toward-the-root is a branch, not a tree).
    ///
    /// <para>Streaming: the predicate re-fires per drain (the re-enumeration contract);
    /// <c>Materialize()</c> is the consumer's pin. The result is a streaming-tier citizen,
    /// not a composition seam -- a following Select or Where joins one driver over it.</para>
    /// </summary>
    // The scan spelling (design-docs/SELECT_INTO_CAPTURES_DESIGN.md section 5): "keep this
    // node" is the rootfix fold fact kept(parent) || predicate(node), so the operator IS
    // RootfixScan(false, fold).Where(pair => pair.Accumulate).Select(pair => pair.Node) --
    // the outermost rule falls out of the fold (inside a kept region the disjunction
    // short-circuits). No buffer arm is needed: the general Where machinery's breadth-first
    // wrapper produces the re-rooted forest's true level order. Dimension-dispatched behind
    // the citizenship: depth-first acquires the bespoke O(1)-state pass-through wrapper
    // (measured ~2.3x the scan chain for the same work), breadth-first the Where machinery
    // in subtree mode (kept-region membership read off the skip prefix it already carries).
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
