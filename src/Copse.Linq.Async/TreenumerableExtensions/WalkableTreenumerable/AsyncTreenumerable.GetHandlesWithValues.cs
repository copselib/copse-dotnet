using Copse.Async;
using System.Collections.Generic;
using System.Threading;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The acquisition scan: every handle paired with the value it labels, in the same
    /// deliberately unspecified order as <see cref="GetHandles{TNode, THandle}"/>. The rows
    /// let value predicates pick out handles -- consumer-side, preserving the
    /// no-node-equality pledge (the library compares nothing; the consumer's predicate is the
    /// consumer's business). The search law's one earned exception: without the pairing, a
    /// value predicate mid-chain cannot reach the receiver's probe without naming it twice.
    /// A stance walk (Stage B): each row is where the walk stood and what it extracted there.
    /// </summary>
    public static async IAsyncEnumerable<HandleAndValue<THandle, TNode>> GetHandlesWithValues<TNode, THandle>(
      this IAsyncWalkableTreenumerable<TNode, THandle> source,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      // One knock; the roots are the unfocused stance's child group. The unfocused stance itself gets no
      // row -- it has no handle and no value, so the scan excludes it by type.
      var door = await source.GetTreeWalkerAsync().ConfigureAwait(false);
      var pending = new Stack<AsyncTreeWalker<TNode, THandle>>();

      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootStance = await door.MoveToChildAsync(rootIndex).ConfigureAwait(false);

        if (!rootStance.HasValue)
          break;

        pending.Push(rootStance.Value);
      }

      while (pending.Count > 0)
      {
        cancellationToken.ThrowIfCancellationRequested();

        var stance = pending.Pop();

        yield return new HandleAndValue<THandle, TNode>(stance.Focus, await stance.GetValueAsync().ConfigureAwait(false));

        for (var childIndex = 0; ; childIndex++)
        {
          var childStance = await stance.MoveToChildAsync(childIndex).ConfigureAwait(false);

          if (!childStance.HasValue)
            break;

          pending.Push(childStance.Value);
        }
      }
    }
  }
}
