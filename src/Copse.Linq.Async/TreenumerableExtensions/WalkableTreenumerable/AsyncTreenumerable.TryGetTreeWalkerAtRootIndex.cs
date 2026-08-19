using Copse.Async;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The root door: a walker standing at root <paramref name="rootIndex"/>, or an empty
    /// result past the last root. TryGet by the Try law: the miss is expected and TYPED (a
    /// forest may have fewer roots, or none) -- the result struct is the async spelling of
    /// the try-pattern (<c>out</c> cannot cross an <c>await</c>). The name spells RootIndex
    /// because the int here is a root ordinal, not a handle -- the door that takes a handle
    /// is <see cref="GetTreeWalkerAt{TValue, THandle}"/>, and when <c>THandle</c> is
    /// <c>int</c> only the names keep the two questions apart. The no-unfocused-walker
    /// DOOR MACHINERY CLAUSE (Stage B): doors may touch the topology -- this one probes the
    /// root group directly; consumers never need to, and post-Stage-C this body reaches the
    /// topology through the walker seam rather than the contract.
    /// invariant, kept at the door.
    /// </summary>
    public static async ValueTask<Option<AsyncTreeWalker<TValue, THandle>>> TryGetTreeWalkerAtRootIndexAsync<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      int rootIndex = 0)
    {
      // Stage C: the door clause in action -- knock once, then reach the bound topology
      // through the walker seam for the k-th root (the sentinel's child group).
      var door = await source.TryGetTreeWalkerAsync().ConfigureAwait(false);

      // Root 0 is the door's own stance -- answer it without a second probe.
      if (rootIndex == 0 || !door.HasValue)
        return door;

      return await door.Value.MoveToRootAsync(rootIndex).ConfigureAwait(false);
    }
  }
}
