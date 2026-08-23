using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The acquisition scan: every handle paired with the value it labels, each node once, order
    /// unspecified -- the rows of the labeling. Searches are consumer LINQ over these rows (the
    /// search law); the empty sequence is the miss, never a sentinel handle (ordinal handles
    /// include 0, a real node).
    /// </summary>
    public static IEnumerable<DagHandleAndValue<THandle, TValue>> GetHandlesWithValues<TValue, THandle, TEdge>(
      this IWalkableDagnumerable<TValue, THandle, TEdge> source)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));

      return HandlesWithValuesOf(source);
    }

    private static IEnumerable<DagHandleAndValue<THandle, TValue>> HandlesWithValuesOf<TValue, THandle, TEdge>(
      IWalkableDagnumerable<TValue, THandle, TEdge> source)
    {
      foreach (var stance in Stances(source))
        yield return new DagHandleAndValue<THandle, TValue>(stance.Focus, stance.GetValue());
    }
  }
}
