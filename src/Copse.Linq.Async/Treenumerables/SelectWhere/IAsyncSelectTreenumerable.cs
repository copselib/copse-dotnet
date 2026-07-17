using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The projection fast path, declared only where it exists: a wrapper with a LIGHT
  // projection-only representation composes a further projection without converting to the
  // filter driver. This is a capability of the projection wrapper, not an obligation of
  // every composable -- the general Compose handles projections correctly (a projection is a
  // result selector that never rejects); this interface only preserves the cheaper acquisition. An
  // implementer is by construction a projection-only chain, so its Relabels
  // is always false.
  internal interface IAsyncSelectTreenumerable<TNode> : IAsyncSelectWhereTreenumerable<TNode>
  {
    IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector);
  }
}
