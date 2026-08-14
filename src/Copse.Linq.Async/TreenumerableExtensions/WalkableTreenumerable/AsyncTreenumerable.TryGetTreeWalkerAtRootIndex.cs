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
    /// DOOR MACHINERY CLAUSE (Stage B): doors may touch the terrain -- this one probes the
    /// root group directly; consumers never need to, and post-Stage-C this body reaches the
    /// terrain through the walker seam rather than the contract.
    /// invariant, kept at the door.
    /// </summary>
    public static async ValueTask<AsyncTreeWalkerResult<TValue, THandle>> TryGetTreeWalkerAtRootIndexAsync<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      int rootIndex = 0)
    {
      var rootResult = await source.TryGetRootAtAsync(rootIndex).ConfigureAwait(false);

      return rootResult.HasChild
        ? new AsyncTreeWalkerResult<TValue, THandle>(new AsyncTreeWalker<TValue, THandle>(source, rootResult.Child.Node))
        : default;
    }
  }
}
