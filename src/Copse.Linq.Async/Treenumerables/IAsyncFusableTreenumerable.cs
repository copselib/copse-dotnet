using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The fusion recipe surface (docs/OPERATOR_FUSION_DESIGN.md): a fusable wrapper is offered an
  // appended operator and either FUSES it (collapsing the layer) or DECLINES by returning null,
  // in which case the operator falls back to its plain wrap -- declining is always safe, so
  // every hook is an optimization with a mandatory correct fallback. Double dispatch because
  // the appending operator cannot name the wrapper's erased source type. Deliberately INTERNAL:
  // a public recipe would make these operators' correctness depend on foreign implementations,
  // and the older TFMs' lack of default interface members would make every added hook a
  // breaking change.
  internal interface IAsyncFusableTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    // A value-only Where appended. Observes no coordinates, so it may cross any emission
    // boundary; every wrapper can legally fuse or decline it.
    IAsyncTreenumerable<TNode> FuseWhere(Func<TNode, bool> predicate);

    // A positional Where appended. Legal only across boundaries that do not relabel positions
    // (Select's is the identity); a Where wrapper must decline -- its boundary compresses
    // depths and renumbers siblings, and the appended predicate is entitled to those labels.
    IAsyncTreenumerable<TNode> FusePositionalWhere(Func<TNode, NodePosition, bool> predicate);

    // A Select appended (either flavor, pre-adapted to context shape by the operator).
    IAsyncTreenumerable<TOuterResult> FuseSelect<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector);
  }
}
