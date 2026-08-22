namespace Copse.Dags
{
  // The builder's adapter to the traversal contract (design-docs/DAG_CONTRACT_DESIGN.md, THE LAZY
  // BUILDER RULING 2026-08-06): Dag is the family's first IDagnumerable, and its acquisition is
  // LAZY -- no topological snapshot, no CSR arrays, no cycle check. The walk is Kahn on demand
  // (BuilderDagnumerator) over the live node graph; acquisition runs one light counting pass
  // (membership + member-in-degree -- the stray-parent affordance makes in-degree a
  // reachability fact: a member may have a parent outside the dag, whose edges are not the
  // dag's), and everything after is proportional to what the consumer pulls. A cyclic graph
  // does not throw here: it publishes its maximal acyclic prefix and throws DagCycleException
  // at exhaustion -- starvation is the failure, exhaustion is the proof, and Materialize is the
  // validator (the completed DagBuffer is the certificate). The no-snapshot ethos is unchanged:
  // mutate the builder, then acquire again -- each drain sees the graph as it is then. There is
  // no backward acquisition: the backward walk is forward-of-the-transpose, an operator's
  // business (the 2026-08-02 re-founding).
  public sealed partial class Dag<TValue, TEdge> : IDagnumerable<TValue, TEdge>
  {
    public IDagnumerator<TValue, TEdge> GetDagnumerator() =>
      new BuilderDagnumerator<TValue, TEdge>(_Sources);
  }
}
