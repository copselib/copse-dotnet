using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Copse.Dags
{
  /// <summary>
  /// The owned, mutation-friendly builder -- a DAG held by its SOURCE nodes (in-degree zero;
  /// the graph-theoretic vocabulary: sources and sinks, design-docs/DAG_CONTRACT_DESIGN.md) -- and the
  /// family's concrete <see cref="IDagnumerable{TNode, TEdge}"/>. Acquisition is LAZY (THE
  /// LAZY BUILDER RULING): Kahn on demand over the live node graph, no snapshot,
  /// no cycle check -- a cyclic graph streams its maximal acyclic prefix and throws
  /// <see cref="DagCycleException"/> at exhaustion; <c>Materialize</c> is the validator and
  /// the completed buffer is the certificate. (The owned-node
  /// <see cref="GetTopologicalOrder"/> view below remains eager and cycle-throwing -- it
  /// returns a completed list, so it IS a drain.)
  ///
  /// <para>Deliberately NOT a frozen snapshot: it holds only the sources, and every
  /// acquisition walks the live node graph. Mutate the nodes -- relink, sort children -- and
  /// the next acquisition just sees the new shape; there is no invalidation protocol to get
  /// wrong ("is acyclic" is a predicate of a DRAIN, never of this mutable object). Perf is
  /// explicitly not a goal of this tier.</para>
  /// </summary>
  public sealed partial class Dag<TValue, TEdge>
  {
    public Dag(params DagNode<TValue, TEdge>[] sources)
      : this((IEnumerable<DagNode<TValue, TEdge>>)sources)
    {
    }

    public Dag(IEnumerable<DagNode<TValue, TEdge>> sources)
    {
      if (sources == null)
        throw new ArgumentNullException(nameof(sources));

      _Sources = sources.ToList();

      if (_Sources.Any(source => source == null))
        throw new ArgumentException("Sources must not contain null.", nameof(sources));
    }

    private readonly List<DagNode<TValue, TEdge>> _Sources;

    public IReadOnlyList<DagNode<TValue, TEdge>> Sources => _Sources;


    /// <summary>
    /// Every node reachable from the sources, parents before children, each exactly once (this is
    /// also the "distinct nodes" enumeration -- sum over it for shared-counted-once semantics).
    /// Deterministic, and biased toward discovery order (sources first-to-last, siblings
    /// first-to-last) wherever the edge constraints allow.
    /// Throws <see cref="DagCycleException"/> if the reachable graph has a cycle.
    /// This is the owned-node view; the contract-level value view is the
    /// <c>GetTopologicalOrder</c> extension on <see cref="IDagnumerable{TNode, TEdge}"/>.
    /// </summary>
    public IReadOnlyList<DagNode<TValue, TEdge>> GetTopologicalOrder()
    {
      var topologicalOrder = GetPostorder();
      topologicalOrder.Reverse();
      return topologicalOrder;
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

    /// <summary>
    /// The discovery walk acquisition rides: iterative depth-first (an explicit frame stack,
    /// so recursion depth is bounded by heap not call stack) with a visited set for sharing and an
    /// on-path set for cycle detection. Children before parents in the returned list.
    /// </summary>
    private List<DagNode<TValue, TEdge>> GetPostorder()
    {
      var visitedNodes = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      var nodesOnPath = new HashSet<DagNode<TValue, TEdge>>(ReferenceEqualityComparer.Instance);
      var postorder = new List<DagNode<TValue, TEdge>>();
      var pathFrames = new List<PathFrame>();

      // Sources and children are walked in REVERSE so that the reversed postorder comes out in
      // discovery order (sources first-to-last, siblings first-to-last) wherever the edge
      // constraints allow -- a plain forward walk would put later siblings first.
      for (var sourceIndex = _Sources.Count - 1; sourceIndex >= 0; sourceIndex--)
      {
        var source = _Sources[sourceIndex];

        if (!visitedNodes.Add(source))
          continue;

        nodesOnPath.Add(source);
        pathFrames.Add(new PathFrame(source));

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
