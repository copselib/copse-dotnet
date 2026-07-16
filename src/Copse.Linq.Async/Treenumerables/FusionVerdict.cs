using Copse.Core;

namespace Copse.Linq.Async.Treenumerables
{
  // The fused pipeline's carrier (docs/OPERATOR_FUSION_DESIGN.md, "the verdict monad"): one
  // evaluation of the composed stage chain against a source node yields either the node's
  // final projected value plus any operator-side strategies the accepting stages contributed
  // (PruneAfter's SkipDescendants), or a rejection carrying the strategies that remove it
  // (Where's SkipNode -> promotion; PruneBefore's SkipNodeAndDescendants -> subtree drop).
  // Strategies union under composition; the first rejecting stage ends evaluation.
  internal readonly struct FusionVerdict<TNode>
  {
    private FusionVerdict(TNode value, NodeTraversalStrategies strategies, bool rejected)
    {
      Value = value;
      Strategies = strategies;
      Rejected = rejected;
    }

    public readonly TNode Value;
    public readonly NodeTraversalStrategies Strategies;
    public readonly bool Rejected;

    public static FusionVerdict<TNode> Accept(TNode value)
      => new FusionVerdict<TNode>(value, NodeTraversalStrategies.TraverseAll, rejected: false);

    public static FusionVerdict<TNode> Accept(TNode value, NodeTraversalStrategies strategies)
      => new FusionVerdict<TNode>(value, strategies, rejected: false);

    public static FusionVerdict<TNode> Reject(NodeTraversalStrategies strategies)
      => new FusionVerdict<TNode>(default, strategies, rejected: true);

    // The composition law's accept-side union: strategies gathered by earlier accepting
    // stages ride along with this verdict's own.
    public FusionVerdict<TNode> WithStrategies(NodeTraversalStrategies earlierStrategies)
      => new FusionVerdict<TNode>(Value, Strategies | earlierStrategies, Rejected);
  }
}
