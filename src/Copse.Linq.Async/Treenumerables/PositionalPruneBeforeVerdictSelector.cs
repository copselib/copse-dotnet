using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The positional PruneBefore's verdict: the predicate sees (node, this layer's input labels).
  internal readonly struct PositionalPruneBeforeVerdictSelector<TNode> : IVerdictSelector<TNode, TNode>
  {
    public PositionalPruneBeforeVerdictSelector(Func<TNode, NodePosition, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, NodePosition, bool> _Predicate;

    public CompositionVerdict<TNode> GetVerdict(NodeContext<TNode> nodeContext)
      => new CompositionVerdict<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node, nodeContext.Position)
          ? NodeTraversalStrategies.SkipNodeAndDescendants
          : NodeTraversalStrategies.TraverseAll);
  }
}
