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
  /// the completed buffer is the certificate. The builder has no owned-node order view of its
  /// own: <c>GetTopologicalOrder</c> is the contract extension, values in entry order, and the
  /// nodes themselves are the walker tier's handles (<c>GetHandles</c>).
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
    /// Stably sorts EVERY reachable node's out-edges in place, ascending by a key of the child
    /// node -- each node once, even when shared. Purely an edge reorder (payloads travel with
    /// their edges); no back-links move.
    /// </summary>
    public void SortChildrenBy<TKey>(Func<DagNode<TValue, TEdge>, TKey> keySelector)
    {
      if (keySelector == null)
        throw new ArgumentNullException(nameof(keySelector));

      using var walk = new TopologyWalkDagnumerator<TValue, DagNode<TValue, TEdge>, TEdge>(new DagNodeTopology<TValue, TEdge>(_Sources), _Sources);

      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
        if (walk.Mode == DagnumeratorMode.EnteringNode)
          walk.CurrentHandle.SortChildrenBy(keySelector);
    }

  }
}
