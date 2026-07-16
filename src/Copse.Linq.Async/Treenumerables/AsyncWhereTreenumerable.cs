using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The value-only Where wrapper: the named, probeable form. Only the value-only overload
  // builds this (the positional overload builds the anonymous factory wrapper and never fuses
  // with its own kind -- each positional predicate is entitled to its input tree's labels, and
  // a Where boundary relabels). An appended value-only Where fuses by predicate combination --
  // LINQ's CombinePredicates, legal because neither predicate observes a coordinate.
  internal sealed class AsyncWhereTreenumerable<TNode> : IAsyncFusableTreenumerable<TNode>
  {
    public AsyncWhereTreenumerable(IAsyncTreenumerable<TNode> source, Func<TNode, bool> predicate)
    {
      _Source = source;
      _Predicate = predicate;
      _ContextPredicate = nodeContext => predicate(nodeContext.Node);
    }

    private readonly IAsyncTreenumerable<TNode> _Source;
    private readonly Func<TNode, bool> _Predicate;
    private readonly Func<NodeContext<TNode>, bool> _ContextPredicate;

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncWhereBreadthFirstTreenumerator<TNode, TNode>(
        _Source.GetAsyncBreadthFirstTreenumerator,
        AsyncIdentitySelector<TNode>.Instance,
        _ContextPredicate,
        NodeTraversalStrategies.SkipNode);

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() =>
      new AsyncWhereDepthFirstTreenumerator<TNode, TNode>(
        _Source.GetAsyncDepthFirstTreenumerator,
        AsyncIdentitySelector<TNode>.Instance,
        _ContextPredicate,
        NodeTraversalStrategies.SkipNode);

    public IAsyncTreenumerable<TNode> FuseWhere(Func<TNode, bool> predicate)
    {
      var innerPredicate = _Predicate;

      return new AsyncWhereTreenumerable<TNode>(_Source, node => innerPredicate(node) && predicate(node));
    }

    // This boundary relabels (depth compression, sibling renumbering), and a positional
    // predicate is entitled to the relabeled coordinates.
    public IAsyncTreenumerable<TNode> FusePositionalWhere(Func<TNode, NodePosition, bool> predicate) => null;

    // Where-then-Select needs an emission-side projection seam the drivers do not have yet
    // (they project BEFORE the test); decline until that seam exists.
    public IAsyncTreenumerable<TOuterResult> FuseSelect<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector) => null;
  }
}
