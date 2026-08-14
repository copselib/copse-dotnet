using Copse.Async;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Enter the comonadic view: a walker standing at <paramref name="handle"/>. Choosing a
    /// focus is an explicit act -- handles come from recording positions while consuming
    /// (<see cref="GetHandles{TValue, THandle}"/>) or from the root door below, never from
    /// value search -- and there is deliberately no door that produces an unfocused walker.
    /// The handle is presumed to be one this walkable issued (the foreign-handle clause).
    /// Pure construction: no probe fires here.
    /// </summary>
    public static AsyncTreeWalker<TValue, THandle> WalkerAt<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
      => new AsyncTreeWalker<TValue, THandle>(source, handle);

    /// <summary>
    /// The root door: a walker standing at root <paramref name="rootIndex"/>, or an empty
    /// result past the last root. Result-typed because the probe can miss (a forest may have
    /// fewer roots, or none) -- the no-unfocused-walker invariant, kept at the door.
    /// </summary>
    public static async ValueTask<AsyncTreeWalkerResult<TValue, THandle>> GetRootWalkerAsync<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      int rootIndex = 0)
    {
      var rootResult = await source.GetRootAtAsync(rootIndex).ConfigureAwait(false);

      return rootResult.HasChild
        ? new AsyncTreeWalkerResult<TValue, THandle>(new AsyncTreeWalker<TValue, THandle>(source, rootResult.Child.Node))
        : default;
    }
  }
}
