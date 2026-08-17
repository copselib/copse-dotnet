using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq.Async
{
  // The composed-projection variant of the TakeSubtreesWhere citizen: the same dispatched
  // recipe with a product selector, itself a citizen (further Selects compose onto the
  // selector -- closure by signature). Per dimension the selector rides the leanest seam
  // available: breadth-first it lands INSIDE the chain's SelectWhere driver (the lattice
  // absorbs it); depth-first it is one light Select wrapper over the bespoke O(1) wrapper --
  // one layer however long the composed chain, because composition happened on the selector.
  internal sealed class AsyncTakeSubtreesWhereProductTreenumerable<TNode, TProduct>
    : IAsyncSelectComposableTreenumerable<TProduct>
  {
    public AsyncTakeSubtreesWhereProductTreenumerable(
      IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      Func<TNode, TProduct> productSelector)
    {
      _Source = source;
      _Predicate = predicate;
      _ProductSelector = productSelector;
    }

    private readonly IAsyncTreenumerable<TNode> _Source;
    private readonly Func<NodeContext<TNode>, bool> _Predicate;
    private readonly Func<TNode, TProduct> _ProductSelector;

    public IAsyncTreenumerator<TProduct> GetAsyncDepthFirstTreenumerator()
      => AsyncTree.CreateDepthFirst(
          () => new AsyncTakeSubtreesWhereTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator, _Predicate))
        .Select(_ProductSelector)
        .GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TProduct> GetAsyncBreadthFirstTreenumerator()
      => AsyncTakeSubtreesWhereTreenumerable<TNode>.BreadthFirstChain(_Source, _Predicate)
        .Select(_ProductSelector)
        .GetAsyncBreadthFirstTreenumerator();

    public IAsyncSelectComposableTreenumerable<TResult> ComposeSelect<TResult>(Func<TProduct, TResult> selector)
    {
      var currentProductSelector = _ProductSelector;

      return new AsyncTakeSubtreesWhereProductTreenumerable<TNode, TResult>(
        _Source, _Predicate, node => selector(currentProductSelector(node)));
    }
  }
}
