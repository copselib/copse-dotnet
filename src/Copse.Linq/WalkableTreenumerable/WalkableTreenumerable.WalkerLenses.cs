using Copse;
using Copse.Core;
using Copse.Linq.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class WalkableTreenumerable
  {
    /// <summary>
    /// The restriction lens: <c>PruneAfter</c> over a WALKABLE stays walkable. Same semantics
    /// and predicate flavor as the streaming overload (keeps each matching node, sheds its
    /// subtree), resolved STATICALLY by the receiver's type -- no probes, the dimension-split
    /// discipline. A pair citizen: the order half IS the streaming operator applied to the
    /// source (the composition lattice inside it keeps working -- a stacked lens's stream half
    /// is prune-over-prune, which the light tier merges in-tier as always); the adjacency half
    /// wraps one probe. O(1) per probe. (The positional flavor is deliberately absent: depth
    /// is not stored on a walker, so a positional predicate would price an O(depth) climb per
    /// probe -- it arrives when a caller needs it, with that cost documented.)
    /// </summary>
    public static IWalkableTreenumerable<TValue, THandle> PruneAfter<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      Func<TValue, bool> predicate)
    {
      if (predicate == null)
        return source;

      return new PruneAfterWalkable<TValue, THandle>(
        source,
        predicate,
        ((ITreenumerable<TValue>)source).PruneAfter(predicate));
    }
  }
}
