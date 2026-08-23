using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// <c>PruneNodesBefore</c> over node VALUES (prune polarity: true = prune): a matching node
    /// leaves the logical dag and its edges die with it; what lies below survives only where
    /// another live path reaches it. Deferred, streaming -- the wrapper answers
    /// <see cref="DagTraversalStrategies.SkipEdge"/> to every discovery of a pruned node and
    /// suppresses those visits, and the source's own liveness fold does the rest. The predicate
    /// is evaluated per discovery (counts unspecified; purity expected).
    /// </summary>
    public static IDagnumerable<TNode, TEdge> PruneNodesBefore<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        return source;

      return new PruneNodesBeforeDagnumerable<TNode, TEdge>(source, predicate);
    }
  }

  internal sealed class PruneNodesBeforeDagnumerable<TNode, TEdge> : IDagnumerable<TNode, TEdge>
  {
    public PruneNodesBeforeDagnumerable(IDagnumerable<TNode, TEdge> source, Func<TNode, bool> predicate)
    {
      _Source = source;
      _Predicate = predicate;
    }

    private readonly IDagnumerable<TNode, TEdge> _Source;
    private readonly Func<TNode, bool> _Predicate;

    public IDagnumerator<TNode, TEdge> GetDagnumerator() =>
      new PruneNodesBeforeDagnumerator<TNode, TEdge>(_Source.GetDagnumerator(), _Predicate);
  }

  internal sealed class PruneNodesBeforeDagnumerator<TNode, TEdge> : ForwardingDagnumerator<TNode, TEdge>
  {
    public PruneNodesBeforeDagnumerator(IDagnumerator<TNode, TEdge> inner, Func<TNode, bool> predicate)
      : base(inner)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, bool> _Predicate;

    public override bool MoveNext(DagTraversalStrategies strategies)
    {
      // The consumer's verdict answers the visit the consumer saw; the wrapper's own verdicts
      // answer the visits it swallowed. A pruned node's every discovery is severed, so it never
      // enters and its dispatches never happen -- the source's liveness fold is the machinery.
      var verdict = strategies;

      while (Inner.MoveNext(verdict))
      {
        if (Inner.Mode == DagnumeratorMode.DiscoveringNode && _Predicate(Inner.Node))
        {
          verdict = DagTraversalStrategies.SkipEdge;
          continue;
        }

        return true;
      }

      return false;
    }
  }
}
