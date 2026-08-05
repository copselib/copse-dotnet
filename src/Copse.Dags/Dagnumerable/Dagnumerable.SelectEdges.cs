using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The edge-side dual of <see cref="Select"/> (docs/DAG_CONTRACT_DESIGN.md, the edge dual):
    /// maps each edge's payload, forwarding the visit stream otherwise unchanged (node values,
    /// structure, ordinals). The selector receives the full relationship context -- dispatching
    /// parent, discovered child, payload, in-edge index -- and is evaluated once per published
    /// discovery (counts unspecified; purity expected). Conventional source discoveries carry no
    /// edge and publish <c>default</c>. Deferred, streaming: an edge payload is a discovery-time
    /// fact, so nothing materializes.
    /// </summary>
    public static IDagnumerable<TNode, TEdgeResult> SelectEdges<TNode, TEdge, TEdgeResult>(
      this IDagnumerable<TNode, TEdge> source,
      Func<DagEdgeContext<TNode, TEdge>, TEdgeResult> selector)
    {
      if (selector == null)
        throw new ArgumentNullException(nameof(selector));

      return new SelectEdgesForwardDagnumerable<TNode, TEdge, TEdgeResult>(source, selector);
    }
  }

  internal sealed class SelectEdgesForwardDagnumerable<TNode, TEdge, TEdgeResult> : IDagnumerable<TNode, TEdgeResult>
  {
    public SelectEdgesForwardDagnumerable(
      IDagnumerable<TNode, TEdge> source,
      Func<DagEdgeContext<TNode, TEdge>, TEdgeResult> selector)
    {
      _Source = source;
      _Selector = selector;
    }

    private readonly IDagnumerable<TNode, TEdge> _Source;
    private readonly Func<DagEdgeContext<TNode, TEdge>, TEdgeResult> _Selector;

    public IDagnumerator<TNode, TEdgeResult> GetDagnumerator() =>
      new SelectEdgesDagnumerator<TNode, TEdge, TEdgeResult>(_Source.GetDagnumerator(), _Selector);
  }

  internal sealed class SelectEdgesDagnumerator<TNode, TEdge, TEdgeResult> : IDagnumerator<TNode, TEdgeResult>
  {
    public SelectEdgesDagnumerator(
      IDagnumerator<TNode, TEdge> inner,
      Func<DagEdgeContext<TNode, TEdge>, TEdgeResult> selector)
    {
      _Inner = inner;
      _Selector = selector;
      _RelationshipContext = new DagRelationshipTracker<TNode, TEdge>();

      // The pre-enumeration convention, mirrored.
      Mode = DagnumeratorMode.DiscoveringNode;
      Ordinal = -1;
      ParentOrdinal = -1;
      EdgeIndex = 0;
    }

    private readonly IDagnumerator<TNode, TEdge> _Inner;
    private readonly Func<DagEdgeContext<TNode, TEdge>, TEdgeResult> _Selector;
    private readonly DagRelationshipTracker<TNode, TEdge> _RelationshipContext;

    public DagnumeratorMode Mode { get; private set; }
    public TNode Node { get; private set; }
    public int Ordinal { get; private set; }
    public TEdgeResult Edge { get; private set; }
    public int ParentOrdinal { get; private set; }
    public int EdgeIndex { get; private set; }

    public bool MoveNext(DagTraversalStrategies strategies)
    {
      if (!_Inner.MoveNext(strategies))
        return false;

      Mode = _Inner.Mode;
      Node = _Inner.Node;
      Ordinal = _Inner.Ordinal;
      ParentOrdinal = _Inner.ParentOrdinal;
      EdgeIndex = _Inner.EdgeIndex;

      Edge = _RelationshipContext.TryTrack(_Inner, out var relationship)
        ? _Selector(relationship)
        : default;

      return true;
    }

    public void Dispose() => _Inner.Dispose();
  }
}
