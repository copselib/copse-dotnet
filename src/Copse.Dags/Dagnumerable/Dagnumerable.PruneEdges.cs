using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The edge-side prune (prune polarity: true = prune): a matching edge is severed -- the
    /// relationship leaves the logical dag; both endpoints are untouched except through
    /// liveness (a node whose every in-edge is pruned or dead never appears). ONE operator, no
    /// Before/After pair: those distinguish what happens to a matched NODE's dependents, and an
    /// edge has none -- removal is removal. The predicate receives the full relationship
    /// context (parent, child, payload, in-edge index) and runs once per presented discovery.
    /// Deferred, streaming -- the wrapper answers <see cref="DagTraversalStrategies.SkipEdge"/>
    /// to matching discoveries and swallows them; the source's liveness fold does the rest.
    ///
    /// <para>CONSTRAINT CAVEAT: pruning an edge does not rebalance its siblings. Where edge
    /// payloads form a constrained group (fractions summing to one), flow passes that normalize
    /// over presented weights stay correct, but absolute-fact consumers (scans multiplying by
    /// the payload) see the broken group -- rebalancing is the caller's algebra, on the group
    /// (see the design doc's edge-dual notes).</para>
    /// </summary>
    public static IForwardDagnumerable<TNode, TEdge> PruneEdges<TNode, TEdge>(
      this IForwardDagnumerable<TNode, TEdge> source,
      Func<DagEdgeContext<TNode, TEdge>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return new PruneEdgesForwardDagnumerable<TNode, TEdge>(source, predicate);
    }
  }

  internal sealed class PruneEdgesForwardDagnumerable<TNode, TEdge> : IForwardDagnumerable<TNode, TEdge>
  {
    public PruneEdgesForwardDagnumerable(
      IForwardDagnumerable<TNode, TEdge> source,
      Func<DagEdgeContext<TNode, TEdge>, bool> predicate)
    {
      _Source = source;
      _Predicate = predicate;
    }

    private readonly IForwardDagnumerable<TNode, TEdge> _Source;
    private readonly Func<DagEdgeContext<TNode, TEdge>, bool> _Predicate;

    public IDagnumerator<TNode, TEdge> GetForwardDagnumerator() =>
      new PruneEdgesDagnumerator<TNode, TEdge>(_Source.GetForwardDagnumerator(), _Predicate);
  }

  internal sealed class PruneEdgesDagnumerator<TNode, TEdge> : IDagnumerator<TNode, TEdge>
  {
    public PruneEdgesDagnumerator(
      IDagnumerator<TNode, TEdge> inner,
      Func<DagEdgeContext<TNode, TEdge>, bool> predicate)
    {
      _Inner = inner;
      _Predicate = predicate;
      _RelationshipContext = new DagRelationshipTracker<TNode, TEdge>();
    }

    private readonly IDagnumerator<TNode, TEdge> _Inner;
    private readonly Func<DagEdgeContext<TNode, TEdge>, bool> _Predicate;
    private readonly DagRelationshipTracker<TNode, TEdge> _RelationshipContext;

    public DagnumeratorMode Mode => _Inner.Mode;
    public TNode Node => _Inner.Node;
    public int Ordinal => _Inner.Ordinal;
    public TEdge Edge => _Inner.Edge;
    public int ParentOrdinal => _Inner.ParentOrdinal;
    public int EdgeIndex => _Inner.EdgeIndex;

    public bool MoveNext(DagTraversalStrategies strategies)
    {
      // The consumer's verdict answers the visit the consumer saw; the wrapper's own SkipEdge
      // verdicts answer the discoveries it swallowed. The source's liveness fold does the rest.
      var verdict = strategies;

      while (_Inner.MoveNext(verdict))
      {
        if (_RelationshipContext.TryTrack(_Inner, out var relationship) && _Predicate(relationship))
        {
          verdict = DagTraversalStrategies.SkipEdge;
          continue;
        }

        return true;
      }

      return false;
    }

    public void Dispose() => _Inner.Dispose();
  }
}
