using Copse.Stores;
using Copse.Treenumerators;
using Copse.Core;
using System;

namespace Copse.Treenumerables
{
  /// <summary>
  /// An async tree streaming from a forward-only level-order source: the async analog of
  /// <c>Copse.Treenumerables.LevelOrderStreamTreenumerable</c>, and deliberately only an
  /// <see cref="IAsyncBreadthFirstTreenumerable{TNode}"/> -- a one-pass source cannot affordably
  /// serve the depth-first dimension. Each acquisition invokes the factory for a fresh stream and
  /// OWNS it (async disposal closes it); re-enumeration re-reads the source.
  /// </summary>
  internal sealed class AsyncLevelOrderStreamTreenumerable<TNode, TStream> : IAsyncBreadthFirstTreenumerable<TNode>
    where TStream : IAsyncLevelOrderStream<TNode>
  {
    /// <summary>Wraps a forward-only level-order stream factory; each traversal opens a
    /// fresh stream and disposes it with the treenumerator.</summary>
    public AsyncLevelOrderStreamTreenumerable(Func<TStream> streamFactory)
    {
      _StreamFactory = streamFactory;
    }

    private readonly Func<TStream> _StreamFactory;

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
      => new AsyncLevelOrderStreamBreadthFirstTreenumerator<TNode, TStream>(_StreamFactory());
  }
}
