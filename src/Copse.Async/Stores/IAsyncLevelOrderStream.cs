using System;
using System.Threading.Tasks;

namespace Copse.Async.Stores
{
  // Async, struct-return source of ILevelOrderStream (the generated sync twin): the forward-only level-order
  // protocol read asynchronously. The value read returns ValueTask<LevelOrderRead<TValue>> -- the
  // struct-return shape is mandatory (out params can't cross an await) and is the single codegen
  // source the generator transcribes into the sync ILevelOrderStream twin. The skip-count and
  // group-boundary signals stay ValueTask<int> / ValueTask<bool>. IAsyncDisposable: the
  // treenumerator riding the stream owns it and disposes it (async).
  public interface IAsyncLevelOrderStream<TValue> : IAsyncDisposable
  {
    // Read the next value in the current group. HasValue == false at the end of the group.
    ValueTask<LevelOrderRead<TValue>> TryReadNextInGroupAsync();

    // Discard the remainder of the current group -- WITHOUT materializing values -- and return
    // how many entries were discarded.
    ValueTask<int> SkipGroupRemainderAsync();

    // Advance to the start of the next group; the current group must already be finished. False
    // when the stream is exhausted.
    ValueTask<bool> TryMoveToNextGroupAsync();
  }
}
