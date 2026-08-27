using Copse.Core.Async;
using System;

namespace Copse.Async.Treenumerables
{
  // Async analog of Copse.Treenumerables.DelegatingTreenumerable: a composite async tree whose two
  // dimension cursors come from injected factories. This is what AsyncTree.Defer returns -- the
  // factory indirection is where "fresh tree per acquisition" lives.
  /// <summary>A treenumerable that delegates each acquisition to an injected treenumerator
  /// factory pair -- what <c>Tree.Create</c> and the deferral factories return.</summary>
  internal sealed class AsyncDelegatingTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    /// <summary>Builds each traversal from its factory; nothing runs until a treenumerator
    /// is acquired.</summary>
    public AsyncDelegatingTreenumerable(
      Func<IAsyncTreenumerator<TNode>> breadthFirstTreenumeratorFactory,
      Func<IAsyncTreenumerator<TNode>> depthFirstTreenumeratorFactory)
    {
      _BreadthFirstTreenumeratorFactory = breadthFirstTreenumeratorFactory;
      _DepthFirstTreenumeratorFactory = depthFirstTreenumeratorFactory;
    }

    private readonly Func<IAsyncTreenumerator<TNode>> _BreadthFirstTreenumeratorFactory;
    private readonly Func<IAsyncTreenumerator<TNode>> _DepthFirstTreenumeratorFactory;

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() => _BreadthFirstTreenumeratorFactory();

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() => _DepthFirstTreenumeratorFactory();
  }
}
