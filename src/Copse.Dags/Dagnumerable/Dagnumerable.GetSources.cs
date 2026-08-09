using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The nodes with no in-edges -- where flow begins, GetRoots' dag analog -- in topological
    /// order. O(1) state and an EARLY EXIT: the protocol discovers every source at the start of
    /// enumeration (ParentOrdinal -1, before anything else), so this drain reads that prefix
    /// and stops at the first non-source event without walking the rest of the dag. The prefix
    /// survives every wrapper: pruning severs edges and the liveness fold kills what loses its
    /// last path, so no operator ever creates a mid-stream source (the same fact that makes a
    /// streaming TakeDownstreamWhere a contract amendment rather than a wrapper). Deferred: the walk
    /// is acquired on first enumeration -- and the early exit means a CYCLIC graph's sources
    /// still stream fine (the lazy builder ruling: cycles surface as starvation at exhaustion,
    /// which this drain never reaches).
    /// </summary>
    public static IEnumerable<TNode> GetSources<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source)
    {
      using var walk = source.GetDagnumerator();

      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
      {
        if (walk.Mode != DagnumeratorMode.DiscoveringNode || walk.ParentOrdinal >= 0)
          yield break;

        yield return walk.Node;
      }
    }
  }
}
