using Copse.Core;
using Copse.Linq.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>PruneDescendantsWhere</c> over node VALUES: keeps each matching node but sheds its subtree
    /// (the matched node is the deepest of its lineage kept). Deferred. PruneDescendantsWhere is
    /// label-preserving: survivors keep their coordinates.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneDescendantsWhere<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      // ONE sniff (design-docs/PUBLIC_COMPOSITION_SURFACE_DESIGN.md): every member's
      // ComposePruneDescendantsWhere is that member's best machinery, and prune-afters compose in-tier
      // only -- the light tier merges in-tier and keeps no-promotion machinery; every other
      // internal member STACKS the light wrapper over itself, since joining would demote its
      // representation for a layer that costs almost nothing; foreign citizens absorb into
      // their recipe.
      if (source is IAsyncPruneDescendantsWhereTreenumerable<TNode> pruneComposableSource)
        return pruneComposableSource.ComposePruneDescendantsWhere(predicate);

      return new AsyncPruneDescendantsWhereTreenumerable<TNode>(source, nodeAndPosition => predicate(nodeAndPosition.Node));
    }

    /// <summary>
    /// Async <c>PruneDescendantsWhere</c> over (node, position). Deferred. The positional predicate sees
    /// ITS input tree's labels.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneDescendantsWhere<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The positional flavor takes the wrapper over citizens (the value-only contract
      // rule); the internal context-shaped door dispatches per member -- the light tier
      // merges in-tier (it never relabels, so the positional flavor always qualifies),
      // everyone else stacks. Stacking preserves the join rule by construction: the
      // stacked predicate reads its input tree's emitted labels.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePruneDescendantsWhere(nodeAndPosition => predicate(nodeAndPosition.Node, nodeAndPosition.Position));

      return new AsyncPruneDescendantsWhereTreenumerable<TNode>(source, nodeAndPosition => predicate(nodeAndPosition.Node, nodeAndPosition.Position));
    }

    /// <summary>
    /// Async <c>PruneDescendantsWhere</c> over node VALUES: keeps each matching node but sheds its subtree
    /// (the matched node is the deepest of its lineage kept). Deferred. PruneDescendantsWhere is
    /// label-preserving: survivors keep their coordinates.
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TNode> PruneDescendantsWhere<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The narrow probes mirror the composite overload's (the context-shaped door
      // dispatches per member). A composite-width wrapper arriving through a narrow-typed
      // receiver composes on its own representation -- the successor keeps both dimensions;
      // a narrow chain composes to a narrow successor.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePruneDescendantsWhere(nodeAndPosition => predicate(nodeAndPosition.Node));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TNode> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.ComposePruneDescendantsWhere(nodeAndPosition => predicate(nodeAndPosition.Node));

      return new AsyncPruneDescendantsWhereDepthFirstTreenumerable<TNode>(source, nodeAndPosition => predicate(nodeAndPosition.Node));
    }

    /// <summary>
    /// Async <c>PruneDescendantsWhere</c> over (node, position). Deferred. The positional predicate sees
    /// ITS input tree's labels.
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TNode> PruneDescendantsWhere<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The light tier never relabels, so the positional flavor always qualifies for the
      // join rule; stacked members preserve it by construction.
      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePruneDescendantsWhere(nodeAndPosition => predicate(nodeAndPosition.Node, nodeAndPosition.Position));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TNode> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.ComposePruneDescendantsWhere(nodeAndPosition => predicate(nodeAndPosition.Node, nodeAndPosition.Position));

      return new AsyncPruneDescendantsWhereDepthFirstTreenumerable<TNode>(source, nodeAndPosition => predicate(nodeAndPosition.Node, nodeAndPosition.Position));
    }

    /// <summary>
    /// Async <c>PruneDescendantsWhere</c> over node VALUES: keeps each matching node but sheds its subtree
    /// (the matched node is the deepest of its lineage kept). Deferred. PruneDescendantsWhere is
    /// label-preserving: survivors keep their coordinates.
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> PruneDescendantsWhere<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePruneDescendantsWhere(nodeAndPosition => predicate(nodeAndPosition.Node));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TNode> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.ComposePruneDescendantsWhere(nodeAndPosition => predicate(nodeAndPosition.Node));

      return new AsyncPruneDescendantsWhereBreadthFirstTreenumerable<TNode>(source, nodeAndPosition => predicate(nodeAndPosition.Node));
    }

    /// <summary>
    /// Async <c>PruneDescendantsWhere</c> over (node, position). Deferred. The positional predicate sees
    /// ITS input tree's labels.
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> PruneDescendantsWhere<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<TNode> selectWhereSource)
        return selectWhereSource.ComposePruneDescendantsWhere(nodeAndPosition => predicate(nodeAndPosition.Node, nodeAndPosition.Position));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TNode> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.ComposePruneDescendantsWhere(nodeAndPosition => predicate(nodeAndPosition.Node, nodeAndPosition.Position));

      return new AsyncPruneDescendantsWhereBreadthFirstTreenumerable<TNode>(source, nodeAndPosition => predicate(nodeAndPosition.Node, nodeAndPosition.Position));
    }
  }
}
