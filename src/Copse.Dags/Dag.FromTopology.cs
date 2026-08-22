namespace Copse.Dags
{
  /// <summary>The dag creation surface beside the builder (the tree family's <c>Tree</c> statics, dualized).</summary>
  public static class Dag
  {
    /// <summary>
    /// The Walk adapter: any topology, streamed under the visit protocol -- Kahn's algorithm
    /// driven by the topology's probes (sources from the virtual source's child group, out-edge
    /// groups as dispatch blocks), labels resolving during the walk through <c>GetValue</c>.
    /// The third-party story this completes: implement <see cref="IDagTopology{TValue, THandle, TEdge}"/>
    /// over your native structure and the streaming half of <see cref="IWalkableDagnumerable{TValue, THandle, TEdge}"/>
    /// is one delegation (the walker half is one construction -- the public <see cref="DagWalker{TValue, THandle, TEdge}"/>
    /// mint). A view whose <c>GetValue</c> is an observation (the Extend lens) streams its own
    /// labeling by walking itself. A cyclic topology streams its maximal acyclic prefix and
    /// throws <see cref="DagCycleException"/> at starvation, like the builder. Conformance is the
    /// law suites' degenerate-tower pin: walking the buffer's topology reproduces the buffer's
    /// own visit stream.
    /// </summary>
    public static IDagnumerable<TValue, TEdge> FromTopology<TValue, THandle, TEdge>(
      IDagTopology<TValue, THandle, TEdge> topology)
      => new TopologyWalkDagnumerable<TValue, THandle, TEdge>(topology);
  }

  internal sealed class TopologyWalkDagnumerable<TValue, THandle, TEdge> : IDagnumerable<TValue, TEdge>
  {
    public TopologyWalkDagnumerable(IDagTopology<TValue, THandle, TEdge> topology)
    {
      _Topology = topology ?? throw new System.ArgumentNullException(nameof(topology));
    }

    private readonly IDagTopology<TValue, THandle, TEdge> _Topology;

    public IDagnumerator<TValue, TEdge> GetDagnumerator()
      => new TopologyWalkDagnumerator<TValue, THandle, TEdge>(_Topology);
  }
}
