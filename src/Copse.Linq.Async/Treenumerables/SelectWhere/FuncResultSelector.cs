using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The composed chains' selector: wraps the Kleisli-composed result closure. A composed
  // chain inherently carries user delegates, so this path keeps the delegate call -- the
  // struct seam exists so the PLAIN operators don't pay it.
  internal readonly struct FuncResultSelector<TInner, TNode> : IResultSelector<TInner, TNode>
  {
    public FuncResultSelector(Func<NodeContext<TInner>, SelectWhereResult<TNode>> resultSelector)
    {
      _ResultSelector = resultSelector;
    }

    private readonly Func<NodeContext<TInner>, SelectWhereResult<TNode>> _ResultSelector;

    public SelectWhereResult<TNode> GetResult(NodeContext<TInner> nodeContext) => _ResultSelector(nodeContext);
  }
}
