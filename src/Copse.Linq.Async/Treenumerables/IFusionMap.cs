using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The erased combinator surface of a fused chain's accumulated mapping (Jason's FusionMap
  // model, docs/OPERATOR_FUSION_DESIGN.md): a fusable wrapper offers its internal mapping up
  // through this interface, the appending operator composes onto it with a bare lambda --
  // Select is fmap, Filter is the filter-bind -- and ToTreenumerable reifies the result,
  // choosing the representation (a projection-only map stays on the light Select
  // treenumerator; a filtering map acquires through the verdict driver). Erasure is why this
  // exists: the operator cannot name the map's source type, but the map can, so the map
  // composes and constructs.
  internal interface IFusionMap<TNode>
  {
    // True once any relabeling stage is aboard; the operators' positional flavors read this
    // to apply the join rule before composing.
    bool ContainsRelabelingStage { get; }

    // fmap: compose a projection onto the mapping. Never relabels, never rejects.
    IFusionMap<TOuterResult> Select<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector);

    // The filter-bind: compose a type-preserving verdict stage (where, prunes, future
    // filters). relabels: whether THIS stage moves surviving nodes' labels (Where and
    // PruneBefore do; PruneAfter does not).
    IFusionMap<TNode> Filter(Func<NodeContext<TNode>, FusionVerdict<TNode>> stage, bool relabels);

    // Reify the accumulated mapping back into a treenumerable.
    IAsyncTreenumerable<TNode> ToTreenumerable();
  }
}
