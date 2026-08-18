using Copse.Core;
using System.Runtime.CompilerServices;
using System;
using System.Runtime.CompilerServices;

namespace Copse.Linq.Async.Treenumerables
{
  // A projection as a result-selector leg: never rejects, carries no strategies -- the user
  // lambda is the leaf call; the struct is the inlinable plumbing around it.
  internal readonly struct SelectResultSelector<TSource, TResult> : IResultSelector<TSource, TResult>
  {
    public SelectResultSelector(Func<NodeContext<TSource>, TResult> selector)
    {
      _Selector = selector;
    }

    private readonly Func<NodeContext<TSource>, TResult> _Selector;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SelectWhereResult<TResult> GetResult(NodeContext<TSource> nodeContext)
      => new SelectWhereResult<TResult>(_Selector(nodeContext), NodeTraversalStrategies.TraverseAll);
  }
}
