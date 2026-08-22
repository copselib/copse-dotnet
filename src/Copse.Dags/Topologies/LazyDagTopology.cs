using System;

namespace Copse.Dags
{
  // DagTopology.Lazy's engine: the knock happens once, at the first probe, and is cached -- the
  // contract does not promise cheap or idempotent doors, so the cache keeps a view honest against
  // the weakest citizen. The door is total, so the empty dag needs no special case: its bound
  // topology answers every probe with the miss.
  internal sealed class LazyDagTopology<TValue, THandle, TEdge> : IDagTopology<TValue, THandle, TEdge>
  {
    public LazyDagTopology(IWalkableDagnumerable<TValue, THandle, TEdge> source)
    {
      _Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    private readonly IWalkableDagnumerable<TValue, THandle, TEdge> _Source;
    private IDagTopology<TValue, THandle, TEdge> _Topology;

    private IDagTopology<TValue, THandle, TEdge> Resolve()
    {
      if (_Topology != null)
        return _Topology;

      // The door yields a stance; its public Topology field is the bound physics.
      _Topology = _Source.GetDagWalker().Topology;

      return _Topology;
    }

    public TValue GetValue(THandle handle) => Resolve().GetValue(handle);

    public DagStep<THandle, TEdge> TryGetParentAt(THandle handle, int inEdgeIndex) => Resolve().TryGetParentAt(handle, inEdgeIndex);

    public DagStep<THandle, TEdge> TryGetChildAt(THandle handle, int outEdgeIndex) => Resolve().TryGetChildAt(handle, outEdgeIndex);

    public DagStep<THandle, TEdge> TryGetSourceAt(int sourceIndex) => Resolve().TryGetSourceAt(sourceIndex);
  }
}
