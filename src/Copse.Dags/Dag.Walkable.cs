namespace Copse.Dags
{
  // The builder's door: the live node graph is a walkable with the NODE as its handle (the
  // self-sufficiency criterion -- a DagNode answers adjacency from itself). The topology is the
  // builder's membership rule applied to DagNode's edge lists (DagNodeTopology); like the
  // stream, a walker sees the graph as it is when acquired -- mutate, then acquire again.
  public sealed partial class Dag<TValue, TEdge> : IWalkableDagnumerable<TValue, DagNode<TValue, TEdge>, TEdge>
  {
    public DagWalker<TValue, DagNode<TValue, TEdge>, TEdge> GetDagWalker()
      => new DagWalker<TValue, DagNode<TValue, TEdge>, TEdge>(new DagNodeTopology<TValue, TEdge>(_Sources));
  }
}
