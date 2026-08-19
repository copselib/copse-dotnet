using Copse.Async;
using System.Collections.Generic;
using System.Threading;

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
    /// A stance walk (Stage B): each row is where the walk stood and what it extracted there.
    /// </summary>
    public static async IAsyncEnumerable<HandleAndValue<THandle, TValue>> GetHandlesWithValues<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var pending = new Stack<AsyncTreeWalker<TValue, THandle>>();

      for (var rootIndex = 0;
        (await source.TryGetTreeWalkerAtRootIndexAsync(rootIndex).ConfigureAwait(false)).TryGetValue(out var rootStance);
        rootIndex++)
        pending.Push(rootStance);

      while (pending.Count > 0)
      {
        var stance = pending.Pop();

        yield return new HandleAndValue<THandle, TValue>(stance.Focus, await stance.GetValueAsync().ConfigureAwait(false));

        for (var childIndex = 0;
          (await stance.MoveToChildAsync(childIndex).ConfigureAwait(false)).TryGetValue(out var child);
          childIndex++)
          pending.Push(child);
      }
    }
  }
}
