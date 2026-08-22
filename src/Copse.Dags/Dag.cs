using System;
using System.Collections.Generic;
using System.Linq;

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
  /// <see cref="GetTopologicalOrder"/> view below is that same walk drained into a list of
  /// nodes -- a completed list IS a drain, so it throws where the drain starves.)
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
    /// also the "distinct nodes" enumeration -- sum over it for shared-counted-once semantics):
    /// the entry order of the builder's one walk, read as nodes -- deterministic, discovery-biased
    /// (sources first-to-last, siblings first-to-last) wherever the edge constraints allow. A
    /// completed list is a drain, so a cyclic graph throws <see cref="DagCycleException"/> here at
    /// the starvation point, the loop named. This is the owned-node view; the contract-level
    /// value view is the <c>GetTopologicalOrder</c> extension on
    /// <see cref="IDagnumerable{TNode, TEdge}"/> -- on a builder receiver THIS overload binds.
    /// </summary>
    public IReadOnlyList<DagNode<TValue, TEdge>> GetTopologicalOrder()
    {
      var topologicalOrder = new List<DagNode<TValue, TEdge>>();
      using var walk = new TopologyWalkDagnumerator<TValue, DagNode<TValue, TEdge>, TEdge>(new DagNodeTopology<TValue, TEdge>(_Sources), _Sources);

      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
        if (walk.Mode == DagnumeratorMode.EnteringNode)
          topologicalOrder.Add(walk.CurrentHandle);

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

  }
}
