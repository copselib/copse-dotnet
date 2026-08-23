using System;

namespace Copse.Dags
{
  // Where a Kahn walk stands between visits: both walks (the buffer's and the topology's)
  // publish the same phases and refuse the same verdicts.
  internal enum DagWalkPhase
  {
    NotStarted,
    SourceDiscoveries,
    Entering,
    Dispatching,
    Done,
  }

  internal static class DagWalkVerdicts
  {
    /// <summary>
    /// Refuses a verdict the current visit cannot take: unknown flags, any verdict but
    /// TraverseAll when no visit is published, SkipOutEdges on a discovery, SkipEdge on an entry.
    /// </summary>
    public static void Require(DagTraversalStrategies strategies, DagWalkPhase phase, DagnumeratorMode mode)
    {
      if ((strategies & ~(DagTraversalStrategies.SkipEdge | DagTraversalStrategies.SkipOutEdges)) != 0)
        throw new ArgumentException($"Unknown strategy flags: {strategies}.", nameof(strategies));

      if (phase == DagWalkPhase.NotStarted || phase == DagWalkPhase.Done)
      {
        if (strategies != DagTraversalStrategies.TraverseAll)
          throw new ArgumentException(
            $"{strategies} answers no visit -- the dagnumerator has not published one.", nameof(strategies));
        return;
      }

      if (mode == DagnumeratorMode.DiscoveringNode && strategies.HasFlag(DagTraversalStrategies.SkipOutEdges))
        throw new ArgumentException(
          "SkipOutEdges answers an entry; the current visit is a discovery.", nameof(strategies));

      if (mode == DagnumeratorMode.EnteringNode && strategies.HasFlag(DagTraversalStrategies.SkipEdge))
        throw new ArgumentException(
          "SkipEdge answers a discovery; the current visit is an entry.", nameof(strategies));
    }
  }
}
