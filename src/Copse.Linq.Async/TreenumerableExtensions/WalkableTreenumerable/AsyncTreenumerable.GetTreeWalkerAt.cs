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
    /// while consuming (<see cref="GetHandles{TNode, THandle}"/>) or from the root door
    /// (<see cref="TryGetTreeWalkerAtRootIndexAsync{TNode, THandle}"/>), never from value
    /// search. The handle is presumed to be one this walkable issued (the foreign-handle
    /// clause). Pure construction: no probe fires here; a forged handle stays loud on the
    /// exception channel at the first probe through the walker.
    /// </summary>
    public static async ValueTask<AsyncTreeWalker<TNode, THandle>> GetTreeWalkerAtAsync<TNode, THandle>(
      this IAsyncWalkableTreenumerable<TNode, THandle> source,
      THandle handle)
      // Re-entry goes door-then-jump -- one knock, then the trusted address.
      => (await source.GetTreeWalkerAsync().ConfigureAwait(false)).At(handle);
  }
}
