using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;

namespace Copse.Dags.Tests
{
  /// <summary>
  /// The conformance oracle (the EngineTree precedent -- the oracle lives with the tests so the
  /// product carries ONE spelling of every operator, the contract's): independent
  /// implementations of four contract operators' semantics over the owned nodes directly --
  /// non-destructive shape clones, sharing and parallel edges preserved, payloads carried --
  /// which is what makes them worth diffing against: same answers, disjoint machinery. Only
  /// the operators a differential pin consumes live here: the two prunes and the two scans.
  /// </summary>
  public static class OracleDagOperations
  {
    /// <summary>
    /// Upward fold: children before parents, each node computed exactly once; a shared child's
    /// (single, reused) result appears in EACH parent's list, parallel edges twice; empty at
    /// sinks. The bare-list ancestor of the contract's edge-paired SinkfixScan.
    /// </summary>
    private static IReadOnlyDictionary<DagNode<TValue, TEdge>, TResult> OracleSinkfixAggregate<TValue, TEdge, TResult>(
      this Dag<TValue, TEdge> dag,
      Func<DagNode<TValue, TEdge>, IReadOnlyList<TResult>, TResult> aggregate)
    {
      var resultsByNode = NewNodeKeyedDictionary<TValue, TEdge, TResult>();

      foreach (var node in dag.GetTopologicalOrder().Reverse())
      {
        var childResults = node.Children.Select(child => resultsByNode[child]).ToList();
        resultsByNode.Add(node, aggregate(node, childResults));
      }

      return resultsByNode;
    }

    /// <summary>
    /// Downward allocation: every parent contributes before a node is processed;
    /// <paramref name="mergeInflows"/> receives one inflow per in-edge (empty at sources --
    /// that call seeds); <paramref name="allocateToChildren"/> must return exactly one outflow
    /// per out-edge. The ancestor of the contract's SourcefixDispatch.
    /// </summary>
    private static IReadOnlyDictionary<DagNode<TValue, TEdge>, TAllocation> OracleSourcefixAllocate<TValue, TEdge, TAllocation>(
      this Dag<TValue, TEdge> dag,
      Func<DagNode<TValue, TEdge>, IReadOnlyList<TAllocation>, TAllocation> mergeInflows,
      Func<DagNode<TValue, TEdge>, TAllocation, IReadOnlyList<TAllocation>> allocateToChildren)
    {
      var allocationsByNode = NewNodeKeyedDictionary<TValue, TEdge, TAllocation>();
      var inflowsByNode = NewNodeKeyedDictionary<TValue, TEdge, List<TAllocation>>();

      foreach (var node in dag.GetTopologicalOrder())
      {
        inflowsByNode.TryGetValue(node, out var inflows);
        var mergedAllocation = mergeInflows(node, (IReadOnlyList<TAllocation>)inflows ?? Array.Empty<TAllocation>());
        allocationsByNode.Add(node, mergedAllocation);

        if (node.Children.Count == 0)
          continue;

        var outflows = allocateToChildren(node, mergedAllocation);

        if (outflows == null || outflows.Count != node.Children.Count)
          throw new InvalidOperationException(
            $"allocateToChildren must return exactly one outflow per out-edge: " +
            $"expected {node.Children.Count} for node '{node}', got {(outflows == null ? "null" : outflows.Count.ToString())}.");

        for (var edgeIndex = 0; edgeIndex < node.Children.Count; edgeIndex++)
        {
          var child = node.Children[edgeIndex];

          if (!inflowsByNode.TryGetValue(child, out var childInflows))
          {
            childInflows = new List<TAllocation>();
            inflowsByNode.Add(child, childInflows);
          }

          childInflows.Add(outflows[edgeIndex]);
        }
      }

      return allocationsByNode;
    }

    /// <summary>Downward cumulative scan: OracleSourcefixAllocate with uniform outflows (bare-list inflows).</summary>
    public static Dag<TResult, TEdge> OracleSourcefixScan<TValue, TEdge, TResult>(
      this Dag<TValue, TEdge> dag,
      Func<DagNode<TValue, TEdge>, IReadOnlyList<TResult>, TResult> accumulate)
    {
      var accumulationsByNode = dag.OracleSourcefixAllocate(
        mergeInflows: accumulate,
        allocateToChildren: (node, accumulation) =>
          Enumerable.Repeat(accumulation, node.Children.Count).ToList());

      return dag.CloneShape(node => accumulationsByNode[node], (parent, edge) => edge.Value);
    }

