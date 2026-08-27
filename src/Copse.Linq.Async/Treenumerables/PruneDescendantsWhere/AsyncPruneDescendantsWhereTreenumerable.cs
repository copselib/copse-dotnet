using Copse.Linq.Treenumerables;
using Copse.Linq.Treenumerators;
using Copse.Core;
using Copse.Linq; // the sync transform needs the mapped using to resolve the treenumerator
using System;

namespace Copse.Linq
{
  /// <summary>
  /// The canonical prune-after treenumerable: a source and a predicate; each matching node
  /// is kept and its subtree shed (via the consumer protocol's <c>PruneDescendants</c> --
  /// no promotion, no relabeling: survivors keep their coordinates). This is the type the
  /// <c>PruneDescendantsWhere</c> operator builds, made PUBLIC
  /// (design-docs/PUBLIC_COMPOSITION_SURFACE_DESIGN.md) as the prune citizenship's
  /// canonical vehicle: a citizen whose own walk cannot absorb the predicate returns
  /// <c>new AsyncPruneDescendantsWhereTreenumerable(sourceOrSelf, predicate)</c>, and further
  /// prune-afters merge into this ONE wrapper by predicate union.
  /// </summary>
  public sealed partial class AsyncPruneDescendantsWhereTreenumerable<TNode> : IAsyncSelectWhereTreenumerable<TNode>
  {
    /// <summary>Wraps <paramref name="source"/> with a prune-after predicate.</summary>
    public AsyncPruneDescendantsWhereTreenumerable(
      IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      if (predicate == null)
        throw new ArgumentNullException(nameof(predicate));

      _Source = source;
      _Predicate = nodeAndPosition => predicate(nodeAndPosition.Node);
    }

    // The context-shaped recipe seat (internal: the operators' positional flavors ride it).
    internal AsyncPruneDescendantsWhereTreenumerable(
      IAsyncTreenumerable<TNode> source,
      Func<NodeAndPosition<TNode>, bool> predicate)
    {
      _Source = source;
      _Predicate = predicate;
    }

    private readonly IAsyncTreenumerable<TNode> _Source;
    private readonly Func<NodeAndPosition<TNode>, bool> _Predicate;

    // ---- The internal algebra, explicitly implemented: the public surface of this class is
    // its constructor and its public doors; the driver recipe stays internal ----

    // A prune-after never moves a label, so the position-reading doors ARE the blind doors.
    IAsyncTreenumerable<TOuterResult> IAsyncSelectWhereTreenumerable<TNode>.ComposePositional<TOuterResult>(Func<NodeAndPosition<TNode>, TOuterResult> selector)
      => ((IAsyncSelectWhereTreenumerable<TNode>)this).Compose(selector);

    IAsyncTreenumerable<TOuterResult> IAsyncSelectWhereTreenumerable<TNode>.ComposePositional<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      => ((IAsyncSelectWhereTreenumerable<TNode>)this).Compose<TOuterResult, TOuterSelector>(outerSelector, relabels);

    // A rejecting operator splices over this wrapper: the predicate rides its own struct
    // leaf, so this wrapper's donation is delegate-free plumbing (one leaf lambda, as always).
    IAsyncTreenumerable<TOuterResult> IAsyncSelectWhereTreenumerable<TNode>.Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
    {
      return new AsyncSelectWhereTreenumerable<TNode, TOuterResult, AsyncComposedResultSelector<TNode, TNode, TOuterResult, AsyncPruneDescendantsWhereResultSelector<TNode>, TOuterSelector>>(
        _Source,
        new AsyncComposedResultSelector<TNode, TNode, TOuterResult, AsyncPruneDescendantsWhereResultSelector<TNode>, TOuterSelector>(
          new AsyncPruneDescendantsWhereResultSelector<TNode>(_Predicate), outerSelector));
    }

    // PruneDescendantsWhere over PruneDescendantsWhere stays on the bespoke driver: the pair merges into ONE
    // wrapper by predicate union.
    IAsyncTreenumerable<TNode> IAsyncSelectWhereTreenumerable<TNode>.ComposePruneDescendantsWhere(Func<NodeAndPosition<TNode>, bool> outerPredicate)
    {
      return new AsyncPruneDescendantsWhereTreenumerable<TNode>(
        _Source, AsyncSelectWhereComposition.PruneDescendantsWhereThenPruneDescendantsWhere(_Predicate, outerPredicate));
    }

    // A projection joins: promote to the middle tier (light passthrough driver), never the
    // filter driver.
    IAsyncTreenumerable<TOuterResult> IAsyncSelectWhereTreenumerable<TNode>.Compose<TOuterResult>(Func<NodeAndPosition<TNode>, TOuterResult> selector)
    {
      return new AsyncSelectPruneDescendantsWhereTreenumerable<TNode, TOuterResult>(
        _Source, AsyncSelectWhereComposition.PruneDescendantsWhereThenSelect(_Predicate, selector));
    }

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncPruneDescendantsWhereTreenumerator<TNode>(_Source.GetAsyncBreadthFirstTreenumerator, _Predicate);

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() =>
      new AsyncPruneDescendantsWhereTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator, _Predicate);
  }
}
