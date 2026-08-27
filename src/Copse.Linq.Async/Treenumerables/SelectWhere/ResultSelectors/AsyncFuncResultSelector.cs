using System;

namespace Copse.Linq.Treenumerables
{
  // The closure LEAF of the selector algebra: wraps ONE user-delegate result selector so it
  // can ride the struct forms (a closure-bound piece enters the splice through this leaf,
  // and chains then nest via AsyncComposedResultSelector). A user delegate inherently costs a
  // delegate call, so this leaf keeps it -- the struct seam exists so the PLAIN operators
  // don't pay it.
  internal readonly struct AsyncFuncResultSelector<TSource, TResult> : IAsyncResultSelector<TSource, TResult>
  {
    public AsyncFuncResultSelector(Func<NodeContext<TSource>, AsyncSelectWhereResult<TResult>> resultSelector)
    {
      _ResultSelector = resultSelector;
    }

    private readonly Func<NodeContext<TSource>, AsyncSelectWhereResult<TResult>> _ResultSelector;

    public AsyncSelectWhereResult<TResult> GetResult(NodeContext<TSource> nodeContext) => _ResultSelector(nodeContext);
  }
}
