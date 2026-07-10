using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Dags
{
  /// <summary>
  /// The LINQ-style operators: unlike <see cref="SortChildrenBy{TKey}"/> (an in-place edge
  /// reorder), every operator here is NON-destructive -- it returns a NEW dag of fresh
  /// <see cref="DagNode{TValue, TEdge}"/> wrappers (the consumer's node and edge values are
  /// carried by reference, never copied or compared) and leaves the source graph untouched.
  /// Shape clones preserve sharing: a node with two parents in the source is ONE node with two
  /// parents in the result, parallel edges survive per edge, and every surviving edge keeps its
  /// payload -- which is what lets a prune compose with a payload-driven pass downstream.
  /// </summary>
  public sealed partial class Dag<TValue, TEdge>
  {
    /// <summary>
    /// Shape-preserving map over NODE values: the result dag is edge-for-edge isomorphic to this
    /// one (edge payloads carried), with each node's value replaced by <paramref name="selector"/>'s
    /// result. The selector runs exactly once per node -- sharing means a two-parent node is
    /// mapped once, not once per path -- and it receives the NODE, so "aggregate across children"
    /// is just a selector that reads <see cref="DagNode{TValue, TEdge}.Children"/> or
    /// <see cref="DagNode{TValue, TEdge}.ChildEdges"/> (for the recursive version use
    /// <see cref="LeaffixAggregate{TResult}"/> / <see cref="LeaffixScan{TResult}"/>).
    /// </summary>
    public Dag<TResult, TEdge> Select<TResult>(Func<DagNode<TValue, TEdge>, TResult> selector)
    {
      if (selector == null)
        throw new ArgumentNullException(nameof(selector));

      return CloneShape(selector, (parent, edge) => edge.Value);
    }

    /// <summary>
    /// The edge-side dual of <see cref="Select{TResult}"/>: node values carried, each edge's
    /// payload replaced by <paramref name="edgeSelector"/>'s result (it receives the owning
    /// parent and the whole edge, once per edge).
    /// </summary>
    public Dag<TValue, TEdgeResult> SelectEdges<TEdgeResult>(
      Func<DagNode<TValue, TEdge>, DagEdge<TValue, TEdge>, TEdgeResult> edgeSelector)
    {
      if (edgeSelector == null)
        throw new ArgumentNullException(nameof(edgeSelector));

      return CloneShape(node => node.Value, edgeSelector);
    }

    /// <summary>
    /// Downward cumulative scan. The tree scan's single parent accumulation generalizes to a DAG
    /// as one accumulation PER IN-EDGE: <paramref name="accumulate"/> receives the node and all
    /// its parents' accumulated results (empty at roots -- that call seeds the scan; parallel
    /// edges contribute the parent's result twice) and returns the node's accumulated result.
    /// This is exactly <see cref="RootfixAllocate{TAllocation}"/> with uniform outflows -- a scan
    /// COPIES its accumulation down every out-edge where an allocation SPLITS it.
    /// Returns the shape-isomorphic dag of accumulated results, edge payloads carried.
    /// </summary>
    public Dag<TResult, TEdge> RootfixScan<TResult>(
      Func<DagNode<TValue, TEdge>, IReadOnlyList<TResult>, TResult> accumulate)
    {
      if (accumulate == null)
        throw new ArgumentNullException(nameof(accumulate));

      var accumulationsByNode = RootfixAllocate(
        mergeInflows: accumulate,
        allocateToChildren: (node, accumulation) =>
          Enumerable.Repeat(accumulation, node.Children.Count).ToList());

      return CloneShape(node => accumulationsByNode[node], (parent, edge) => edge.Value);
    }

    /// <summary>
    /// Upward cumulative scan: <see cref="LeaffixAggregate{TResult}"/> (same per-edge child-result
    /// semantics, each node computed exactly once) returned as the shape-isomorphic dag of results
    /// instead of a dictionary, so it composes with further operators. Edge payloads carried.
    /// </summary>
    public Dag<TResult, TEdge> LeaffixScan<TResult>(
      Func<DagNode<TValue, TEdge>, IReadOnlyList<TResult>, TResult> aggregate)
    {
      if (aggregate == null)
        throw new ArgumentNullException(nameof(aggregate));

      var resultsByNode = LeaffixAggregate(aggregate);

      return CloneShape(node => resultsByNode[node], (parent, edge) => edge.Value);
    }

    /// <summary>
    /// Removal-semantics filter, Copse polarity: PRUNE WHEN TRUE. A pruned node vanishes along
    /// with every edge through it; a descendant survives only while it is still reachable from a
    /// root through surviving nodes -- so a shared descendant with another live path stays (with
    /// just its surviving in-edges, payloads intact), and a pruned root drops its whole exclusive
    /// component. The predicate runs at most once per node and never for a node all of whose
    /// paths are already severed.
    /// </summary>
    public Dag<TValue, TEdge> PruneBefore(Func<DagNode<TValue, TEdge>, bool> prune)
    {
      if (prune == null)
        throw new ArgumentNullException(nameof(prune));

      return ClonePruned(prune, keepMatchedNode: false);
    }

    /// <summary>
    /// As <see cref="PruneBefore"/> but the matched node itself is KEPT (as a leaf) -- only its
    /// out-edges are cut. Its former children survive if reachable via another live path.
    /// </summary>
    public Dag<TValue, TEdge> PruneAfter(Func<DagNode<TValue, TEdge>, bool> prune)
    {
      if (prune == null)
        throw new ArgumentNullException(nameof(prune));

      return ClonePruned(prune, keepMatchedNode: true);
    }

    /// <summary>
    /// The shared prune walk. One pass down the topological order decides each node's fate --
    /// a node is reachable when it is a root or some surviving, non-cut parent has an edge to it;
    /// reachable nodes are then either dropped (PruneBefore match), kept with out-edges cut
    /// (PruneAfter match), or kept whole -- followed by a clone of the surviving subgraph.
    /// </summary>
    private Dag<TValue, TEdge> ClonePruned(Func<DagNode<TValue, TEdge>, bool> prune, bool keepMatchedNode)
    {
      var topologicalOrder = GetTopologicalOrder();

      var rootSet = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      foreach (var root in _Roots)
        rootSet.Add(root);

      var nodesWithLiveInEdge = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      var survivingNodes = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      var nodesWithCutChildren = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);

      foreach (var node in topologicalOrder)
      {
        var reachable = rootSet.Contains(node) || nodesWithLiveInEdge.Contains(node);

        if (!reachable)
          continue;

        var matched = prune(node);

        if (matched && !keepMatchedNode)
          continue;

        survivingNodes.Add(node);

        if (matched)
        {
          nodesWithCutChildren.Add(node);
          continue;
        }

        foreach (var child in node.Children)
          nodesWithLiveInEdge.Add(child);
      }

      var cloneByNode = new Dictionary<DagNode<TValue, TEdge>, DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);

      foreach (var node in topologicalOrder)
        if (survivingNodes.Contains(node))
          cloneByNode.Add(node, new DagNode<TValue, TEdge>(node.Value));

      foreach (var node in topologicalOrder)
      {
        if (!survivingNodes.Contains(node) || nodesWithCutChildren.Contains(node))
          continue;

        foreach (var edge in node.ChildEdges)
          if (survivingNodes.Contains(edge.Child))
            cloneByNode[node].AddChild(cloneByNode[edge.Child], edge.Value);
      }

      return new Dag<TValue, TEdge>(_Roots.Where(survivingNodes.Contains).Select(root => cloneByNode[root]));
    }

    /// <summary>
    /// The clone machinery every operator above rides: fresh wrapper nodes valued by
    /// <paramref name="valueForNode"/>, then every edge re-linked clone-to-clone with the payload
    /// <paramref name="valueForEdge"/> chooses -- child-edge order and parallel-edge multiplicity
    /// preserved, shared nodes cloned once.
    /// </summary>
    private Dag<TResultValue, TResultEdge> CloneShape<TResultValue, TResultEdge>(
      Func<DagNode<TValue, TEdge>, TResultValue> valueForNode,
      Func<DagNode<TValue, TEdge>, DagEdge<TValue, TEdge>, TResultEdge> valueForEdge)
    {
      var topologicalOrder = GetTopologicalOrder();
      var cloneByNode = new Dictionary<DagNode<TValue, TEdge>, DagNode<TResultValue, TResultEdge>>(ReferenceEqualityComparer.Instance);

      foreach (var node in topologicalOrder)
        cloneByNode.Add(node, new DagNode<TResultValue, TResultEdge>(valueForNode(node)));

      foreach (var node in topologicalOrder)
        foreach (var edge in node.ChildEdges)
          cloneByNode[node].AddChild(cloneByNode[edge.Child], valueForEdge(node, edge));

      return new Dag<TResultValue, TResultEdge>(_Roots.Select(root => cloneByNode[root]));
    }
  }
}
