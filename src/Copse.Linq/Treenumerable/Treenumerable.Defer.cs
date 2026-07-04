using Copse.Core;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    // Lazy tree factory (Ix's Defer): the factory runs per treenumerator acquisition, so each
    // traversal sees a freshly constructed tree. The two dimensions invoke the factory
    // independently -- an impure factory can therefore hand them different trees, the same
    // contract as any impure source (Memoize is what pins a single shape).
    public static ITreenumerable<TNode> Defer<TNode>(Func<ITreenumerable<TNode>> treenumerableFactory)
      => TreenumerableFactory.Create(
        () => treenumerableFactory().GetBreadthFirstTreenumerator(),
        () => treenumerableFactory().GetDepthFirstTreenumerator());
  }
}
