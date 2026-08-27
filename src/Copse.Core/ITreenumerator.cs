using System;

namespace Copse.Core
{
  /// <summary>
  /// A stateful traversal cursor over a tree, advanced with <see cref="MoveNext"/> -- the tree
  /// analog of <c>IEnumerator&lt;T&gt;</c>. Each advance lands on one event of the visit
  /// stream: the current node (<see cref="Node"/>), whether it is being scheduled or visited
  /// (<see cref="Mode"/>), how many visits it has received (<see cref="VisitCount"/>), and its
  /// position (<see cref="Position"/>). Dispose when done, as with any enumerator.
  /// </summary>
  public interface ITreenumerator<TNode> : IDisposable
  {
    /// <summary>Advances to the next visit event, pruning the traversal from the current node
    /// as <paramref name="nodeTraversalStrategies"/> directs
    /// (<see cref="NodeTraversalStrategies.TraverseAll"/> prunes nothing). Returns
    /// <c>false</c> when the traversal is exhausted.</summary>
    bool MoveNext(NodeTraversalStrategies nodeTraversalStrategies);

    /// <summary>The node the traversal is currently visiting (or scheduling -- see <see cref="Mode"/>).</summary>
    TNode Node { get; }

    /// <summary>How many times the current node has been visited so far: 0 while being
    /// scheduled, 1 on its first visit, incrementing on each return to it.</summary>
    int VisitCount { get; }

    /// <summary>Whether the current event schedules the node (first appearance) or visits it
    /// (a return to it while its children are enumerated).</summary>
    TreenumeratorMode Mode { get; }

    /// <summary>
    /// The current node's position. Before the first <see cref="MoveNext"/> this must be
    /// <see cref="NodePosition.ForestRoot"/> (depth -1) with <see cref="VisitCount"/> 0 --
    /// consumers observe pre-enumeration state and rely on it.
    /// </summary>
    NodePosition Position { get; }
  }
}
