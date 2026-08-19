using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  /// <summary>
  /// The canonical projection treenumerable: a source and a value selector, applied per
  /// node with the visit stream forwarded unchanged (positions never move under a
  /// projection). This is the type the <c>Select</c> operator builds, made PUBLIC
  /// (design-docs/PUBLIC_COMPOSITION_SURFACE_DESIGN.md) as the citizenship's canonical
  /// vehicle: a citizen whose own type cannot vary its output parameter absorbs a
  /// projection by returning <c>new AsyncSelectTreenumerable(sourceOrSelf,
  /// composedSelector)</c> -- and chains over the result still collapse to this ONE
  /// wrapper, because its own doors compose selectors instead of stacking.
  /// </summary>
  public sealed partial class AsyncSelectTreenumerable<TSource, TResult> : IAsyncSelectWhereTreenumerable<TResult>
  {
    /// <summary>Wraps <paramref name="source"/> with a per-node projection.</summary>
    public AsyncSelectTreenumerable(
      IAsyncTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      if (selector == null)
        throw new ArgumentNullException(nameof(selector));

      _Source = source;
      _Selector = nodeContext => selector(nodeContext.Node);
    }

    // The context-shaped recipe seat (internal: the operators' positional flavors ride it).
    internal AsyncSelectTreenumerable(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
    {
      _Source = source;
      _Selector = selector;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;
    private readonly Func<NodeContext<TSource>, TResult> _Selector;

    // ---- The internal algebra, explicitly implemented: the public surface of this class is
    // its constructor and its public doors; the driver recipe stays internal ----

    // A projection never moves a label, so the position-reading doors ARE the blind doors.
    IAsyncTreenumerable<TOuterResult> IAsyncSelectWhereTreenumerable<TResult>.ComposePositional<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
      => ((IAsyncSelectWhereTreenumerable<TResult>)this).Compose(selector);

    IAsyncTreenumerable<TOuterResult> IAsyncSelectWhereTreenumerable<TResult>.ComposePositional<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      => ((IAsyncSelectWhereTreenumerable<TResult>)this).Compose<TOuterResult, TOuterSelector>(outerSelector, relabels);

    // The fast path: a projection composed onto a projection is still a projection, so the
    // chain keeps the light acquisition.
    IAsyncTreenumerable<TOuterResult> IAsyncSelectWhereTreenumerable<TResult>.Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      return new AsyncSelectTreenumerable<TSource, TOuterResult>(
        _Source, SelectWhereComposition.SelectThenSelect(_Selector, selector));
    }

    // A prune-after joins: promote to the middle tier (light passthrough driver), never the
    // filter driver.
    IAsyncTreenumerable<TResult> IAsyncSelectWhereTreenumerable<TResult>.ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
    {
      return new AsyncSelectPruneAfterTreenumerable<TSource, TResult>(
        _Source, SelectWhereComposition.SelectThenPruneAfter(_Selector, predicate));
    }

    // A rejecting operator splices over this wrapper: the projection is donated as an
    // inlinable STRUCT leg (the user lambda staying a leaf call), so the composed chain the
    // driver ends up holding is delegate-free plumbing. A leg donated as a bare Func would
    // de-inline the whole chain -- measured, and the reason the struct seam exists
    // (design-docs/OPERATOR_COMPOSITION_DESIGN.md).
    IAsyncTreenumerable<TOuterResult> IAsyncSelectWhereTreenumerable<TResult>.Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
    {
      return new AsyncSelectWhereTreenumerable<TSource, TOuterResult, ComposedResultSelector<TSource, TResult, TOuterResult, SelectResultSelector<TSource, TResult>, TOuterSelector>>(
        _Source,
        new ComposedResultSelector<TSource, TResult, TOuterResult, SelectResultSelector<TSource, TResult>, TOuterSelector>(
          new SelectResultSelector<TSource, TResult>(_Selector), outerSelector));
    }

    /// <inheritdoc/>
    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _Selector);

    /// <inheritdoc/>
    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _Selector);
  }
}
