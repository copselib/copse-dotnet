using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The topological order as a VALUE view over any forward source: entered values in entry
    /// order, one per node (shared or not). The DRAIN validates acyclicity (THE LAZY BUILDER
    /// RULING): this method consumes the whole walk, so calling it on a cyclic
    /// source throws <see cref="DagCycleException"/> at the starvation point -- the
    /// contract-level cycle check, now paid at exhaustion rather than acquisition. The
    /// builder's instance method remains the owned-node view (it returns
    /// <see cref="DagNode{TValue, TEdge}"/>s for structural assertions); consumers on the
    /// contract surface use this one and never touch a node type.
    /// </summary>
    public static IReadOnlyList<TNode> GetTopologicalOrder<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source)
    {
      var topologicalOrder = new List<TNode>();

      using var walk = source.GetDagnumerator();
      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
        if (walk.Mode == DagnumeratorMode.EnteringNode)
          topologicalOrder.Add(walk.Node);

      return topologicalOrder;
    }
  }
}
