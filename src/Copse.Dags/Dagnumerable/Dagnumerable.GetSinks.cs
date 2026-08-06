using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The nodes with no out-edges -- where flow ends, GetLeaves' dag analog -- in topological
    /// order: the transpose's GetSources, without paying for the transpose. A sink is a
    /// whole-stream fact, so the drain consumes the full walk -- but dispatch contiguity (a
    /// discovery's parent is always the LATEST entered node) collapses "did this node ever
    /// dispatch" to one bit about one pending node, so the state is O(1), not O(V). Deferred:
    /// the walk is acquired on first enumeration.
    /// </summary>
    public static IEnumerable<TNode> GetSinks<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source)
    {
      using var walk = source.GetDagnumerator();

      var hasPending = false;
      var pendingDispatched = false;
      var pending = default(TNode);

      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
      {
        if (walk.Mode == DagnumeratorMode.EnteringNode)
        {
          if (hasPending && !pendingDispatched)
            yield return pending;

          pending = walk.Node;
          hasPending = true;
          pendingDispatched = false;
          continue;
        }

        if (walk.ParentOrdinal >= 0)
          pendingDispatched = true;
      }

      if (hasPending && !pendingDispatched)
        yield return pending;
    }
  }
}
