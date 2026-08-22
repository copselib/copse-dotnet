using Copse.Core;

namespace Copse.Linq.Async.Treenumerables
{
  // The bind's selector as a struct leg, so the driver's per-node call inlines and the
  // left door (SELECTMANY_DESIGN.md Addendum V) can nest a collapsed chain's arrow in the
  // TYPE ahead of the user's selector -- the same shape as IResultSelector for the
  // SelectWhere lattice. Context-shaped: a folded chain's positional legs read the source
  // position exactly as they did in the chain.
  internal interface IAsyncExpansionSelector<TSource, TResult>
  {
    AsyncExpansion<TResult> GetExpansion(NodeContext<TSource> nodeContext);
  }
}