    /// <summary>Upward cumulative scan: OracleSinkfixAggregate as a shape-isomorphic dag.</summary>
    public static Dag<TResult, TEdge> OracleSinkfixScan<TValue, TEdge, TResult>(
      this Dag<TValue, TEdge> dag,
      Func<DagNode<TValue, TEdge>, IReadOnlyList<TResult>, TResult> aggregate)
    {
      var resultsByNode = dag.OracleSinkfixAggregate(aggregate);

      return dag.CloneShape(node => resultsByNode[node], (parent, edge) => edge.Value);
    }

    /// <summary>Prune polarity (true = prune): the node vanishes with every edge through it; survivors need another live path.</summary>
    public static Dag<TValue, TEdge> OraclePruneBefore<TValue, TEdge>(
      this Dag<TValue, TEdge> dag,
      Func<DagNode<TValue, TEdge>, bool> prune)
      => dag.ClonePruned(prune, keepMatchedNode: false);

    /// <summary>As OraclePruneBefore but the matched node is kept as a sink (out-edges cut).</summary>
    public static Dag<TValue, TEdge> OraclePruneAfter<TValue, TEdge>(
      this Dag<TValue, TEdge> dag,
      Func<DagNode<TValue, TEdge>, bool> prune)
      => dag.ClonePruned(prune, keepMatchedNode: true);

    // The shared prune walk: one pass down the topological order decides each node's fate, then
    // a clone of the surviving subgraph.
    private static Dag<TValue, TEdge> ClonePruned<TValue, TEdge>(
      this Dag<TValue, TEdge> dag,
      Func<DagNode<TValue, TEdge>, bool> prune,
      bool keepMatchedNode)
    {
      var topologicalOrder = dag.GetTopologicalOrder();

      var sourceSet = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      foreach (var source in dag.Sources)
        sourceSet.Add(source);

      var nodesWithLiveInEdge = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      var survivingNodes = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      var nodesWithCutChildren = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);

      foreach (var node in topologicalOrder)
      {
        var reachable = sourceSet.Contains(node) || nodesWithLiveInEdge.Contains(node);

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

      return new Dag<TValue, TEdge>(dag.Sources.Where(survivingNodes.Contains).Select(source => cloneByNode[source]));
    }

    // The clone machinery: fresh wrapper nodes, every edge re-linked clone-to-clone, child-edge
    // order and parallel-edge multiplicity preserved, shared nodes cloned once.
    private static Dag<TResultValue, TResultEdge> CloneShape<TValue, TEdge, TResultValue, TResultEdge>(
      this Dag<TValue, TEdge> dag,
      Func<DagNode<TValue, TEdge>, TResultValue> valueForNode,
      Func<DagNode<TValue, TEdge>, DagEdge<TValue, TEdge>, TResultEdge> valueForEdge)
    {
      var topologicalOrder = dag.GetTopologicalOrder();
      var cloneByNode = new Dictionary<DagNode<TValue, TEdge>, DagNode<TResultValue, TResultEdge>>(ReferenceEqualityComparer.Instance);

      foreach (var node in topologicalOrder)
        cloneByNode.Add(node, new DagNode<TResultValue, TResultEdge>(valueForNode(node)));

      foreach (var node in topologicalOrder)
        foreach (var edge in node.ChildEdges)
          cloneByNode[node].AddChild(cloneByNode[edge.Child], valueForEdge(node, edge));

      return new Dag<TResultValue, TResultEdge>(dag.Sources.Select(source => cloneByNode[source]));
    }

    private static Dictionary<DagNode<TValue, TEdge>, TEntry> NewNodeKeyedDictionary<TValue, TEdge, TEntry>() =>
      new Dictionary<DagNode<TValue, TEdge>, TEntry>(ReferenceEqualityComparer.Instance);
  }

}
