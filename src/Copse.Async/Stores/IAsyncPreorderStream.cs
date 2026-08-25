using System;
using System.Threading.Tasks;

namespace Copse.Async.Stores
{
  // Codegen source of the sync twin, Copse.Stores.IPreorderStream.
  /// <summary>
  /// The forward-only preorder stream protocol: one pass over a tree encoded as (value, depth)
  /// reads. The treenumerator riding the stream owns it and disposes it.
  /// </summary>
  public interface IAsyncPreorderStream<TNode> : IAsyncDisposable
  {
    /// <summary>Reads the next preorder node, or absent when the stream is
    /// exhausted.</summary>
    ValueTask<Option<PreorderRead<TNode>>> TryReadNextAsync();

    /// <summary>Discards nodes -- without materializing their values -- until one arrives at
    /// depth at most <paramref name="maxDepth"/>, and returns it; absent when the stream
    /// exhausts first.</summary>
    ValueTask<Option<PreorderRead<TNode>>> TrySkipToDepthAsync(int maxDepth);
  }
}
