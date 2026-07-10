using System;

namespace Copse.Dags
{
  /// <summary>
  /// Thrown when a <see cref="Dag{TValue}"/> operation walks the graph and finds a cycle. Linking
  /// (<see cref="DagNode{TValue}.AddChild(DagNode{TValue})"/>) is deliberately unvalidated, so this
  /// is where acyclicity is actually enforced. The message spells out the offending cycle by node
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
