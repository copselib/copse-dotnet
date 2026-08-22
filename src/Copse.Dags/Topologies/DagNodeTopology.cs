using System.Collections.Generic;

namespace Copse.Dags
{
  // The builder's topology: the live node graph, answered from DagNode's own edge lists with the
  // node as its handle (the self-sufficiency criterion -- a DagNode answers adjacency from itself).
  // Three answers bend to the builder's rules, all memoized at first need: a member may have a
  // STRAY parent outside the dag (the stray-parent affordance), whose edge is not the dag's, so the
  // in-edge group is filtered to members; the in-edge group is presented in DISCOVERY order --
  // the parents' entry order in the builder's own walk, the structural promise every in-group
  // keeps (DagNode's ParentEdges is insertion order, which the stream never shows) -- so the
  // first parent probe runs the walk once to learn entry order; and a listed source that another
  // member reaches is a member, not a source. Every door knock binds a FRESH topology: the
  // builder is mutable, and a walker sees the graph as it was when its door was knocked, as a
  // drain sees it when acquired. Mutate, then knock again.
  internal sealed class DagNodeTopology<TValue, TEdge> : IDagTopology<TValue, DagNode<TValue, TEdge>, TEdge>
  {
    public DagNodeTopology(IReadOnlyList<DagNode<TValue, TEdge>> listedSources)
    {
      _ListedSources = listedSources;
    }

    private readonly IReadOnlyList<DagNode<TValue, TEdge>> _ListedSources;
    private HashSet<DagNode<TValue, TEdge>> _Members;
    private List<DagNode<TValue, TEdge>> _Sources;
    private Dictionary<DagNode<TValue, TEdge>, int> _EntryOrder;
    private Dictionary<DagNode<TValue, TEdge>, DagParentEdge<TValue, TEdge>[]> _OrderedInEdges;

    public TValue GetValue(DagNode<TValue, TEdge> handle) => handle.Value;

    public DagStep<DagNode<TValue, TEdge>, TEdge> TryGetParentAt(DagNode<TValue, TEdge> handle, int inEdgeIndex)
    {
      if (inEdgeIndex < 0)
        return default;

      var inEdges = OrderedInEdges(handle);

      return inEdgeIndex < inEdges.Length
        ? new DagStep<DagNode<TValue, TEdge>, TEdge>(inEdges[inEdgeIndex].Parent, inEdges[inEdgeIndex].Value, inEdgeIndex)
        : default;
    }

    public DagStep<DagNode<TValue, TEdge>, TEdge> TryGetChildAt(DagNode<TValue, TEdge> handle, int outEdgeIndex)
    {
      var childEdges = handle.ChildEdges;

      if (outEdgeIndex < 0 || outEdgeIndex >= childEdges.Count)
        return default;

      return new DagStep<DagNode<TValue, TEdge>, TEdge>(childEdges[outEdgeIndex].Child, childEdges[outEdgeIndex].Value, outEdgeIndex);
    }

    public DagStep<DagNode<TValue, TEdge>, TEdge> TryGetSourceAt(int sourceIndex)
    {
      EnsureMembership();

      if (sourceIndex < 0 || sourceIndex >= _Sources.Count)
        return default;

      return new DagStep<DagNode<TValue, TEdge>, TEdge>(_Sources[sourceIndex], default, sourceIndex);
    }

    private DagParentEdge<TValue, TEdge>[] OrderedInEdges(DagNode<TValue, TEdge> handle)
    {
      EnsureMembership();

      if (_OrderedInEdges.TryGetValue(handle, out var ordered))
        return ordered;

      if (_EntryOrder == null)
        _EntryOrder = LearnEntryOrder();

      var memberEdges = new List<DagParentEdge<TValue, TEdge>>();

      foreach (var parentEdge in handle.ParentEdges)
        if (_Members.Contains(parentEdge.Parent))
          memberEdges.Add(parentEdge);

      // Stable by entry order: parallel edges from one parent keep their insertion order, which
      // is the parent's dispatch order.
      ordered = memberEdges.ToArray();
      var keys = new int[ordered.Length];
      for (var index = 0; index < ordered.Length; index++)
        keys[index] = _EntryOrder.TryGetValue(ordered[index].Parent, out var entry) ? entry * ordered.Length + index : int.MaxValue;
      System.Array.Sort(keys, ordered);

      _OrderedInEdges.Add(handle, ordered);

      return ordered;
    }

    // The walk over this very topology, drained once for the entry order that discovery order
    // follows (the walk reads only sources and out-edge groups, so no probe recurses here).
    private Dictionary<DagNode<TValue, TEdge>, int> LearnEntryOrder()
    {
      var entryOrder = new Dictionary<DagNode<TValue, TEdge>, int>(ReferenceEqualityComparer.Instance);
      using var walk = new TopologyWalkDagnumerator<TValue, DagNode<TValue, TEdge>, TEdge>(this, _ListedSources);

      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
        if (walk.Mode == DagnumeratorMode.EnteringNode)
          entryOrder[walk.CurrentHandle] = entryOrder.Count;

      return entryOrder;
    }

    private void EnsureMembership()
    {
      if (_Members != null)
        return;

      var members = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      var pointedTo = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      var sweep = new Stack<DagNode<TValue, TEdge>>();

      foreach (var listed in _ListedSources)
        if (members.Add(listed))
          sweep.Push(listed);

      while (sweep.Count > 0)
      {
        var member = sweep.Pop();

        foreach (var childEdge in member.ChildEdges)
        {
          pointedTo.Add(childEdge.Child);

          if (members.Add(childEdge.Child))
            sweep.Push(childEdge.Child);
        }
      }

      var sources = new List<DagNode<TValue, TEdge>>();

      foreach (var listed in _ListedSources)
        if (!pointedTo.Contains(listed) && !sources.Contains(listed))
          sources.Add(listed);

      _Sources = sources;
      _OrderedInEdges = new Dictionary<DagNode<TValue, TEdge>, DagParentEdge<TValue, TEdge>[]>(ReferenceEqualityComparer.Instance);
      _Members = members;
    }
  }
}
