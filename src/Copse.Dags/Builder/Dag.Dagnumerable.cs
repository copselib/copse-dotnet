namespace Copse.Dags
{
  // The builder's adapter to the traversal contract (design-docs/DAG_CONTRACT_DESIGN.md, THE LAZY
  // BUILDER RULING): Dag is the family's first IDagnumerable, and its acquisition is LAZY -- the
  // one demand-driven walk (TopologyWalkDagnumerator) over the builder's own topology
  // (DagNodeTopology: the live node graph, membership and the stray-parent rule applied), so
  // the stream and the walker read the same physics. No topological snapshot, no CSR arrays, no
  // cycle check at acquisition: a cyclic graph publishes its maximal acyclic prefix and throws
  // DagCycleException at exhaustion -- starvation is the failure, exhaustion is the proof, and
  // Materialize is the validator (the completed DagBuffer is the certificate). The no-snapshot
  // ethos is unchanged: mutate the builder, then acquire again -- each drain sees the graph as
  // it is then. There is no backward acquisition: the backward walk is forward-of-the-transpose,
  // an operator's business.
  public sealed partial class Dag<TValue, TEdge> : IDagnumerable<TValue, TEdge>
  {
    public IDagnumerator<TValue, TEdge> GetDagnumerator() =>
      new TopologyWalkDagnumerator<TValue, DagNode<TValue, TEdge>, TEdge>(new DagNodeTopology<TValue, TEdge>(_Sources), _Sources);
  }
}
