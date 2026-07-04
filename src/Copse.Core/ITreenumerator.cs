using System;

namespace Copse.Core
{
  public interface ITreenumerator<TNode> : IDisposable
  {
    bool MoveNext(NodeTraversalStrategies nodeTraversalStrategies);

    TNode Node { get; }
    int VisitCount { get; }
    TreenumeratorMode Mode { get; }

    /// <summary>
    /// The current node's position. Before the first <see cref="MoveNext"/> this must be
    /// <see cref="NodePosition.ForestRoot"/> (depth -1) with <see cref="VisitCount"/> 0 --
    /// consumers (e.g. Where's sentinel seeding and its not-started test) observe
    /// pre-enumeration state and rely on it.
    /// </summary>
    NodePosition Position { get; }
  }
}
