using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The fusion recipe surface (docs/OPERATOR_FUSION_DESIGN.md): a fusable wrapper is offered an
  // appended operator and either FUSES it (collapsing the layer) or DECLINES by returning null,
  // in which case the operator falls back to its plain wrap -- declining is always safe, so
  // every hook is an optimization with a mandatory correct fallback. Hooks are split by lambda
  // FLAVOR, not just operator: value lambdas may join any chain, positional lambdas only a
  // label-preserving prefix, and only the wrapper knows which prefix it is (the join rule).
  // Double dispatch because the appending operator cannot name the wrapper's erased source
  // type. Deliberately INTERNAL: a public recipe would make these operators' correctness
  // depend on foreign implementations, and the older TFMs' lack of default interface members
  // would make every added hook a breaking change.
  internal interface IAsyncFusableTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    IAsyncTreenumerable<TNode> FuseWhere(Func<TNode, bool> predicate);

    IAsyncTreenumerable<TNode> FusePositionalWhere(Func<TNode, NodePosition, bool> predicate);

    IAsyncTreenumerable<TOuterResult> FuseSelect<TOuterResult>(Func<TNode, TOuterResult> selector);

    IAsyncTreenumerable<TOuterResult> FusePositionalSelect<TOuterResult>(Func<TNode, NodePosition, TOuterResult> selector);
  }
}
