using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  // The edge-dual wrappers' shared plumbing: turns the walk's current visit into the full
  // relationship context (dispatching parent via the contiguity clause, discovered child, payload,
  // in-edge index via per-child arrival counters). Returns false for entries and for conventional
  // source discoveries -- neither is an edge.
  internal sealed class DagRelationshipTracker<TNode, TEdge>
  {
    private TNode _DispatchingValue;
    private int _DispatchingOrdinal = -1;
    private readonly Dictionary<int, int> _InEdgeCountsByOrdinal = new();

    public bool TryTrack(IDagnumerator<TNode, TEdge> walk, out DagEdgeContext<TNode, TEdge> relationship)
    {
      if (walk.Mode == DagnumeratorMode.EnteringNode)
      {
        _DispatchingValue = walk.Node;
        _DispatchingOrdinal = walk.Ordinal;
        relationship = default;
        return false;
      }

      if (walk.ParentOrdinal < 0)
      {
        relationship = default;
        return false;
      }

      if (walk.ParentOrdinal != _DispatchingOrdinal)
        throw new InvalidOperationException(
          "Non-contiguous dispatch: a discovery arrived from a node other than the last entered one.");

      _InEdgeCountsByOrdinal.TryGetValue(walk.Ordinal, out var inEdgeIndex);
      _InEdgeCountsByOrdinal[walk.Ordinal] = inEdgeIndex + 1;

      relationship = new DagEdgeContext<TNode, TEdge>(_DispatchingValue, walk.Node, walk.Edge, inEdgeIndex);
      return true;
    }
  }
}
