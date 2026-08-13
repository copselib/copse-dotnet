using Copse.Async;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncWalkableTreenumerable
  {
    /// <summary>
    /// The acquisition front door: every handle whose value satisfies the predicate, in
    /// deliberately unspecified order. This is the rowid scan
    /// (<see cref="GetHandlesWithValues{TValue, THandle}"/>) with the idiom folded in --
    /// handles are ENUMERATED and predicated, never computed from values, so the library
    /// still compares nothing (the no-node-equality pledge): the predicate is consumer code,
    /// and an outside-supplied target list becomes a consumer-side set inside it, with
    /// whatever comparer the consumer likes.
    ///
    /// <para>CAUTION on the miss: do not follow this with <c>FirstOrDefault()</c> -- ordinal
    /// handle spaces start at ZERO, so the default on a miss is a REAL node (the first one).
    /// For "the first match or an honest miss," use the single-handle form, whose result
    /// struct makes the miss a fact.</para>
    /// </summary>
    public static async IAsyncEnumerable<THandle> FindHandles<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      Func<TValue, bool> predicate,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      await foreach (var row in source.GetHandlesWithValues().ConfigureAwait(false))
      {
        if (predicate(row.Value))
          yield return row.Handle;
      }
    }

    /// <summary>
    /// The single-handle search: the first handle (in the scan's unspecified order) whose
    /// value satisfies the predicate, or an empty result. Result-typed BECAUSE the miss is
    /// otherwise unrepresentable -- see <see cref="HandleResult{THandle}"/>'s sentinel-collision
    /// clause. When several nodes match and the choice matters, the order is not yours to
    /// lean on: use the plural form and pick deliberately.
    /// </summary>
    public static async ValueTask<HandleResult<THandle>> FindHandleAsync<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      Func<TValue, bool> predicate)
    {
      await foreach (var row in source.GetHandlesWithValues().ConfigureAwait(false))
      {
        if (predicate(row.Value))
          return new HandleResult<THandle>(row.Handle);
      }

      return default;
    }
  }
}
