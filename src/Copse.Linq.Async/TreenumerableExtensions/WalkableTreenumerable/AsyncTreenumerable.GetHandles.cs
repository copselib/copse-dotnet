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
      // One knock; the roots are the unfocused stance's child group. The unfocused stance itself gets no
      // row -- it has no handle to record, so the value-level scan excludes it by type.
      var door = await source.GetTreeWalkerAsync().ConfigureAwait(false);
      var pending = new Stack<AsyncTreeWalker<TValue, THandle>>();

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

        yield return stance.Focus;

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
