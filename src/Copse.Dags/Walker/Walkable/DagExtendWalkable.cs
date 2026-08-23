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
    : DagLensWalkable<TResult, THandle, TEdge>
  {
    public DagExtendWalkable(
      IDagTopology<TValue, THandle, TEdge> source,
      Func<IDagTopology<TValue, THandle, TEdge>, THandle, TResult> observer)
    {
      _Source = source;
      _Observer = observer;
    }

    private readonly IDagTopology<TValue, THandle, TEdge> _Source;
    private readonly Func<IDagTopology<TValue, THandle, TEdge>, THandle, TResult> _Observer;
    public override TResult GetValue(THandle handle) => _Observer(_Source, handle);

    public override DagStep<THandle, TEdge> TryGetParentAt(THandle handle, int inEdgeIndex) => _Source.TryGetParentAt(handle, inEdgeIndex);

    public override DagStep<THandle, TEdge> TryGetChildAt(THandle handle, int outEdgeIndex) => _Source.TryGetChildAt(handle, outEdgeIndex);

    public override DagStep<THandle, TEdge> TryGetSourceAt(int sourceIndex) => _Source.TryGetSourceAt(sourceIndex);
  }
}
