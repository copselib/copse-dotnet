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
    /// Async <c>Select</c> over node VALUES: maps each node, forwarding the visit stream
    /// unchanged (positions never move under a projection). Deferred. Consecutive selects
    /// collapse by selector composition, and a following Where (either flavor) composes into
    /// the projection-carrying filter driver (design-docs/OPERATOR_COMPOSITION_DESIGN.md).
    ///
    /// <para>THE SELECTOR MUST BE PURE -- its invocation count is deliberately UNSPECIFIED
    /// along two axes: COMPOSITION (a following Where collapses it to once per tested node, where
    /// the uncomposed wrapper projects per pulled visit) and the CONSUMER's pull pattern (a
    /// value drain pulls scheduling-only, so the wrapper LOOKS once-per-node; a structural
    /// drain pulls the full visit stream and re-projects per visit). An impure selector's
    /// effect count therefore silently changes with the operators AFTER it and the drain at
    /// the END of the chain (pinned by CompositionTests and DoLandingCompositionTests; the
    /// freedom is what lets the composition machinery evolve). Effects belong in <c>Do</c>, the
    /// composition barrier with the exact per-visit contract: to LAND aggregation results on
    /// mutable nodes, use the landing idiom -- <c>.Do(visit =&gt; { if (visit.Mode ==
    /// TreenumeratorMode.SchedulingNode) ... })</c>, deterministically once per scheduled
    /// node under every composition and every consumer (design-docs/SCANRESULT_DESIGN.md, THE
    /// DEMOTION).</para>
    /// </summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
    {
      // A value selector observes no coordinates, so it composes unconditionally. The fast
      // path first: a projection-only chain composes selectors and stays on the light
      // acquisition; anything else composes the projection as a never-rejecting selector.
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource)
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node));

      // The PUBLIC projection citizenship (SELECT_INTO_CAPTURES_DESIGN.md) -- probed BEFORE
      // the general surface since the fourth-cell door (SCAN_TIER_DESIGN.md): the scan
      // citizens now also implement ISelectWhere, and a bare Select must keep taking
      // ComposeSelect (the product ENGINE) rather than minting a fold-carrying driver.
      if (source is IAsyncSelectComposableTreenumerable<TSource> composableSource)
        return composableSource.ComposeSelect(selector);

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource)
        // The struct-composed splice (the reunification gate): the projection rides an
        // inlinable selector-struct leg instead of erasing the chain to delegates.
        return selectWhereSource.Compose<TResult, SelectResultSelector<TSource, TResult>>(
          new SelectResultSelector<TSource, TResult>(nodeContext => selector(nodeContext.Node)),
          relabels: false);

      return SelectCore(source, nodeContext => selector(nodeContext.Node));
    }

    /// <summary>
    /// <c>Select</c> over a CAPTURE: projecting a capture produces a capture -- the
    /// buffer-producer rule discloses the O(n) product in the return type, and the result
    /// keeps everything a capture affords (both dimensions, replay, the walker door).
    /// Deferred like every capture: nothing builds until the first pull.
    ///
    /// <para>The probe order (SELECT_INTO_CAPTURES_DESIGN.md): a PROJECTION CITIZEN
    /// (<see cref="IAsyncSelectComposableTreenumerableBuffer{TNode}"/>) composes the
    /// selector into its own machinery -- for a deferred scan product, into the pending
    /// build, so the un-projected intermediate never exists. Any other buffer takes the
    /// projected re-capture: one walk of the completed capture into a fresh buffer of
    /// projected values.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerableBuffer<TSource> source,
      Func<TSource, TResult> selector)
      => SelectBuffer(source, selector);

    // The thin shape (2026-08-17): a citizen composes its selector (a projected buffer over
    // a projected buffer would double-map; ComposeSelect keeps one map over the original
    // source); any other buffer becomes the first projection.
    private static IAsyncTreenumerableBuffer<TResult> SelectBuffer<TSource, TResult>(
      IAsyncTreenumerableBuffer<TSource> source,
      Func<TSource, TResult> selector)
    {
      if (source is IAsyncSelectComposableTreenumerableBuffer<TSource> citizen)
        return citizen.ComposeSelect(selector);

      return new AsyncProjectedTreenumerableBuffer<TSource, TResult>(source, selector);
    }

    /// <summary>The positional flavor over a capture: citizens are value-only (the contract's
    /// final-surface rule), so the positional projection always takes the re-capture.</summary>
    public static IAsyncTreenumerableBuffer<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerableBuffer<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
      => SelectCore(source, nodeContext => selector(nodeContext.Node, nodeContext.Position)).Materialize(BufferLayout.Preorder);

    /// <summary>
    /// Async <c>Select</c> over (node, position) -- the positional analog of LINQ's indexed
    /// Select. Positions never move under a projection, so this flavor composes exactly like
    /// the value-only one.
    /// </summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
    {
      // The join rule (see Where's positional overload): splice only over a label-preserving
      // chain; otherwise stack, so the selector reads genuinely emitted labels.
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource) // the tier never relabels, so the positional flavor always qualifies
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose<TResult, SelectResultSelector<TSource, TResult>>(
          new SelectResultSelector<TSource, TResult>(nodeContext => selector(nodeContext.Node, nodeContext.Position)),
          relabels: false);

      return SelectCore(source, nodeContext => selector(nodeContext.Node, nodeContext.Position));
    }

    private static IAsyncTreenumerable<TResult> SelectCore<TSource, TResult>(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
    {
      return new AsyncSelectTreenumerable<TSource, TResult>(source, selector);
    }

    public static IAsyncDepthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
    {
      // The narrow probes mirror the composite overload's. A composite-width wrapper arriving
      // through a narrow-typed receiver composes on its own representation -- the successor
      // keeps both dimensions; a narrow chain composes to a narrow successor.
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource)
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node));

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource)
        // The struct-composed splice (the reunification gate): the projection rides an
        // inlinable selector-struct leg instead of erasing the chain to delegates.
        return selectWhereSource.Compose<TResult, SelectResultSelector<TSource, TResult>>(
          new SelectResultSelector<TSource, TResult>(nodeContext => selector(nodeContext.Node)),
          relabels: false);

      if (source is IAsyncSelectPruneAfterDepthFirstTreenumerable<TSource> depthFirstSelectPruneAfterSource)
        return depthFirstSelectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TSource> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.Compose<TResult, SelectResultSelector<TSource, TResult>>(
          new SelectResultSelector<TSource, TResult>(nodeContext => selector(nodeContext.Node)),
          relabels: false);

      return new AsyncSelectDepthFirstTreenumerable<TSource, TResult>(
        source, nodeContext => selector(nodeContext.Node));
    }

    public static IAsyncDepthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
    {
      // The join rule (see the composite positional overload): splice only over a
      // label-preserving chain.
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource) // the tier never relabels, so the positional flavor always qualifies
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose<TResult, SelectResultSelector<TSource, TResult>>(
          new SelectResultSelector<TSource, TResult>(nodeContext => selector(nodeContext.Node, nodeContext.Position)),
          relabels: false);

      if (source is IAsyncSelectPruneAfterDepthFirstTreenumerable<TSource> depthFirstSelectPruneAfterSource) // the tier never relabels
        return depthFirstSelectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TSource> depthFirstSelectWhereSource && !depthFirstSelectWhereSource.Relabels)
        return depthFirstSelectWhereSource.Compose<TResult, SelectResultSelector<TSource, TResult>>(
          new SelectResultSelector<TSource, TResult>(nodeContext => selector(nodeContext.Node, nodeContext.Position)),
          relabels: false);

      return new AsyncSelectDepthFirstTreenumerable<TSource, TResult>(
        source, nodeContext => selector(nodeContext.Node, nodeContext.Position));
    }

    public static IAsyncBreadthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
    {
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource)
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node));

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource)
        // The struct-composed splice (the reunification gate): the projection rides an
        // inlinable selector-struct leg instead of erasing the chain to delegates.
        return selectWhereSource.Compose<TResult, SelectResultSelector<TSource, TResult>>(
          new SelectResultSelector<TSource, TResult>(nodeContext => selector(nodeContext.Node)),
          relabels: false);

      if (source is IAsyncSelectPruneAfterBreadthFirstTreenumerable<TSource> breadthFirstSelectPruneAfterSource)
        return breadthFirstSelectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TSource> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.Compose<TResult, SelectResultSelector<TSource, TResult>>(
          new SelectResultSelector<TSource, TResult>(nodeContext => selector(nodeContext.Node)),
          relabels: false);

      return new AsyncSelectBreadthFirstTreenumerable<TSource, TResult>(
        source, nodeContext => selector(nodeContext.Node));
    }

    public static IAsyncBreadthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
    {
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource) // the tier never relabels, so the positional flavor always qualifies
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose<TResult, SelectResultSelector<TSource, TResult>>(
          new SelectResultSelector<TSource, TResult>(nodeContext => selector(nodeContext.Node, nodeContext.Position)),
          relabels: false);

      if (source is IAsyncSelectPruneAfterBreadthFirstTreenumerable<TSource> breadthFirstSelectPruneAfterSource) // the tier never relabels
        return breadthFirstSelectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TSource> breadthFirstSelectWhereSource && !breadthFirstSelectWhereSource.Relabels)
        return breadthFirstSelectWhereSource.Compose<TResult, SelectResultSelector<TSource, TResult>>(
          new SelectResultSelector<TSource, TResult>(nodeContext => selector(nodeContext.Node, nodeContext.Position)),
          relabels: false);

      return new AsyncSelectBreadthFirstTreenumerable<TSource, TResult>(
        source, nodeContext => selector(nodeContext.Node, nodeContext.Position));
    }
  }
}
