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
      this IDagnumerable<TNode, TEdge> source)
    {
      using var walk = source.GetDagnumerator();
      var relationshipContext = new DagRelationshipTracker<TNode, TEdge>();

      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
        if (relationshipContext.TryTrack(walk, out var relationship))
          yield return relationship;
    }
  }
}
