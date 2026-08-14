using Copse.Async;
using System.Collections.Generic;
using System.Threading;

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
        var rootResult = await source.TryGetRootAtAsync(rootIndex).ConfigureAwait(false);

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
          var childResult = await source.TryGetChildAtAsync(current, childIndex).ConfigureAwait(false);

          if (!childResult.HasChild)
            break;

          pending.Push(childResult.Child.Node);
        }
      }
    }
  }
}
