using Copse.Async;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Every handle the walkable's terrain reaches from its roots, in DELIBERATELY UNSPECIFIED
    /// order -- the SET is the promise (handles are positional identity made portable; recording
    /// them while consuming is the sanctioned acquisition path, since the library never searches
    /// by value). An explicit-stack sweep over the indexed probes; on a growing source each
    /// probe is demand.
    /// </summary>
    public static async IAsyncEnumerable<THandle> GetHandles<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var pending = new Stack<THandle>();

      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootResult = await source.GetRootAtAsync(rootIndex).ConfigureAwait(false);

        if (!rootResult.HasChild)
          break;

        pending.Push(rootResult.Child.Node);
      }

      while (pending.Count > 0)
      {
        var current = pending.Pop();

        yield return current;

        for (var childIndex = 0; ; childIndex++)
        {
          var childResult = await source.GetChildAtAsync(current, childIndex).ConfigureAwait(false);

          if (!childResult.HasChild)
            break;

          pending.Push(childResult.Child.Node);
        }
      }
    }

    /// <summary>
    /// The acquisition scan: every handle paired with the value it labels, in the same
    /// deliberately unspecified order as <see cref="GetHandles{TValue, THandle}"/>. The rows
    /// let value predicates pick out handles -- consumer-side, preserving the
    /// no-node-equality pledge (the library compares nothing; the consumer's predicate is the
    /// consumer's business).
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
