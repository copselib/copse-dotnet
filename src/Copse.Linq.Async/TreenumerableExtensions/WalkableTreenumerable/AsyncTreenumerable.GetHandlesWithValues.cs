using Copse.Async;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The acquisition scan: every handle paired with the value it labels, in the same
    /// deliberately unspecified order as <see cref="GetHandles{TValue, THandle}"/>. The rows
    /// let value predicates pick out handles -- consumer-side, preserving the
    /// no-node-equality pledge (the library compares nothing; the consumer's predicate is the
    /// consumer's business). The search law's one earned exception: without the pairing, a
    /// value predicate mid-chain cannot reach the receiver's probe without naming it twice.
    /// </summary>
    public static async IAsyncEnumerable<HandleAndValue<THandle, TValue>> GetHandlesWithValues<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      await foreach (var handle in source.GetHandles().ConfigureAwait(false))
      {
        var value = await source.GetValueAsync(handle).ConfigureAwait(false);

        yield return new HandleAndValue<THandle, TValue>(handle, value);
      }
    }
  }
}
