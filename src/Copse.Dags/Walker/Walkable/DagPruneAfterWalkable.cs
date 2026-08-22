using System;

namespace Copse.Dags
{
  // The restriction lens: PruneNodesAfter over a walkable, as a PAIR -- the ORDER half is the shipped
  // streaming operator, delegated wholesale, and the ADJACENCY half is the rewritten groups: a
  // matched node hands out no out-edges, and every in-edge group omits the edges from matched
  // parents (the pair agree: an edge a matched node does not dispatch does not exist, in either
  // direction -- transpose consistency on everything this view hands out). The in-edge filter
  // costs one predicate evaluation per in-edge scanned; the out-edge and source probes are one
  // evaluation or none. GetValue and the source group delegate untouched -- prune-after keeps the
  // matched handle and its ancestry, and sources always survive.
  //
  // Handle stance (lens semantics): the lens restricts what it HANDS OUT, not what arithmetic can
  // name -- a node the stream starves (every in-edge from matched parents) is still addressable
  // by a guessed handle, and answers as a source of this view. Handles obtained from this
  // walkable's probes never cross the boundary.
  internal sealed class DagPruneAfterWalkable<TValue, THandle, TEdge>
    : IWalkableDagnumerable<TValue, THandle, TEdge>, IDagTopology<TValue, THandle, TEdge>
  {
    public DagPruneAfterWalkable(
      IWalkableDagnumerable<TValue, THandle, TEdge> source,
      Func<TValue, bool> predicate)
    {
      _Source = DagTopology.Lazy(source);
      _Predicate = predicate;
      // Via the streaming EXTENSION on the plain contract, so the walkable overload's own
      // caller does not win betterness and recurse.
      _PrunedStream = ((IDagnumerable<TValue, TEdge>)source).PruneNodesAfter(predicate);
    }

    private readonly IDagTopology<TValue, THandle, TEdge> _Source;
    private readonly Func<TValue, bool> _Predicate;
    private readonly IDagnumerable<TValue, TEdge> _PrunedStream;

    public IDagnumerator<TValue, TEdge> GetDagnumerator() => _PrunedStream.GetDagnumerator();

    public TValue GetValue(THandle handle) => _Source.GetValue(handle);

    public DagStep<THandle, TEdge> TryGetParentAt(THandle handle, int inEdgeIndex)
    {
      if (inEdgeIndex < 0)
        return default;

      var survivingIndex = 0;

      for (var parentStep = _Source.TryGetParentAt(handle, 0); parentStep.HasValue; parentStep = _Source.TryGetParentAt(handle, parentStep.EdgeIndex + 1))
      {
        if (_Predicate(_Source.GetValue(parentStep.Handle)))
          continue;

        if (survivingIndex == inEdgeIndex)
          return new DagStep<THandle, TEdge>(parentStep.Handle, parentStep.Edge, inEdgeIndex);

        survivingIndex++;
      }

      return default;
    }

    public DagStep<THandle, TEdge> TryGetChildAt(THandle handle, int outEdgeIndex)
      => _Predicate(_Source.GetValue(handle))
        ? default
        : _Source.TryGetChildAt(handle, outEdgeIndex);

    public DagStep<THandle, TEdge> TryGetSourceAt(int sourceIndex) => _Source.TryGetSourceAt(sourceIndex);

    public DagWalker<TValue, THandle, TEdge> GetDagWalker()
      => new DagWalker<TValue, THandle, TEdge>(this);
  }
}
