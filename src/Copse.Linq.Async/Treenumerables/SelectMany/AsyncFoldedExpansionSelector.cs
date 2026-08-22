using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The left door's leg: a collapsed chain's arrow (Where/PruneBefore/PruneAfter/Select
  // legs, Kleisli-composed) folded ahead of the user's selector, pointwise. The arrow's
  // result is the quartet encoded as strategies (SELECTMANY_DESIGN.md Addendum V), and the
  // quartet is a sub-monoid under Kleisli composition, so the fold is a table lookup:
  //
  //   SkipNode in s                     -> Promote  (Where's drop arm)
  //   SkipNode and SkipDescendants in s -> Drop     (PruneBefore)
  //   SkipDescendants in s              -> f(v) slotless (PruneAfter then f: the Leaf row)
  //   otherwise                         -> f(v)
  //
  // Consumer-side strategies are a separate channel (the driver's), untouched here.
  internal readonly struct AsyncFoldedExpansionSelector<TSource, TMid, TResult, TArrow> : IAsyncExpansionSelector<TSource, TResult>
    where TArrow : struct, IResultSelector<TSource, TMid>
  {
    public AsyncFoldedExpansionSelector(TArrow arrow, Func<TMid, AsyncExpansion<TResult>> selector)
    {
      _Arrow = arrow;
      _Selector = selector;
    }

    private readonly TArrow _Arrow;
    private readonly Func<TMid, AsyncExpansion<TResult>> _Selector;

    public AsyncExpansion<TResult> GetExpansion(NodeContext<TSource> nodeContext)
    {
      var result = _Arrow.GetResult(nodeContext);
      var dropsDescendants = result.Strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants);

      if (result.Strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        return dropsDescendants ? AsyncExpansion.Drop<TResult>() : AsyncExpansion.Promote<TResult>();

      var expansion = _Selector(result.Value);

      return dropsDescendants ? expansion.WithoutSlot() : expansion;
    }
  }
}
