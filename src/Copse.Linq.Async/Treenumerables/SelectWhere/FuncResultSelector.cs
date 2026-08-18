using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The closure LEAF of the selector algebra: wraps ONE user-delegate result selector so it
  // can ride the struct forms (the Func Compose doors wrap their delegate in this and
  // forward to struct Compose; chains then nest via ComposedResultSelector). A user delegate
  // inherently costs a delegate call, so this leaf keeps it -- the struct seam exists so the
  // PLAIN operators don't pay it.
  internal readonly struct FuncResultSelector<TSource, TResult> : IResultSelector<TSource, TResult>
  {
    public FuncResultSelector(Func<NodeContext<TSource>, SelectWhereResult<TResult>> resultSelector)
    {
      _ResultSelector = resultSelector;
    }

    private readonly Func<NodeContext<TSource>, SelectWhereResult<TResult>> _ResultSelector;

    public SelectWhereResult<TResult> GetResult(NodeContext<TSource> nodeContext) => _ResultSelector(nodeContext);
  }
}
