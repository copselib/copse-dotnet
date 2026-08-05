using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// <c>PruneAfter</c> over node VALUES (prune polarity: true = prune): a matching node is
    /// KEPT -- it enters as usual -- but dispatches nothing; what lies below survives only
    /// where another live path reaches it. Deferred, streaming -- the wrapper adds
    /// <see cref="DagTraversalStrategies.SkipOutEdges"/> to its answer at a matching entry,
    /// and the source's liveness fold does the rest. The predicate is evaluated once per
    /// entered node.
    /// </summary>
    public static IDagnumerable<TNode, TEdge> PruneAfter<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      return new PruneAfterForwardDagnumerable<TNode, TEdge>(source, predicate);
    }
  }

  internal sealed class PruneAfterForwardDagnumerable<TNode, TEdge> : IDagnumerable<TNode, TEdge>
  {
    public PruneAfterForwardDagnumerable(IDagnumerable<TNode, TEdge> source, Func<TNode, bool> predicate)
    {
      _Source = source;
      _Predicate = predicate;
    }

    private readonly IDagnumerable<TNode, TEdge> _Source;
    private readonly Func<TNode, bool> _Predicate;

    public IDagnumerator<TNode, TEdge> GetDagnumerator() =>
      new PruneAfterDagnumerator<TNode, TEdge>(_Source.GetDagnumerator(), _Predicate);
  }

  internal sealed class PruneAfterDagnumerator<TNode, TEdge> : IDagnumerator<TNode, TEdge>
  {
    public PruneAfterDagnumerator(IDagnumerator<TNode, TEdge> inner, Func<TNode, bool> predicate)
    {
      _Inner = inner;
      _Predicate = predicate;
    }

    private readonly IDagnumerator<TNode, TEdge> _Inner;
    private readonly Func<TNode, bool> _Predicate;
    private DagTraversalStrategies _OwnVerdict = DagTraversalStrategies.TraverseAll;

    public DagnumeratorMode Mode => _Inner.Mode;
    public TNode Node => _Inner.Node;
    public int Ordinal => _Inner.Ordinal;
    public TEdge Edge => _Inner.Edge;
    public int ParentOrdinal => _Inner.ParentOrdinal;
    public int EdgeIndex => _Inner.EdgeIndex;

    public bool MoveNext(DagTraversalStrategies strategies)
    {
      // The wrapper's verdict rides along with the consumer's for the entry just shown (the
      // union is safe: both answer the same visit, and the mode check stays the source's).
      var verdict = strategies | _OwnVerdict;
      _OwnVerdict = DagTraversalStrategies.TraverseAll;

      if (!_Inner.MoveNext(verdict))
        return false;

      if (_Inner.Mode == DagnumeratorMode.EnteringNode && _Predicate(_Inner.Node))
        _OwnVerdict = DagTraversalStrategies.SkipOutEdges;

      return true;
    }

    public void Dispose() => _Inner.Dispose();
  }
}
