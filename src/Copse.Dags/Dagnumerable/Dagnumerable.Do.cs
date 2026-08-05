using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The sanctioned effect point (the tree family's <c>Do</c>, carried over): the action runs
    /// on every published visit and receives the full <see cref="DagVisit{TNode, TEdge}"/> --
    /// deliberately permissive, because every narrower cadence is a one-line filter inside the
    /// caller's action (<c>Mode == EnteringNode</c> = once per node; <c>ParentOrdinal &gt;= 0</c>
    /// = real edges only). Deferred: the action fires per enumeration.
    /// </summary>
    public static IDagnumerable<TNode, TEdge> Do<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Action<DagVisit<TNode, TEdge>> action)
    {
      if (action == null)
        throw new ArgumentNullException(nameof(action));

      return new DoForwardDagnumerable<TNode, TEdge>(source, action);
    }
  }

  internal sealed class DoForwardDagnumerable<TNode, TEdge> : IDagnumerable<TNode, TEdge>
  {
    public DoForwardDagnumerable(IDagnumerable<TNode, TEdge> source, Action<DagVisit<TNode, TEdge>> action)
    {
      _Source = source;
      _Action = action;
    }

    private readonly IDagnumerable<TNode, TEdge> _Source;
    private readonly Action<DagVisit<TNode, TEdge>> _Action;

    public IDagnumerator<TNode, TEdge> GetDagnumerator() =>
      new DoDagnumerator<TNode, TEdge>(_Source.GetDagnumerator(), _Action);
  }

  internal sealed class DoDagnumerator<TNode, TEdge> : IDagnumerator<TNode, TEdge>
  {
    public DoDagnumerator(IDagnumerator<TNode, TEdge> inner, Action<DagVisit<TNode, TEdge>> action)
    {
      _Inner = inner;
      _Action = action;
    }

    private readonly IDagnumerator<TNode, TEdge> _Inner;
    private readonly Action<DagVisit<TNode, TEdge>> _Action;

    public DagnumeratorMode Mode => _Inner.Mode;
    public TNode Node => _Inner.Node;
    public int Ordinal => _Inner.Ordinal;
    public TEdge Edge => _Inner.Edge;
    public int ParentOrdinal => _Inner.ParentOrdinal;
    public int EdgeIndex => _Inner.EdgeIndex;

    public bool MoveNext(DagTraversalStrategies strategies)
    {
      if (!_Inner.MoveNext(strategies))
        return false;

      _Action(new DagVisit<TNode, TEdge>(
        _Inner.Mode, _Inner.Node, _Inner.Ordinal, _Inner.Edge, _Inner.ParentOrdinal, _Inner.EdgeIndex));
      return true;
    }

    public void Dispose() => _Inner.Dispose();
  }
}
