using Copse.Async;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Enter the comonadic view: a walker standing at <paramref name="handle"/>. Walker
    /// acquisition named like treenumerator acquisition (the <c>GetEnumerator</c> sense of
    /// Get: this door MINTS its object) -- and bare Get by the Try law: choosing a focus is
    /// trust-based, so there is no typed miss here. Handles come from recording positions
    /// while consuming (<see cref="GetHandles{TValue, THandle}"/>) or from the root door
    /// (<see cref="TryGetTreeWalkerAtRootIndexAsync{TValue, THandle}"/>), never from value
    /// search -- and there is deliberately no door that produces an unfocused walker.
    /// The handle is presumed to be one this walkable issued (the foreign-handle clause).
    /// Pure construction: no probe fires here; a forged handle stays loud on the exception
    /// channel at the first probe through the walker.
    /// </summary>
    public static async ValueTask<AsyncTreeWalker<TValue, THandle>> GetTreeWalkerAtAsync<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
      // Stage C: the walkable no longer IS a topology, so re-entry goes door-then-jump --
      // one knock, then the trusted address. A valid handle implies a nonempty forest, so
      // the door's walker is presumed present (the trust door's usual bargain).
      => (await source.TryGetTreeWalkerAsync().ConfigureAwait(false)).Value.At(handle);
  }
}
