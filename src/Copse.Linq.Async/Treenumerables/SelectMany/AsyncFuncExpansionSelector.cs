using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The user's selector as the leaf leg: value-only, position ignored.
  internal readonly struct AsyncFuncExpansionSelector<TSource, TResult> : IAsyncExpansionSelector<TSource, TResult>
  {
    public AsyncFuncExpansionSelector(Func<TSource, AsyncExpansion<TResult>> selector)
    {
      _Selector = selector;
    }

    private readonly Func<TSource, AsyncExpansion<TResult>> _Selector;

    public AsyncExpansion<TResult> GetExpansion(NodeContext<TSource> nodeContext) => _Selector(nodeContext.Node);
  }
}
