using Copse.Core.Async;
using System;

namespace Copse.Async.Treenumerables
{
  // Async analog of Copse.Treenumerables.DelegatingBreadthFirstTreenumerable: the single-dimension
  // sibling the narrow async operator overloads return, so a chain over an
  // IAsyncBreadthFirstTreenumerable source stays honestly breadth-first-only end to end. This is
  // the codegen source of truth for the sync twin.
  /// <summary>The breadth-first-narrow form of <c>AsyncDelegatingTreenumerable</c>: one
  /// injected treenumerator factory.</summary>
  internal sealed class AsyncDelegatingBreadthFirstTreenumerable<TNode> : IAsyncBreadthFirstTreenumerable<TNode>
  {
    /// <summary>Builds each traversal from the factory; nothing runs until a treenumerator
    /// is acquired.</summary>
    public AsyncDelegatingBreadthFirstTreenumerable(Func<IAsyncTreenumerator<TNode>> breadthFirstTreenumeratorFactory)
    {
      _BreadthFirstTreenumeratorFactory = breadthFirstTreenumeratorFactory;
    }

    private readonly Func<IAsyncTreenumerator<TNode>> _BreadthFirstTreenumeratorFactory;

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() => _BreadthFirstTreenumeratorFactory();
  }
}
