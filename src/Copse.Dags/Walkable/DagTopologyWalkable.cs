namespace Copse.Dags
{
  // The identity view: a topology worn as a walkable, nothing rewritten -- what the unfocused
  // walker's hoist denotes (the whole dag: nothing above the virtual source to sever, and it
  // contributes no row of its own, having no value). Streams via the Walk adapter over the same
  // topology the door binds, so the walkable and its walkers agree on every answer.
  internal sealed class DagTopologyWalkable<TValue, THandle, TEdge> : IWalkableDagnumerable<TValue, THandle, TEdge>
  {
    public DagTopologyWalkable(IDagTopology<TValue, THandle, TEdge> topology)
    {
      _Topology = topology;
      _Walk = Dag.FromTopology(topology);
    }

    private readonly IDagTopology<TValue, THandle, TEdge> _Topology;
    private readonly IDagnumerable<TValue, TEdge> _Walk;

    public IDagnumerator<TValue, TEdge> GetDagnumerator() => _Walk.GetDagnumerator();

    public DagWalker<TValue, THandle, TEdge> GetDagWalker()
      => new DagWalker<TValue, THandle, TEdge>(_Topology);
  }
}
