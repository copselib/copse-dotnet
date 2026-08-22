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

      return GetHandlesWithValuesIterator(source);
    }

    private static IEnumerable<DagHandleAndValue<THandle, TValue>> GetHandlesWithValuesIterator<TValue, THandle, TEdge>(
      IWalkableDagnumerable<TValue, THandle, TEdge> source)
    {
      var door = source.GetDagWalker();
      var seen = new HashSet<THandle>();
      var pending = new Stack<DagWalker<TValue, THandle, TEdge>>();

      for (var sourceIndex = 0; ; sourceIndex++)
      {
        var sourceStance = door.MoveToChild(sourceIndex);

        if (!sourceStance.HasValue)
          break;

        if (seen.Add(sourceStance.Value.Focus))
          pending.Push(sourceStance.Value);
      }

      while (pending.Count > 0)
      {
        var stance = pending.Pop();

        yield return new DagHandleAndValue<THandle, TValue>(stance.Focus, stance.GetValue());

        for (var outEdgeIndex = 0; ; outEdgeIndex++)
        {
          var childStance = stance.MoveToChild(outEdgeIndex);

          if (!childStance.HasValue)
            break;

          if (seen.Add(childStance.Value.Focus))
            pending.Push(childStance.Value);
        }
      }
    }
  }
}
