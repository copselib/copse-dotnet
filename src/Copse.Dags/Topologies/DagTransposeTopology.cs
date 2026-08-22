using System.Collections.Generic;

namespace Copse.Dags
{
  // The free lens: the transpose as a topology transformer -- the two edge groups trade places
  // (child probes answer from the source's in-edge group and parent probes from its out-edge
  // group; payloads ride unchanged), and the transpose's sources are the source's SINKS, found
  // by one reachability sweep memoized at first need, in discovery order. Transposing a
  // transpose unwraps (the involution, free). Contrast the order algebra, where Transpose is a
  // materialized reversal: in adjacency it is two method references trading places.
  internal sealed class DagTransposeTopology<TValue, THandle, TEdge> : IDagTopology<TValue, THandle, TEdge>
  {
    public DagTransposeTopology(IDagTopology<TValue, THandle, TEdge> source)
    {
      Source = source;
    }

    public IDagTopology<TValue, THandle, TEdge> Source { get; }

    private List<THandle> _Sinks;

    public static IDagTopology<TValue, THandle, TEdge> Over(IDagTopology<TValue, THandle, TEdge> source)
      => source is DagTransposeTopology<TValue, THandle, TEdge> transposed
        ? transposed.Source
        : new DagTransposeTopology<TValue, THandle, TEdge>(source);

    public TValue GetValue(THandle handle) => Source.GetValue(handle);

    public DagStep<THandle, TEdge> TryGetParentAt(THandle handle, int inEdgeIndex) => Source.TryGetChildAt(handle, inEdgeIndex);

    public DagStep<THandle, TEdge> TryGetChildAt(THandle handle, int outEdgeIndex) => Source.TryGetParentAt(handle, outEdgeIndex);

    public DagStep<THandle, TEdge> TryGetSourceAt(int sourceIndex)
    {
      if (_Sinks == null)
        _Sinks = FindSinks();

      if (sourceIndex < 0 || sourceIndex >= _Sinks.Count)
        return default;

      return new DagStep<THandle, TEdge>(_Sinks[sourceIndex], default, sourceIndex);
    }

    // Sinks in DISCOVERY order -- the order a depth-first sweep from the source group first
    // meets them -- a presentation choice, as every source group's order is (the buffer's
    // materialized Transpose() presents them reversed; content pins read sources as a set).
    private List<THandle> FindSinks()
    {
      var sinks = new List<THandle>();
      var discovered = new List<THandle>();
      var visited = new HashSet<THandle>();
      var pending = new Stack<THandle>();

      for (var sourceIndex = 0; ; sourceIndex++)
      {
        var sourceStep = Source.TryGetSourceAt(sourceIndex);

        if (!sourceStep.HasValue)
          break;

        if (visited.Add(sourceStep.Handle))
        {
          discovered.Add(sourceStep.Handle);
          pending.Push(sourceStep.Handle);
        }
      }

      while (pending.Count > 0)
      {
        var handle = pending.Pop();

        for (var childStep = Source.TryGetChildAt(handle, 0); childStep.HasValue; childStep = Source.TryGetChildAt(handle, childStep.EdgeIndex + 1))
          if (visited.Add(childStep.Handle))
          {
            discovered.Add(childStep.Handle);
            pending.Push(childStep.Handle);
          }
      }

      foreach (var handle in discovered)
        if (!Source.TryGetChildAt(handle, 0).HasValue)
          sinks.Add(handle);

      return sinks;
    }
  }
}
