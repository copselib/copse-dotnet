namespace Copse.Core
{
  /// <summary>
  /// The two kinds of event in a traversal's visit stream. Every node is scheduled exactly once
  /// (its first appearance) and then visited one or more times: a first visit when the
  /// traversal is about to enumerate its children, and a further visit between and after each
  /// child. Both depth-first and breadth-first traversals produce the same scheduling and
  /// visiting events for a given tree -- only their order differs.
  /// </summary>
  public enum TreenumeratorMode
  {
    /// <summary>The node's first appearance in the stream, before any of its visits.</summary>
    SchedulingNode,

    /// <summary>A return to an already-scheduled node: its first visit precedes its children,
    /// and one more visit follows each child.</summary>
    VisitingNode
  }
}
