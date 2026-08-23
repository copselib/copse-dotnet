using System;

namespace Copse.Dags
{
  /// <summary>
  /// Thrown when a walk drains a cyclic graph -- at the starvation point, after the maximal
  /// acyclic prefix. Linking (<see cref="DagNode{TValue, TEdge}.AddChild(DagNode{TValue, TEdge}, TEdge)"/>)
  /// is deliberately unvalidated, so this is where acyclicity is actually enforced. The message spells out the offending cycle by node
  /// value.
  /// </summary>
  public sealed class DagCycleException : InvalidOperationException
  {
    public DagCycleException(string message)
      : base(message)
    {
    }
  }
}
