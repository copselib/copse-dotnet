using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // Plain positional Where's verdict: the predicate sees (node, this layer's input labels).
  internal readonly struct PositionalWhereVerdictSelector<TNode> : IVerdictSelector<TNode, TNode>
  {
    public PositionalWhereVerdictSelector(Func<TNode, NodePosition, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, NodePosition, bool> _Predicate;

    public FusionVerdict<TNode> GetVerdict(NodeContext<TNode> nodeContext)
      => new FusionVerdict<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node, nodeContext.Position)
          ? NodeTraversalStrategies.TraverseAll
          : NodeTraversalStrategies.SkipNode);
  }
}
