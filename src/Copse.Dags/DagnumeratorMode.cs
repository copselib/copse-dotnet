namespace Copse.Dags
{
  /// <summary>
  /// The DAG visit protocol's two phases (docs/DAG_CONTRACT_DESIGN.md) — the tree family's
  /// scheduling/visiting split, generalized to shared parentage: a node is DISCOVERED once per
  /// in-edge (as each already-entered parent dispatches its out-edges) and ENTERED exactly once,
  /// after its last discovery. Topological order is precisely the guarantee that entry is
  /// well-defined: every inflow is complete when it fires. A tree degenerates exactly — one
  /// in-edge, so discover = schedule and enter = visit.
  /// </summary>
  public enum DagnumeratorMode
  {
    /// <summary>An in-edge of the current node is being presented (per-edge context is valid).</summary>
    DiscoveringNode = 0,

    /// <summary>The current node is being entered — all of its in-edges have been presented.</summary>
    EnteringNode = 1,
  }
}
