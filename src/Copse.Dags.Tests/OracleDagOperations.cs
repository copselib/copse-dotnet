using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;

namespace Copse.Dags.Tests
{
  /// <summary>
  /// The conformance oracle: the spike-era builder operators, relocated out of the product (the
  /// EngineTree precedent -- the oracle lives with the tests so the product carries ONE spelling
  /// of every operator, the contract's). Each is an independent implementation of its contract
  /// twin's semantics over the owned nodes directly -- non-destructive shape clones, sharing and
  /// parallel edges preserved, payloads carried -- which is exactly what makes them worth
  /// diffing against: same answers, disjoint machinery.
  /// </summary>
  public static class OracleDagOperations
  {
    /// <summary>Shape-preserving map over node values; the selector runs once per node, shared or not.</summary>
    public static Dag<TResult, TEdge> OracleSelect<TValue, TEdge, TResult>(
      this Dag<TValue, TEdge> dag,
      Func<DagNode<TValue, TEdge>, TResult> selector)
      => dag.CloneShape(selector, (parent, edge) => edge.Value);

    /// <summary>The edge-side dual: node values carried, payloads replaced.</summary>
    public static Dag<TValue, TEdgeResult> OracleSelectEdges<TValue, TEdge, TEdgeResult>(
      this Dag<TValue, TEdge> dag,
      Func<DagNode<TValue, TEdge>, DagEdge<TValue, TEdge>, TEdgeResult> edgeSelector)
      => dag.CloneShape(node => node.Value, edgeSelector);

    /// <summary>
    /// Upward fold: children before parents, each node computed exactly once; a shared child's
    /// (single, reused) result appears in EACH parent's list, parallel edges twice; empty at
    /// sinks. The bare-list ancestor of the contract's edge-paired SinkfixScan.
    /// </summary>
    public static IReadOnlyDictionary<DagNode<TValue, TEdge>, TResult> OracleSinkfixAggregate<TValue, TEdge, TResult>(
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
    public static IReadOnlyDictionary<DagNode<TValue, TEdge>, TAllocation> OracleSourcefixAllocate<TValue, TEdge, TAllocation>(
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

    /// <summary>
    /// The survey-shaped downward pass over the builder: exactly-once per-edge dispatch slots;
    /// the ancestor of the contract's SourcefixDispatch, decorated as <see cref="DispatchNode{TValue, TDispatch}"/>.
    /// </summary>
    public static Dag<DispatchNode<TValue, TDispatch>, TEdge> OracleSourcefixDispatch<TValue, TEdge, TDispatch>(
      this Dag<TValue, TEdge> dag,
      Func<DagNode<TValue, TEdge>, IReadOnlyList<TDispatch>, TDispatch> mergeInflows,
      Action<DagNode<TValue, TEdge>, TDispatch, IReadOnlyList<DispatchTarget<TValue, TEdge, TDispatch>>> survey)
    {
      var dispatchedByNode = dag.OracleSourcefixAllocate(
        mergeInflows,
        allocateToChildren: (node, dispatched) =>
        {
          var targets = node.ChildEdges
            .Select(edge => new DispatchTarget<TValue, TEdge, TDispatch>(edge.Child, edge.Value))
            .ToList();

          survey(node, dispatched, targets);

          return targets.Select(target => target.GetDispatchedOrThrow()).ToList();
        });

      return dag.CloneShape(
        node => new DispatchNode<TValue, TDispatch>(node.Value, dispatchedByNode[node]),
        (parent, edge) => edge.Value);
    }

    /// <summary>Eager side-effect pass, parents before children, each node once.</summary>
    public static Dag<TValue, TEdge> OracleDo<TValue, TEdge>(
      this Dag<TValue, TEdge> dag,
      Action<DagNode<TValue, TEdge>> action)
    {
      foreach (var node in dag.GetTopologicalOrder())
        action(node);

      return dag;
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

  /// <summary>
  /// The oracle dispatch decoration (spike-era; the contract's DagDispatchNode superseded it in
  /// the product): a source value paired with what the pass delivered.
  /// </summary>
  public readonly struct DispatchNode<TValue, TDispatch>
  {
    public DispatchNode(TValue value, TDispatch dispatched)
    {
      Value = value;
      Dispatched = dispatched;
    }

    public readonly TValue Value;
    public readonly TDispatch Dispatched;

    public override string ToString() => $"{Value} <- {Dispatched}";
  }

  /// <summary>The oracle's exactly-once write-side handle (spike-era; see DagDispatchTarget for the contract's).</summary>
  public sealed class DispatchTarget<TValue, TEdge, TDispatch>
  {
    internal DispatchTarget(DagNode<TValue, TEdge> node, TEdge edge)
    {
      Node = node;
      Edge = edge;
    }

    public DagNode<TValue, TEdge> Node { get; }
    public TEdge Edge { get; }

    private bool _HasDispatched;
    private TDispatch _Dispatched;

    public void Dispatch(TDispatch value)
    {
      if (_HasDispatched)
        throw new InvalidOperationException(
          $"'{Node}' was dispatched to twice; each target accepts exactly one Dispatch per survey.");

      _HasDispatched = true;
      _Dispatched = value;
    }

    internal TDispatch GetDispatchedOrThrow()
    {
      if (!_HasDispatched)
        throw new InvalidOperationException(
          $"The survey completed without dispatching to '{Node}'; every out-edge must receive exactly one Dispatch.");

      return _Dispatched;
    }
  }
}
