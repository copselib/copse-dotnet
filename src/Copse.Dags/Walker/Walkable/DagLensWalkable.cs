namespace Copse.Dags
{
  // A view that is its own topology: the walkable's stream is the family's one demand-driven
  // walk over the view's probes, and its door is the unfocused walker over the same.
  internal abstract class DagLensWalkable<TValue, THandle, TEdge>
    : IWalkableDagnumerable<TValue, THandle, TEdge>, IDagTopology<TValue, THandle, TEdge>
  {
    protected DagLensWalkable()
    {
      _Walk = Dag.FromTopology(this);
    }

    private readonly IDagnumerable<TValue, TEdge> _Walk;

    public IDagnumerator<TValue, TEdge> GetDagnumerator() => _Walk.GetDagnumerator();

    public DagWalker<TValue, THandle, TEdge> GetDagWalker()
      => new DagWalker<TValue, THandle, TEdge>(this);

    public abstract TValue GetValue(THandle handle);
    public abstract DagStep<THandle, TEdge> TryGetParentAt(THandle handle, int inEdgeIndex);
    public abstract DagStep<THandle, TEdge> TryGetChildAt(THandle handle, int outEdgeIndex);
    public abstract DagStep<THandle, TEdge> TryGetSourceAt(int sourceIndex);
  }
}
