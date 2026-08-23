using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The topological order as a VALUE view over any forward source: entered values in entry
    /// order, one per node (shared or not). The DRAIN validates acyclicity: this method consumes
    /// the whole walk, so calling it on a cyclic source throws <see cref="DagCycleException"/>
    /// at the starvation point -- the contract-level cycle check, paid at exhaustion. The
    /// nodes themselves are the walker tier's handles (<c>GetHandles</c>); this view speaks
    /// values and never touches a node type.
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
