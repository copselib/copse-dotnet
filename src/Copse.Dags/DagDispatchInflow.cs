namespace Copse.Dags
{
  /// <summary>
  /// One edge's delivery to a node during a dispatch pass, with its provenance: the value that
  /// arrived, the edge payload it rode, and the DISPATCHER -- the node that wrote it into its
  /// target slot (the parent in a downward pass, the child in an upward one). Provenance comes
  /// from the API, never smuggled inside <typeparamref name="TDispatch"/>: the machinery always
  /// knows who dispatched. A source's seeded inflow (downward) has no dispatcher -- the seed is
  /// external to the dag -- so its <see cref="Dispatcher"/> is default.
  ///
  /// <para>Inflows arrive in the pass's arrival order, so an inflow's index in
  /// <see cref="DagDispatchNode{TNode, TDispatch, TEdge}.Inflows"/> is its in-edge index --
  /// aligned with <see cref="DagEdgeContext{TNode, TEdge}.InEdgeIndex"/> for post-pass joins.
  /// The library never compares <see cref="Dispatcher"/> values (user values are never
  /// compared); reading it for domain decisions or caller-side identity joins is the caller's
  /// business.</para>
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
