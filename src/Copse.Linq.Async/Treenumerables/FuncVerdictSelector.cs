using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The fused chains' selector: wraps the Kleisli-composed verdict closure. Fusion inherently
  // carries user delegates, so this path keeps the delegate call -- the struct seam exists so
  // the PLAIN operators don't pay it.
  internal readonly struct FuncVerdictSelector<TInner, TNode> : IVerdictSelector<TInner, TNode>
  {
    public FuncVerdictSelector(Func<NodeContext<TInner>, FusionVerdict<TNode>> verdictSelector)
    {
      _VerdictSelector = verdictSelector;
    }

    private readonly Func<NodeContext<TInner>, FusionVerdict<TNode>> _VerdictSelector;

    public FusionVerdict<TNode> GetVerdict(NodeContext<TInner> nodeContext) => _VerdictSelector(nodeContext);
  }
}
