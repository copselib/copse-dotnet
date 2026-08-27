using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async; // the sync transform needs the mapped using to resolve the treenumerator
using System;

namespace Copse.Linq.Async.Treenumerables
{
  /// <summary>
  /// The canonical prune-after treenumerable: a source and a predicate; each matching node
  /// is kept and its subtree shed (via the consumer protocol's <c>PruneDescendants</c> --
  /// no promotion, no relabeling: survivors keep their coordinates). This is the type the
  /// <c>PruneAfter</c> operator builds, made PUBLIC
  /// (design-docs/PUBLIC_COMPOSITION_SURFACE_DESIGN.md) as the prune citizenship's
  /// canonical vehicle: a citizen whose own walk cannot absorb the predicate returns
  /// <c>new AsyncPruneAfterTreenumerable(sourceOrSelf, predicate)</c>, and further
  /// prune-afters merge into this ONE wrapper by predicate union.
  /// </summary>
  public sealed partial class AsyncPruneAfterTreenumerable<TNode> : IAsyncSelectWhereTreenumerable<TNode>
  {
    /// <summary>Wraps <paramref name="source"/> with a prune-after predicate.</summary>
    public AsyncPruneAfterTreenumerable(
      IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      if (predicate == null)
        throw new ArgumentNullException(nameof(predicate));

      _Source = source;
      _Predicate = nodeContext => predicate(nodeContext.Node);
    }

    // The context-shaped recipe seat (internal: the operators' positional flavors ride it).
    internal AsyncPruneAfterTreenumerable(
      IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      _Source = source;
      _Predicate = predicate;
    }

    private readonly IAsyncTreenumerable<TNode> _Source;
    private readonly Func<NodeContext<TNode>, bool> _Predicate;

    // ---- The internal algebra, explicitly implemented: the public surface of this class is
    // its constructor and its public doors; the driver recipe stays internal ----

    // A prune-after never moves a label, so the position-reading doors ARE the blind doors.
    IAsyncTreenumerable<TOuterResult> IAsyncSelectWhereTreenumerable<TNode>.ComposePositional<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector)
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
      return new AsyncSelectWhereTreenumerable<TNode, TOuterResult, ComposedResultSelector<TNode, TNode, TOuterResult, PruneAfterResultSelector<TNode>, TOuterSelector>>(
        _Source,
        new ComposedResultSelector<TNode, TNode, TOuterResult, PruneAfterResultSelector<TNode>, TOuterSelector>(
          new PruneAfterResultSelector<TNode>(_Predicate), outerSelector));
    }

    // PruneAfter over PruneAfter stays on the bespoke driver: the pair merges into ONE
    // wrapper by predicate union.
    IAsyncTreenumerable<TNode> IAsyncSelectWhereTreenumerable<TNode>.ComposePruneAfter(Func<NodeContext<TNode>, bool> outerPredicate)
    {
      return new AsyncPruneAfterTreenumerable<TNode>(
        _Source, SelectWhereComposition.PruneAfterThenPruneAfter(_Predicate, outerPredicate));
    }

    // A projection joins: promote to the middle tier (light passthrough driver), never the
    // filter driver.
    IAsyncTreenumerable<TOuterResult> IAsyncSelectWhereTreenumerable<TNode>.Compose<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector)
    {
      return new AsyncSelectPruneAfterTreenumerable<TNode, TOuterResult>(
        _Source, SelectWhereComposition.PruneAfterThenSelect(_Predicate, selector));
    }

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncPruneAfterTreenumerator<TNode>(_Source.GetAsyncBreadthFirstTreenumerator, _Predicate);

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() =>
      new AsyncPruneAfterTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator, _Predicate);
  }
}
