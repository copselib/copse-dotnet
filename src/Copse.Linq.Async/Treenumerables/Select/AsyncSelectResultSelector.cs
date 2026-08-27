using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // A projection as a result-selector leg: never rejects, carries no strategies -- the user
  // lambda is the leaf call; the struct is the inlinable plumbing around it.
  internal readonly struct AsyncSelectResultSelector<TSource, TResult> : IAsyncResultSelector<TSource, TResult>
  {
    public AsyncSelectResultSelector(Func<NodeContext<TSource>, TResult> selector)
    {
      _Selector = selector;
    }

    private readonly Func<NodeContext<TSource>, TResult> _Selector;

    public AsyncSelectWhereResult<TResult> GetResult(NodeContext<TSource> nodeContext)
      => new AsyncSelectWhereResult<TResult>(_Selector(nodeContext), NodeTraversalStrategies.TraverseAll);
  }
}
