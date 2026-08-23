using System;
using System.Collections.Generic;

namespace Copse.Dags.Tests
{
  // One visit of the traversal contract, as the exact-stream pins spell it.
  internal readonly record struct Visit(
    DagnumeratorMode Mode, string Node, int Ordinal, int ParentOrdinal, int EdgeIndex, decimal Edge);

  internal static class Visits
  {
    public static Visit Discover(string node, int ordinal, int parentOrdinal, int edgeIndex, decimal edge = 0m)
      => new(DagnumeratorMode.DiscoveringNode, node, ordinal, parentOrdinal, edgeIndex, edge);

    public static Visit Enter(string node, int ordinal)
      => new(DagnumeratorMode.EnteringNode, node, ordinal, -1, 0, 0m);

    /// <summary>Drains a walk, answering each visit with the selector's verdict (TraverseAll by default).</summary>
    public static List<Visit> Drain(
      IDagnumerator<string, decimal> dagnumerator,
      Func<Visit, DagTraversalStrategies> strategySelector = null)
    {
      var visits = new List<Visit>();
      var strategies = DagTraversalStrategies.TraverseAll;

      while (dagnumerator.MoveNext(strategies))
      {
        var visit = new Visit(
          dagnumerator.Mode, dagnumerator.Node, dagnumerator.Ordinal,
          dagnumerator.ParentOrdinal, dagnumerator.EdgeIndex, dagnumerator.Edge);
        visits.Add(visit);
        strategies = strategySelector?.Invoke(visit) ?? DagTraversalStrategies.TraverseAll;
      }

      return visits;
    }
  }
}
