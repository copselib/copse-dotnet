using System.Threading.Tasks;

namespace Copse.Async
{
  // Async analog of Copse.IPreorderStore: the flat family's preorder store protocol for a store
  // that may still be GROWING from an ASYNC feed (a lazy async memo suspended mid-capture). The
  // grow operations await -- they pull the underlying async stream just far enough to answer; a
  // completed capture satisfies them with a completed ValueTask. GetValue/GetSubtreeSize are pure
  // reads of already-buffered data and stay synchronous (the decoder always ensures before it
  // reads, so a completed grow guarantees the read is in range).
  //
  // The sync twin (Copse.IPreorderStore) is what the codegen produces from any store decoder
  // written against this; see IPreorderStore for the full contract.
  public interface IAsyncPreorderStore<TValue>
  {
    // Grow the store until the node at index exists. False iff the underlying stream exhausted
    // first (no such node).
    ValueTask<bool> EnsureBufferedAsync(int index);

    // Grow the store until node index's subtree closes, and return its size (>= 1). The node
    // itself must already be buffered.
    ValueTask<int> EnsureSubtreeClosedAsync(int index);

    // 0 while node index's subtree is still open (a closed subtree's size is >= 1).
    int GetSubtreeSize(int index);

    TValue GetValue(int index);
  }
}
