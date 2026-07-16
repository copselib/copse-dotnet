using Copse.Core;

namespace Copse.Linq.Async.Treenumerables
{
  // The fused pipeline's carrier: one evaluation of the composed stage chain against a source
  // node answers the driver's whole per-node question -- is the node in the output tree, and
  // how should the inner treenumerator be pulled past this point? A plain (value, strategies)
  // pair; the strategies speak the consumer protocol directly:
  //
  //   (value, TraverseAll)               in the tree; traverse normally
  //   (value, SkipDescendants)           in the tree; don't descend below it (PruneAfter)
  //   (value, SkipNode)                  filtered; children promote (Where)
  //   (value, SkipNodeAndDescendants)    pruned with its whole subtree (PruneBefore)
  //
  // REJECTION IS SkipNode-MEMBERSHIP: the consumer protocol already defines SkipNode as
  // "remove this node", so the verdict inherits that meaning rather than tracking a second
  // flag or a case split -- any pair is coherent because the strategies alone say what
  // happens to the node.
  internal readonly struct CompositionVerdict<TNode>
  {
    public CompositionVerdict(TNode value, NodeTraversalStrategies strategies)
    {
      Value = value;
      Strategies = strategies;
    }

    // Unobserved when the strategies carry SkipNode (the fold stops and the driver never
    // publishes the node).
    public readonly TNode Value;
    public readonly NodeTraversalStrategies Strategies;

    // The composition law's accept-side union: strategies gathered by earlier accepting stages
    // ride along. Uniform over both fates -- a rejected verdict stays rejected (SkipNode is
    // already aboard) and its instruction gains the earlier stages' contributions.
    public CompositionVerdict<TNode> WithEarlierStrategies(NodeTraversalStrategies earlierStrategies)
      => new CompositionVerdict<TNode>(Value, Strategies | earlierStrategies);
  }
}
