using Copse.Async.Treenumerators;
using Copse.Core.Async;
using System;

namespace Copse.Async.Treenumerables
{
  /// <summary>
  /// An async tree streaming from a forward-only preorder source: the async analog of
  /// <c>Copse.Treenumerables.PreorderStreamTreenumerable</c>, and deliberately only an
  /// <see cref="IAsyncDepthFirstTreenumerable{TValue}"/> -- a one-pass source cannot affordably serve
  /// the breadth-first dimension. Each acquisition invokes the factory for a fresh stream and OWNS it
  /// (async disposal closes it); re-enumeration re-reads the source.
  /// </summary>
  public sealed class AsyncPreorderStreamTreenumerable<TValue, TStream> : IAsyncDepthFirstTreenumerable<TValue>
    where TStream : IAsyncPreorderStream<TValue>
  {
    public AsyncPreorderStreamTreenumerable(Func<TStream> streamFactory)
    {
      _StreamFactory = streamFactory;
    }

    private readonly Func<TStream> _StreamFactory;

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator()
      => new AsyncPreorderStreamDepthFirstTreenumerator<TValue, TStream>(_StreamFactory());
  }
}
