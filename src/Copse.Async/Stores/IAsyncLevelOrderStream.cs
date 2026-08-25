using System;
using System.Threading.Tasks;

namespace Copse.Async.Stores
{
  // Codegen source of the sync twin, Copse.Stores.ILevelOrderStream.
  /// <summary>
  /// The forward-only level-order stream protocol: one pass over a tree encoded as sibling
  /// groups, read group by group. The treenumerator riding the stream owns it and disposes
  /// it.
  /// </summary>
  public interface IAsyncLevelOrderStream<TNode> : IAsyncDisposable
  {
    /// <summary>Reads the next value in the current group, or absent at the end of the
    /// group.</summary>
    ValueTask<Option<TNode>> TryReadNextInGroupAsync();

    /// <summary>Discards the remainder of the current group -- without materializing values --
    /// and completes with how many entries were discarded.</summary>
    ValueTask<int> SkipGroupRemainderAsync();

    /// <summary>Advances to the start of the next group; the current group must already be
    /// finished. Completes with <c>false</c> when the stream is exhausted.</summary>
    ValueTask<bool> TryMoveToNextGroupAsync();
  }
}
