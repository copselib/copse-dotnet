namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// <c>Hide</c>: forwards the visit stream unchanged behind the plain
    /// <see cref="IDagnumerable{TNode, TEdge}"/> contract, so callers can't downcast to (or
    /// feature-test for) the concrete source type -- the tree family's Hide, dag-side. Its
    /// seat in the three-tier stability story (THE LAZY BUILDER RULING, 2026-08-06,
    /// design-docs/DAG_CONTRACT_DESIGN.md): the mutable builder guarantees nothing, Hide guarantees
    /// the CONSUMER can't mutate (no cast back to <see cref="Dag{TValue, TEdge}"/>), and only
    /// the buffer guarantees nobody can. Deliberately NOT a stability promise: the owner can
    /// still mutate behind it, and drains lawfully differ -- Hide launders identity, the
    /// buffer pins values. Deferred; O(1); the walk is forwarded, strategies and all.
    /// </summary>
    public static IDagnumerable<TNode, TEdge> Hide<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source)
      => new HideDagnumerable<TNode, TEdge>(source);
  }

  internal sealed class HideDagnumerable<TNode, TEdge> : IDagnumerable<TNode, TEdge>
  {
    public HideDagnumerable(IDagnumerable<TNode, TEdge> source)
    {
      _Source = source;
    }

    private readonly IDagnumerable<TNode, TEdge> _Source;

    public IDagnumerator<TNode, TEdge> GetDagnumerator() =>
      new HideDagnumerator<TNode, TEdge>(_Source.GetDagnumerator());
  }

  // The walker hides too: a consumer holding the cursor can't feature-test the concrete walk
  // class either.
  internal sealed class HideDagnumerator<TNode, TEdge> : IDagnumerator<TNode, TEdge>
  {
    public HideDagnumerator(IDagnumerator<TNode, TEdge> inner)
    {
      _Inner = inner;
    }

    private readonly IDagnumerator<TNode, TEdge> _Inner;

    public DagnumeratorMode Mode => _Inner.Mode;
    public TNode Node => _Inner.Node;
    public int Ordinal => _Inner.Ordinal;
    public TEdge Edge => _Inner.Edge;
    public int ParentOrdinal => _Inner.ParentOrdinal;
    public int EdgeIndex => _Inner.EdgeIndex;

    public bool MoveNext(DagTraversalStrategies strategies) => _Inner.MoveNext(strategies);

    public void Dispose() => _Inner.Dispose();
  }
}
