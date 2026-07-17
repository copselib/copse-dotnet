using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The composition recipe surface (docs/OPERATOR_FUSION_DESIGN.md): appending an operator is
  // ONE call -- the wrapper unwraps its own mapping, composes the stage onto it, discards
  // itself, and constructs the successor treenumerable. The operator composes with bare
  // lambdas (it knows its own flavor and reads ContainsRelabelingStage for the join rule);
  // the wrapper, which knows the erased source type, composes and constructs. The
  // representation choice is the implementing TYPE: a projection-only chain stays an
  // AsyncSelectTreenumerable (light acquisition) until a filter joins; result-backed chains
  // are ComposableTreenumerable over the filter driver.
  //
  // Deliberately INTERNAL: a public recipe would make these operators' correctness depend on
  // foreign implementations, and the older TFMs' lack of default interface members would make
  // every evolution a breaking change.
  internal interface IAsyncComposableTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    // True once any relabeling stage is aboard; the operators' positional flavors read this
    // to apply the join rule before composing.
    bool ContainsRelabelingStage { get; }

    // fmap: compose a projection onto the mapping. Never relabels, never rejects.
    IAsyncTreenumerable<TOuterResult> ComposeSelect<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector);

    // The filter-bind: compose a type-preserving result stage (wheres, prunes, future
    // filters). relabels: whether THIS stage moves surviving nodes' labels (Where and
    // PruneBefore do; PruneAfter does not).
    IAsyncTreenumerable<TNode> ComposeFilter(Func<NodeContext<TNode>, CompositionResult<TNode>> stage, bool relabels);
  }
}
