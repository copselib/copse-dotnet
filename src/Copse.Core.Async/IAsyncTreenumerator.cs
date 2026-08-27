using System;
using System.Threading.Tasks;

namespace Copse.Core.Async
{
  /// <summary>
  /// A stateful async traversal cursor over a tree, advanced with <see cref="MoveNextAsync"/>.
  /// Each advance lands on one event of the visit stream: the current node
  /// (<see cref="Node"/>), whether it is being scheduled or visited (<see cref="Mode"/>), how
  /// many visits it has received (<see cref="VisitCount"/>), and its position
  /// (<see cref="Position"/>). It produces the same visit stream as the synchronous cursor
  /// over the same tree, awaited. Dispose when done.
  /// </summary>
  public interface IAsyncTreenumerator<TNode> : IAsyncDisposable
  {
    /// <summary>Advances to the next visit event, pruning the traversal from the current node
    /// as <paramref name="nodeTraversalStrategies"/> directs
    /// (<see cref="NodeTraversalStrategies.TraverseAll"/> prunes nothing). Completes with
    /// <c>false</c> when the traversal is exhausted.</summary>
    ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies);

    /// <summary>The node the traversal is currently visiting (or scheduling -- see <see cref="Mode"/>).</summary>
    TNode Node { get; }

    /// <summary>How many times the current node has been visited so far: 0 while being
    /// scheduled, 1 on its first visit, incrementing on each return to it.</summary>
    int VisitCount { get; }

    /// <summary>Whether the current event schedules the node (first appearance) or visits it
    /// (a return to it while its children are enumerated).</summary>
    TreenumeratorMode Mode { get; }

    /// <summary>
    /// The current node's position. Before the first <see cref="MoveNextAsync"/> this must be
    /// <see cref="NodePosition.ForestRoot"/> (depth -1) with <see cref="VisitCount"/> 0 --
    /// consumers observe pre-enumeration state and rely on it.
    /// </summary>
    NodePosition Position { get; }
  }
}
