using System;
using System.Threading.Tasks;

namespace Copse.Async
{
  // Async, struct-return twin of IPreorderStream (Copse.Primitives): the forward-only preorder
  // protocol, read asynchronously. Reads return ValueTask<PreorderRead<TValue>> -- the struct-return
  // shape is mandatory (out params can't cross an await) and is the single codegen source the
  // generator transcribes into the sync IPreorderStream twin. IAsyncDisposable: the treenumerator
  // riding the stream owns it and disposes it (async).
  public interface IAsyncPreorderStream<TValue> : IAsyncDisposable
  {
    // Read the next preorder node. HasValue == false when the stream is exhausted.
    ValueTask<PreorderRead<TValue>> TryReadNextAsync();

    // Discard nodes -- WITHOUT materializing their values -- until one arrives at depth <=
    // maxDepth, and return it. HasValue == false when the stream exhausts first.
    ValueTask<PreorderRead<TValue>> TrySkipToDepthAsync(int maxDepth);
  }
}
