using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The composition recipe surface (design-docs/OPERATOR_COMPOSITION_DESIGN.md): appending an operator is
  // ONE call -- the wrapper unwraps its own mapping, composes the result selector onto it, discards
  // itself, and constructs the successor treenumerable. The operator composes with bare
  // lambdas (it knows its own flavor and reads Relabels for the join rule);
  // the wrapper, which knows the erased source type, binds it and constructs.
  //
  // ONE method covers the whole selector algebra: a projection is just a result selector that never
  // rejects (its results carry TraverseAll), and the composition law handles it without
  // being told. The light projection-only representation is not this contract's business --
  // it is the capability of the one wrapper that has it (IAsyncSelectTreenumerable).
  //
  // Deliberately INTERNAL: a public recipe would make these operators' correctness depend on
  // foreign implementations, and the older TFMs' lack of default interface members would make
  // every evolution a breaking change.
  internal interface IAsyncSelectWhereTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    // True once any relabeling operator is aboard; the operators' positional flavors read this
    // to apply the join rule before composing.
    bool Relabels { get; }

    // Compose a result selector onto the accumulated mapping and return the successor treenumerable.
    // relabels: whether THIS operator moves surviving nodes' labels (Where and PruneBefore do;
    // PruneAfter and projections do not).
    IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TNode>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels);

    // The STRUCT-composed form (the reunification gate): the outer piece arrives as an
    // inlinable selector struct, so the successor's composed chain nests in the TYPE and the
    // splice costs no delegate hops. The Func form above remains for pieces that are
    // inherently closures.
    IAsyncTreenumerable<TOuterResult> Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<TNode, TOuterResult>;
  }
}
