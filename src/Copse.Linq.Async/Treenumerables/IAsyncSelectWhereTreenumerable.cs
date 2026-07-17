using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The composition recipe surface (docs/OPERATOR_FUSION_DESIGN.md): appending an operator is
  // ONE call -- the wrapper unwraps its own mapping, composes the stage onto it, discards
  // itself, and constructs the successor treenumerable. The operator composes with bare
  // lambdas (it knows its own flavor and reads ContainsRelabelingStage for the join rule);
  // the wrapper, which knows the erased source type, binds it and constructs.
  //
  // ONE method covers the whole stage algebra: a projection is just a stage that never
  // rejects (its results carry TraverseAll), and the composition law handles it without
  // being told. The light projection-only representation is not this contract's business --
  // it is the capability of the one wrapper that has it (IAsyncSelectTreenumerable).
  //
  // Deliberately INTERNAL: a public recipe would make these operators' correctness depend on
  // foreign implementations, and the older TFMs' lack of default interface members would make
  // every evolution a breaking change.
  internal interface IAsyncSelectWhereTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    // True once any relabeling stage is aboard; the operators' positional flavors read this
    // to apply the join rule before composing.
    bool ContainsRelabelingStage { get; }

    // Compose a stage onto the accumulated mapping and return the successor treenumerable.
    // relabels: whether THIS stage moves surviving nodes' labels (Where and PruneBefore do;
    // PruneAfter and projections do not).
    IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TNode>, SelectWhereResult<TOuterResult>> stage,
      bool relabels);
  }
}
