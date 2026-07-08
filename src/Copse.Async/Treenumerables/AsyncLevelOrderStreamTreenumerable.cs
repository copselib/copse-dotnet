using Copse.Core.Async;
using System;
using Copse.Async.Treenumerators;

namespace Copse.Async.Treenumerables
{
  /// <summary>
  /// An async tree streaming from a forward-only level-order source: the async analog of
  /// <c>Copse.Treenumerables.LevelOrderStreamTreenumerable</c>, and deliberately only an
  /// <see cref="IAsyncBreadthFirstTreenumerable{TValue}"/> -- a one-pass source cannot affordably
  /// serve the depth-first dimension. Each acquisition invokes the factory for a fresh stream and
  /// OWNS it (async disposal closes it); re-enumeration re-reads the source.
  /// </summary>
  public sealed class AsyncLevelOrderStreamTreenumerable<TValue, TStream> : IAsyncBreadthFirstTreenumerable<TValue>
    where TStream : IAsyncLevelOrderStream<TValue>
  {
    public AsyncLevelOrderStreamTreenumerable(Func<TStream> streamFactory)
    {
      _StreamFactory = streamFactory;
    }

    private readonly Func<TStream> _StreamFactory;

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator()
      => new AsyncLevelOrderStreamBreadthFirstTreenumerator<TValue, TStream>(_StreamFactory());
  }
}
