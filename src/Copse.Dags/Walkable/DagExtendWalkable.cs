using System;

namespace Copse.Dags
{
  // The comonad's defining operation, made concrete: a relabeling of the SAME shape where every
  // node's new value is an arbitrary observation of its focus -- the observer receives the source
  // topology and the handle, so it can see anything reachable from that vantage (upstream
  // arrivals, the downstream cone, in- and out-degree) that a streaming Select never could.
  // Adjacency and handles delegate untouched (extend never reshapes); the streaming half is the
  // Walk adapter driving this view's own adjacency under the observer's labeling (self-feed:
  // GetValue IS the observation). Laws pinned by the dag walker comonad law suites.
  internal sealed class DagExtendWalkable<TValue, THandle, TEdge, TResult>
    : IWalkableDagnumerable<TResult, THandle, TEdge>, IDagTopology<TResult, THandle, TEdge>
  {
    public DagExtendWalkable(
      IDagTopology<TValue, THandle, TEdge> source,
      Func<IDagTopology<TValue, THandle, TEdge>, THandle, TResult> observer)
    {
      _Source = source;
      _Observer = observer;
      _Walk = Dag.FromTopology(this);
    }

    private readonly IDagTopology<TValue, THandle, TEdge> _Source;
    private readonly Func<IDagTopology<TValue, THandle, TEdge>, THandle, TResult> _Observer;
    private readonly IDagnumerable<TResult, TEdge> _Walk;

    public IDagnumerator<TResult, TEdge> GetDagnumerator() => _Walk.GetDagnumerator();

    public TResult GetValue(THandle handle) => _Observer(_Source, handle);

    public DagStep<THandle, TEdge> TryGetParentAt(THandle handle, int inEdgeIndex) => _Source.TryGetParentAt(handle, inEdgeIndex);

    public DagStep<THandle, TEdge> TryGetChildAt(THandle handle, int outEdgeIndex) => _Source.TryGetChildAt(handle, outEdgeIndex);

    public DagStep<THandle, TEdge> TryGetSourceAt(int sourceIndex) => _Source.TryGetSourceAt(sourceIndex);

    public DagWalker<TResult, THandle, TEdge> GetDagWalker()
      => new DagWalker<TResult, THandle, TEdge>(this);
  }
}
