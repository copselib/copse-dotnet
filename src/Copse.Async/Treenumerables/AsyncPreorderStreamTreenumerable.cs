using Copse.Stores;
using Copse.Treenumerators;
using Copse.Core;
using System;

namespace Copse.Treenumerables
{
  /// <summary>
  /// An async tree streaming from a forward-only preorder source: the async analog of
  /// <c>Copse.Treenumerables.PreorderStreamTreenumerable</c>, and deliberately only an
  /// <see cref="IAsyncDepthFirstTreenumerable{TNode}"/> -- a one-pass source cannot affordably serve
  /// the breadth-first dimension. Each acquisition invokes the factory for a fresh stream and OWNS it
  /// (async disposal closes it); re-enumeration re-reads the source.
  /// </summary>
  internal sealed class AsyncPreorderStreamTreenumerable<TNode, TStream> : IAsyncDepthFirstTreenumerable<TNode>
    where TStream : IAsyncPreorderStream<TNode>
  {
    /// <summary>Wraps a forward-only preorder stream factory; each traversal opens a fresh
    /// stream and disposes it with the treenumerator.</summary>
    public AsyncPreorderStreamTreenumerable(Func<TStream> streamFactory)
    {
      _StreamFactory = streamFactory;
    }

    private readonly Func<TStream> _StreamFactory;

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
      => new AsyncPreorderStreamDepthFirstTreenumerator<TNode, TStream>(_StreamFactory());
  }
}
