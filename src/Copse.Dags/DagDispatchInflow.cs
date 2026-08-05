namespace Copse.Dags
{
  /// <summary>
  /// One edge's delivery as a dispatch SURVEY sees it -- the callback-time arrival view
  /// element: the value that arrived, the edge payload it rode, and the DISPATCHER -- the node
  /// that wrote it (the parent downward, the child upward). This view is the Dispatcher's ONE
  /// home (the split-homes ruling, 2026-08-05): mid-pass there is no buffer to consult, so the
  /// context rides the seat, immediate and consumed in place; the TRAVELING result
  /// (<see cref="DagDispatchResult{TNode, TDispatch}"/>) carries no provenance, because there
  /// "who wrote arrival i of node n" is index arithmetic over the buffer's transpose
  /// adjacency. The virtual source family's delivery is dispatcher-less (default) -- the seed
  /// has no author inside the dag, and a dispatcher-less arrival is the in-band
  /// arrived-from-outside test. The library never compares Dispatcher values (user values are
  /// never compared); reading it for domain decisions is the caller's business.
  /// </summary>
  public readonly struct DagDispatchInflow<TNode, TDispatch, TEdge>
  {
    public DagDispatchInflow(TNode dispatcher, TDispatch value, TEdge edge)
    {
      Dispatcher = dispatcher;
      Value = value;
      Edge = edge;
    }

    public TNode Dispatcher { get; }
    public TDispatch Value { get; }
    public TEdge Edge { get; }
  }
}
