using System;
using System.Threading.Tasks;

namespace Copse.Core.Async
{
  /// <summary>
  /// Async analog of <see cref="ITreenumerator{TNode}"/>: the stateful traversal cursor, advanced with
  /// <see cref="MoveNextAsync"/>. Position/VisitCount/Mode semantics are identical to the synchronous
  /// contract -- the same visit stream, awaited.
  /// </summary>
  public interface IAsyncTreenumerator<TNode> : IAsyncDisposable
  {
    ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies);

    TNode Node { get; }
    int VisitCount { get; }
    TreenumeratorMode Mode { get; }

    /// <summary>
    /// The current node's position. Before the first <see cref="MoveNextAsync"/> this must be
    /// <see cref="NodePosition.ForestRoot"/> (depth -1) with <see cref="VisitCount"/> 0.
    /// </summary>
    NodePosition Position { get; }
  }
}
