namespace Copse.Dags
{
  // The streaming wrappers' shared shape: every visit property is the inner walk's; only the
  // verdict routing (MoveNext) is the wrapper's own.
  internal abstract class ForwardingDagnumerator<TNode, TEdge> : IDagnumerator<TNode, TEdge>
  {
    protected ForwardingDagnumerator(IDagnumerator<TNode, TEdge> inner)
    {
      Inner = inner;
    }

    protected IDagnumerator<TNode, TEdge> Inner { get; }

    public DagnumeratorMode Mode => Inner.Mode;
    public TNode Node => Inner.Node;
    public int Ordinal => Inner.Ordinal;
    public TEdge Edge => Inner.Edge;
    public int ParentOrdinal => Inner.ParentOrdinal;
    public int EdgeIndex => Inner.EdgeIndex;

    public virtual bool MoveNext(DagTraversalStrategies strategies) => Inner.MoveNext(strategies);

    public void Dispose() => Inner.Dispose();
  }
}
