using Copse.Async;
using System.Collections.Generic;
using System.Threading;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Every handle the walkable's topology reaches from its roots, in DELIBERATELY UNSPECIFIED
    /// order -- the SET is the promise (handles are positional identity made portable; recording
    /// them while consuming is the sanctioned acquisition path, since the library never searches
    /// by value). A stance walk (Stage B): doors and steps only -- the walk stands at every
    /// node and records where it stood; on a growing source each step is demand.
    /// </summary>
    public static async IAsyncEnumerable<THandle> GetHandles<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var pending = new Stack<AsyncTreeWalker<TValue, THandle>>();

      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootStance = await source.TryGetTreeWalkerAtRootIndexAsync(rootIndex).ConfigureAwait(false);

        if (!rootStance.HasValue)
          break;

        pending.Push(rootStance.Value);
      }

      while (pending.Count > 0)
      {
        var stance = pending.Pop();

        yield return stance.Focus;

        for (var childIndex = 0; ; childIndex++)
        {
          var step = await stance.MoveToChildAsync(childIndex).ConfigureAwait(false);

          if (!step.HasValue)
            break;

          pending.Push(step.Value);
        }
      }
    }
  }
}
