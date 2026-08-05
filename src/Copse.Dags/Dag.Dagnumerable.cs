using System.Collections.Generic;

namespace Copse.Dags
{
  // The builder's adapter to the traversal contract (docs/DAG_CONTRACT_DESIGN.md): Dag is the
  // family's first IDagnumerable. Acquisition snapshots the topological order (cycle detection
  // lives there -- a cyclic graph throws at acquisition, not mid-stream) and resolves it to
  // flat CSR adjacency for the walk; the no-snapshot ethos is unchanged in spirit: mutate the
  // builder, then acquire again. Only child edges are read (a member node may have a STRAY
  // parent outside the dag -- linked above a root, never reachable from one; its edges are not
  // the dag's), and every child of a member is itself a member, so the arrays close over the
  // snapshot. There is no backward acquisition: the backward walk is forward-of-the-transpose,
  // an operator's business (the 2026-08-02 re-founding).
  public sealed partial class Dag<TValue, TEdge> : IDagnumerable<TValue, TEdge>
  {
    public IDagnumerator<TValue, TEdge> GetDagnumerator()
    {
      var topologicalOrder = GetTopologicalOrder();

      var ordinals = new Dictionary<DagNode<TValue, TEdge>, int>(topologicalOrder.Count);
      for (var ordinal = 0; ordinal < topologicalOrder.Count; ordinal++)
        ordinals[topologicalOrder[ordinal]] = ordinal;

      var values = new TValue[topologicalOrder.Count];
      var offsets = new int[topologicalOrder.Count + 1];

      for (var ordinal = 0; ordinal < topologicalOrder.Count; ordinal++)
      {
        values[ordinal] = topologicalOrder[ordinal].Value;
        offsets[ordinal + 1] = offsets[ordinal] + topologicalOrder[ordinal].ChildEdges.Count;
      }

      var targets = new int[offsets[topologicalOrder.Count]];
      var payloads = new TEdge[offsets[topologicalOrder.Count]];

      for (var ordinal = 0; ordinal < topologicalOrder.Count; ordinal++)
      {
        var slot = offsets[ordinal];
        foreach (var childEdge in topologicalOrder[ordinal].ChildEdges)
        {
          targets[slot] = ordinals[childEdge.Child];
          payloads[slot] = childEdge.Value;
          slot++;
        }
      }

      return new TopologicalDagnumerator<TValue, TEdge>(values, offsets, targets, payloads);
    }
  }
}
