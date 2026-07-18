using System.Collections.Generic;

namespace Copse.Dags
{
  // The builder's adapter to the traversal contract (docs/DAG_CONTRACT_DESIGN.md): Dag is the
  // family's first IDagnumerable, affording both dimensions because it owns the whole graph.
  // Acquisition snapshots the topological order (cycle detection lives there -- a cyclic graph
  // throws at acquisition, not mid-stream); the no-snapshot ethos is unchanged in spirit: mutate
  // the builder, then acquire again. The backward walk hands the reversed order to the same
  // walk class -- the reverse of a topological order is a topological order of the transpose.
  public sealed partial class Dag<TValue, TEdge> : IDagnumerable<TValue, TEdge>
  {
    public IDagnumerator<TValue, TEdge> GetForwardDagnumerator() =>
      new TopologicalDagnumerator<TValue, TEdge>(GetTopologicalOrder(), forward: true);

    public IDagnumerator<TValue, TEdge> GetBackwardDagnumerator()
    {
      var topologicalOrder = GetTopologicalOrder();
      var reversed = new List<DagNode<TValue, TEdge>>(topologicalOrder.Count);
      for (var index = topologicalOrder.Count - 1; index >= 0; index--)
        reversed.Add(topologicalOrder[index]);

      return new TopologicalDagnumerator<TValue, TEdge>(reversed, forward: false);
    }
  }
}
