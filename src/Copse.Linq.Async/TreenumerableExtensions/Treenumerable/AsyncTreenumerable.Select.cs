using Copse.Core;
using Copse.Linq.Treenumerables;
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
      // ONE sniff (PUBLIC_COMPOSITION_SURFACE_DESIGN.md): a value selector observes no
      // coordinates, so it composes unconditionally, and every member's ComposeSelect is
      // that member's best machinery (the door-optimality law) -- the light wrappers stay
      // light in-tier, the driver nests a struct leg, the scan citizens re-plant into the
      // product engine, foreign citizens absorb into their recipe.
      if (source is IAsyncSelectTreenumerable<TSource> composableSource)
        return composableSource.ComposeSelect(selector);

      return SelectCore(source, nodeAndPosition => selector(nodeAndPosition.Node));
    }

    /// <summary>
    /// <c>Select</c> over a CAPTURE: projecting a capture produces a capture -- the
    /// buffer-producer rule discloses the O(n) product in the return type, and the result
    /// keeps everything a capture affords (both dimensions, replay, the walker door).
    /// Deferred like every capture: nothing builds until the first pull.
    ///
    /// <para>The probe order (SELECT_INTO_CAPTURES_DESIGN.md): a PROJECTION CITIZEN
    /// (<see cref="IAsyncSelectTreenumerableBuffer{TNode}"/>) composes the
    /// selector into its own machinery -- for a deferred scan product, into the pending
    /// build, so the un-projected intermediate never exists. Any other buffer takes the
    /// projected re-capture: one walk of the completed capture into a fresh buffer of
    /// projected values.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerableBuffer<TSource> source,
      Func<TSource, TResult> selector)
      => SelectBuffer(source, selector);

    // A citizen composes its selector -- a projected buffer over a projected buffer would
    // double-map, where ComposeSelect keeps one map over the original source; any other
    // buffer becomes the first projection.
    private static IAsyncTreenumerableBuffer<TResult> SelectBuffer<TSource, TResult>(
      IAsyncTreenumerableBuffer<TSource> source,
      Func<TSource, TResult> selector)
    {
      if (source is IAsyncSelectTreenumerableBuffer<TSource> citizen)
        return citizen.ComposeSelect(selector);

      return new AsyncProjectedTreenumerableBuffer<TSource, TResult>(source, selector);
    }

    /// <summary>The positional flavor over a capture: citizens are value-only (the contract's
    /// final-surface rule), so the positional projection always takes the re-capture.</summary>
    public static IAsyncTreenumerableBuffer<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerableBuffer<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
      => SelectCore(source, nodeAndPosition => selector(nodeAndPosition.Node, nodeAndPosition.Position)).Materialize(BufferLayout.Preorder);

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
      // chain; otherwise stack, so the selector reads genuinely emitted labels. The
      // context-shaped door dispatches per member (the door-optimality law: light stays
      // light, the driver nests a struct leg, a scan citizen's leg lands in the
      // fold-carrying driver).
      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource)
        return selectWhereSource.ComposePositional(nodeAndPosition => selector(nodeAndPosition.Node, nodeAndPosition.Position));

      return SelectCore(source, nodeAndPosition => selector(nodeAndPosition.Node, nodeAndPosition.Position));
    }

    private static IAsyncTreenumerable<TResult> SelectCore<TSource, TResult>(
      IAsyncTreenumerable<TSource> source,
      Func<NodeAndPosition<TSource>, TResult> selector)
    {
      return new AsyncSelectTreenumerable<TSource, TResult>(source, selector);
    }

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
    public static IAsyncDepthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
    {
      // The narrow probes mirror the composite overload's. A composite-width wrapper arriving
      // through a narrow-typed receiver composes on its own representation -- the successor
      // keeps both dimensions; a narrow chain composes to a narrow successor. The
      // context-shaped door dispatches per member (the door-optimality law).
      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource)
        return selectWhereSource.Compose(nodeAndPosition => selector(nodeAndPosition.Node));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TSource> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.Compose(nodeAndPosition => selector(nodeAndPosition.Node));

      return new AsyncSelectDepthFirstTreenumerable<TSource, TResult>(
        source, nodeAndPosition => selector(nodeAndPosition.Node));
    }

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
    public static IAsyncDepthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
    {
      // The join rule (see the composite positional overload): splice only over a
      // label-preserving chain. The context-shaped door dispatches per member.
      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource)
        return selectWhereSource.ComposePositional(nodeAndPosition => selector(nodeAndPosition.Node, nodeAndPosition.Position));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TSource> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.ComposePositional(nodeAndPosition => selector(nodeAndPosition.Node, nodeAndPosition.Position));

      return new AsyncSelectDepthFirstTreenumerable<TSource, TResult>(
        source, nodeAndPosition => selector(nodeAndPosition.Node, nodeAndPosition.Position));
    }

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
    public static IAsyncBreadthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
    {
      // The context-shaped door dispatches per member (the door-optimality law); see the
      // depth-first overload.
      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource)
        return selectWhereSource.Compose(nodeAndPosition => selector(nodeAndPosition.Node));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TSource> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.Compose(nodeAndPosition => selector(nodeAndPosition.Node));

      return new AsyncSelectBreadthFirstTreenumerable<TSource, TResult>(
        source, nodeAndPosition => selector(nodeAndPosition.Node));
    }

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
    public static IAsyncBreadthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
    {
      // The join rule; the context-shaped door dispatches per member. See the depth-first
      // overload.
      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource)
        return selectWhereSource.ComposePositional(nodeAndPosition => selector(nodeAndPosition.Node, nodeAndPosition.Position));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TSource> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.ComposePositional(nodeAndPosition => selector(nodeAndPosition.Node, nodeAndPosition.Position));

      return new AsyncSelectBreadthFirstTreenumerable<TSource, TResult>(
        source, nodeAndPosition => selector(nodeAndPosition.Node, nodeAndPosition.Position));
    }
  }
}
