using System.Collections.Generic;

namespace Copse.Dags
{
  // The downstream cone, severed at one node -- the dag's re-rooted view (the tree family's
  // SubtreeWalkable, dualized) and the label type of the cofree duplicate (Downstreams): the
  // source seen with one node as the sole source, upward sight severed at the cone's boundary.
  // Nothing is copied, nothing re-addressed; handles are the source's own; out-edge groups
  // delegate untouched (the cone is closed downward). What a dag adds over the tree's "exactly
  // two answers rewritten" is MEMBERSHIP: a node inside the cone may have in-edges from
  // outside it (the diamond's venture, seen from left, has one parent, not two), so the in-edge
  // group is filtered to members -- the reachable set from the root over out-edges, one sweep
  // memoized at the first parent probe (O(reached), one allocation per view, never per step;
  // the descendant-information law's price, disclosed). The root's own in-edge group is empty
  // (its parent is this view's virtual source), and the source group is the single root.
  //
  // Handle equality is the dedup -- EqualityComparer<THandle>.Default, the contract's clause --
  // never value equality. Handles from outside the cone are not handed out by this view; probing
  // with one is answered by delegation with the filter applied, unspecified like any foreign
  // handle.
  internal sealed class DagDownstreamWalkable<TValue, THandle, TEdge>
    : DagLensWalkable<TValue, THandle, TEdge>
  {
    public DagDownstreamWalkable(IDagTopology<TValue, THandle, TEdge> source, THandle root)
    {
      _Source = source;
      _Root = root;
    }

    private readonly IDagTopology<TValue, THandle, TEdge> _Source;
    private readonly THandle _Root;
    private HashSet<THandle> _Members;

    public override TValue GetValue(THandle handle) => _Source.GetValue(handle);

    public override DagStep<THandle, TEdge> TryGetParentAt(THandle handle, int inEdgeIndex)
    {
      if (inEdgeIndex < 0 || EqualityComparer<THandle>.Default.Equals(handle, _Root))
        return default;

      EnsureMembers();

      var memberIndex = 0;

      for (var parentStep = _Source.TryGetParentAt(handle, 0); parentStep.HasValue; parentStep = _Source.TryGetParentAt(handle, parentStep.EdgeIndex + 1))
      {
        if (!_Members.Contains(parentStep.Handle))
          continue;

        if (memberIndex == inEdgeIndex)
          return new DagStep<THandle, TEdge>(parentStep.Handle, parentStep.Edge, inEdgeIndex);

        memberIndex++;
      }

      return default;
    }

    public override DagStep<THandle, TEdge> TryGetChildAt(THandle handle, int outEdgeIndex) => _Source.TryGetChildAt(handle, outEdgeIndex);

    public override DagStep<THandle, TEdge> TryGetSourceAt(int sourceIndex)
      => sourceIndex == 0
        ? new DagStep<THandle, TEdge>(_Root, default, 0)
        : default;

    private void EnsureMembers()
    {
      if (_Members != null)
        return;

      var members = new HashSet<THandle> { _Root };
      var pending = new Stack<THandle>();
      pending.Push(_Root);

      while (pending.Count > 0)
      {
        var handle = pending.Pop();

        for (var childStep = _Source.TryGetChildAt(handle, 0); childStep.HasValue; childStep = _Source.TryGetChildAt(handle, childStep.EdgeIndex + 1))
          if (members.Add(childStep.Handle))
            pending.Push(childStep.Handle);
      }

      _Members = members;
    }
  }
}
