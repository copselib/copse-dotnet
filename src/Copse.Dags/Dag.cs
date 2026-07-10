using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Copse.Dags
{
  /// <summary>
  /// A DAG rooted at one or more <see cref="DagNode{TValue, TEdge}"/>s. Deliberately NOT a frozen
  /// snapshot: it holds only the roots, and every operation re-walks the live node graph (one
  /// iterative depth-first pass with a visited set, validating acyclicity as it goes). Mutate the
  /// nodes -- relink, sort children -- and the next operation just sees the new shape; there is no
  /// invalidation protocol to get wrong. Perf is explicitly not a goal of this family.
  ///
  /// <para>The up/down passes come in the Copse rootfix/leaffix vocabulary, adapted to sharing:
  /// <see cref="LeaffixAggregate{TResult}"/> folds upward (children before parents),
  /// <see cref="RootfixAllocate{TAllocation}"/> pushes downward (a node receives one inflow per
  /// in-edge -- ALL parents contribute before the node distributes across its own out-edges,
  /// which is why these run over a materialized topological order rather than streaming).</para>
  /// </summary>
  public sealed partial class Dag<TValue, TEdge>
  {
    public Dag(params DagNode<TValue, TEdge>[] roots)
      : this((IEnumerable<DagNode<TValue, TEdge>>)roots)
    {
    }

    public Dag(IEnumerable<DagNode<TValue, TEdge>> roots)
    {
      if (roots == null)
        throw new ArgumentNullException(nameof(roots));

      _Roots = roots.ToList();

      if (_Roots.Any(root => root == null))
        throw new ArgumentException("Roots must not contain null.", nameof(roots));
    }

    private readonly List<DagNode<TValue, TEdge>> _Roots;

    public IReadOnlyList<DagNode<TValue, TEdge>> Roots => _Roots;

    /// <summary>
    /// Every node reachable from the roots, parents before children, each exactly once (this is
    /// also the "distinct nodes" enumeration -- sum over it for shared-counted-once semantics).
    /// Deterministic, and biased toward discovery order (roots first-to-last, siblings
    /// first-to-last) wherever the edge constraints allow.
    /// Throws <see cref="DagCycleException"/> if the reachable graph has a cycle.
    /// </summary>
    public IReadOnlyList<DagNode<TValue, TEdge>> GetTopologicalOrder()
    {
      var topologicalOrder = GetPostorder();
      topologicalOrder.Reverse();
      return topologicalOrder;
    }

    /// <summary>
    /// Folds upward: children complete before their parents, and each node's result is computed
    /// EXACTLY ONCE no matter how many parents share it. <paramref name="aggregate"/> receives the
    /// node and one child result per out-edge, in child-edge order -- so a shared child's (single,
    /// reused) result appears in the child-results of each of its parents, and parallel edges
    /// contribute it twice. That makes the diamond question the caller's explicit choice: combine
    /// the per-edge results for per-use ("roll-up") semantics, or ignore them and fold over
    /// <see cref="GetTopologicalOrder"/> for shared-counted-once semantics.
    /// Leaves receive an empty child-results list. Per-edge payloads are on
    /// <see cref="DagNode{TValue, TEdge}.ChildEdges"/>, index-aligned with the child results.
    /// </summary>
    public IReadOnlyDictionary<DagNode<TValue, TEdge>, TResult> LeaffixAggregate<TResult>(
      Func<DagNode<TValue, TEdge>, IReadOnlyList<TResult>, TResult> aggregate)
    {
      if (aggregate == null)
        throw new ArgumentNullException(nameof(aggregate));

      var resultsByNode = NewNodeKeyedDictionary<TResult>();

      foreach (var node in GetPostorder())
      {
        var childResults = node.Children.Select(child => resultsByNode[child]).ToList();
        resultsByNode.Add(node, aggregate(node, childResults));
      }

      return resultsByNode;
    }

    /// <summary>
    /// Pushes downward in topological order: every parent contributes before a node is processed.
    /// For each node, <paramref name="mergeInflows"/> receives one inflow per in-edge from a
    /// parent reachable in this dag (empty for roots -- that call seeds the allocation), and its
    /// result is the node's allocation. Non-leaves then have <paramref name="allocateToChildren"/>
    /// split that allocation into exactly one outflow per out-edge, in child-edge order (a wrong
    /// count throws) -- per-edge payloads are on <see cref="DagNode{TValue, TEdge}.ChildEdges"/>,
    /// index-aligned. Inflow order is deterministic: parents in topological order, edges in each
    /// parent's child-edge order. Returns each node's merged allocation.
    /// </summary>
    public IReadOnlyDictionary<DagNode<TValue, TEdge>, TAllocation> RootfixAllocate<TAllocation>(
      Func<DagNode<TValue, TEdge>, IReadOnlyList<TAllocation>, TAllocation> mergeInflows,
      Func<DagNode<TValue, TEdge>, TAllocation, IReadOnlyList<TAllocation>> allocateToChildren)
    {
      if (mergeInflows == null)
        throw new ArgumentNullException(nameof(mergeInflows));
      if (allocateToChildren == null)
        throw new ArgumentNullException(nameof(allocateToChildren));

      var allocationsByNode = NewNodeKeyedDictionary<TAllocation>();
      var inflowsByNode = NewNodeKeyedDictionary<List<TAllocation>>();

      foreach (var node in GetTopologicalOrder())
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

    /// <summary>
    /// Stably sorts EVERY reachable node's out-edges in place, ascending by a key of the child
    /// node -- each node once, even when shared. Purely an edge reorder (payloads travel with
    /// their edges); no back-links move.
    /// </summary>
    public void SortChildrenBy<TKey>(Func<DagNode<TValue, TEdge>, TKey> keySelector)
    {
      if (keySelector == null)
        throw new ArgumentNullException(nameof(keySelector));

      foreach (var node in GetTopologicalOrder())
        node.SortChildrenBy(keySelector);
    }

    /// <summary>As <see cref="SortChildrenBy{TKey}"/> keyed on whole edges, payload included.</summary>
    public void SortChildEdgesBy<TKey>(Func<DagEdge<TValue, TEdge>, TKey> keySelector)
    {
      if (keySelector == null)
        throw new ArgumentNullException(nameof(keySelector));

      foreach (var node in GetTopologicalOrder())
        node.SortChildEdgesBy(keySelector);
    }

    /// <summary>As <see cref="SortChildrenBy{TKey}"/> with an explicit comparer.</summary>
    public void SortChildren(IComparer<DagNode<TValue, TEdge>> comparer)
    {
      if (comparer == null)
        throw new ArgumentNullException(nameof(comparer));

      foreach (var node in GetTopologicalOrder())
        node.SortChildren(comparer);
    }

    /// <summary>
    /// The discovery walk everything above rides: iterative depth-first (an explicit frame stack,
    /// so recursion depth is bounded by heap not call stack) with a visited set for sharing and an
    /// on-path set for cycle detection. Children before parents in the returned list.
    /// </summary>
    private List<DagNode<TValue, TEdge>> GetPostorder()
    {
      var visitedNodes = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      var nodesOnPath = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      var postorder = new List<DagNode<TValue, TEdge>>();
      var pathFrames = new List<PathFrame>();

      // Roots and children are walked in REVERSE so that the reversed postorder comes out in
      // discovery order (roots first-to-last, siblings first-to-last) wherever the edge
      // constraints allow -- a plain forward walk would put later siblings first.
      for (var rootIndex = _Roots.Count - 1; rootIndex >= 0; rootIndex--)
      {
        var root = _Roots[rootIndex];

        if (!visitedNodes.Add(root))
          continue;

        nodesOnPath.Add(root);
        pathFrames.Add(new PathFrame(root));

        while (pathFrames.Count > 0)
        {
          var frameIndex = pathFrames.Count - 1;
          var frame = pathFrames[frameIndex];

          if (frame.NextChildIndex == frame.Node.Children.Count)
          {
            pathFrames.RemoveAt(frameIndex);
            nodesOnPath.Remove(frame.Node);
            postorder.Add(frame.Node);
            continue;
          }

          var child = frame.Node.Children[frame.Node.Children.Count - 1 - frame.NextChildIndex];
          frame.NextChildIndex++;
          pathFrames[frameIndex] = frame;

          if (nodesOnPath.Contains(child))
            throw new DagCycleException(DescribeCycle(pathFrames, child));

          if (!visitedNodes.Add(child))
            continue;

          nodesOnPath.Add(child);
          pathFrames.Add(new PathFrame(child));
        }
      }

      return postorder;
    }

    private static string DescribeCycle(List<PathFrame> pathFrames, DagNode<TValue, TEdge> reencounteredNode)
    {
      var cycleStartIndex = pathFrames.FindIndex(frame => ReferenceEquals(frame.Node, reencounteredNode));
      var cycleDescription = new StringBuilder("Cycle detected: ");

      for (var frameIndex = cycleStartIndex; frameIndex < pathFrames.Count; frameIndex++)
        cycleDescription.Append(pathFrames[frameIndex].Node).Append(" -> ");

      cycleDescription.Append(reencounteredNode);
      return cycleDescription.ToString();
    }

    private static Dictionary<DagNode<TValue, TEdge>, TEntry> NewNodeKeyedDictionary<TEntry>() =>
      new Dictionary<DagNode<TValue, TEdge>, TEntry>(ReferenceEqualityComparer.Instance);

    private struct PathFrame
    {
      public PathFrame(DagNode<TValue, TEdge> node)
      {
        Node = node;
        NextChildIndex = 0;
      }

      public readonly DagNode<TValue, TEdge> Node;
      public int NextChildIndex;
    }
  }
}
