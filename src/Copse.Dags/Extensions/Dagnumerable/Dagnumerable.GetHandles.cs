using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Every handle the walkable's topology reaches from its sources, each ONCE, in deliberately
    /// unspecified order -- the SET is the promise (set semantics, the dag axis default: a shared
    /// node is one row however many paths reach it). A pure stance walk: one knock, the sources
    /// seeded from the unfocused stance's child group, dedup by handle equality. The unfocused
    /// stance gets no row -- it has no handle to record.
    /// </summary>
    public static IEnumerable<THandle> GetHandles<TValue, THandle, TEdge>(
      this IWalkableDagnumerable<TValue, THandle, TEdge> source)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));

      return HandlesOf(source);
    }

    private static IEnumerable<THandle> HandlesOf<TValue, THandle, TEdge>(IWalkableDagnumerable<TValue, THandle, TEdge> source)
    {
      foreach (var stance in Stances(source))
        yield return stance.Focus;
    }

    // Every node's stance exactly once, each source's reach before the next's (a depth-first
    // order over the handle set; parents are not guaranteed before children).
    internal static IEnumerable<DagWalker<TValue, THandle, TEdge>> Stances<TValue, THandle, TEdge>(
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

        yield return stance;

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
