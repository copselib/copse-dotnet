using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Every live edge exactly once, with both endpoints in scope -- the "one artifact per
    /// relationship" projection (transfers, export rows, validation lines). A discovery IS an
    /// edge visit: the walk already presents each edge once with the child's value and payload,
    /// and the dispatch-contiguity clause makes the parent the last-entered node, so this is a
    /// straight drain with O(1) extra state. Edges arrive in the walk's dispatch order (parents
    /// in topological order, each parent's out-edges in order). Deferred: the walk is acquired
    /// on first enumeration.
    /// </summary>
    public static IEnumerable<DagEdgeContext<TNode, TEdge>> GetEdges<TNode, TEdge>(
      this IForwardDagnumerable<TNode, TEdge> source)
    {
      using var walk = source.GetForwardDagnumerator();

      var dispatchingValue = default(TNode);
      var dispatchingOrdinal = -1;
      var inEdgeCountsByOrdinal = new Dictionary<int, int>();

      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
      {
        if (walk.Mode == DagnumeratorMode.EnteringNode)
        {
          dispatchingValue = walk.Node;
          dispatchingOrdinal = walk.Ordinal;
          continue;
        }

        // Conventional source discoveries have no parent -- they are not edges.
        if (walk.ParentOrdinal < 0)
          continue;

        if (walk.ParentOrdinal != dispatchingOrdinal)
          throw new InvalidOperationException(
            "Non-contiguous dispatch: a discovery arrived from a node other than the last entered one.");

        inEdgeCountsByOrdinal.TryGetValue(walk.Ordinal, out var inEdgeIndex);
        inEdgeCountsByOrdinal[walk.Ordinal] = inEdgeIndex + 1;

        yield return new DagEdgeContext<TNode, TEdge>(dispatchingValue, walk.Node, walk.Edge, inEdgeIndex);
      }
    }
  }
}
