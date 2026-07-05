namespace Copse
{
  // The flat family's store protocol for preorder-laid-out trees -- IChildEnumerator's
  // counterpart for trees stored as linear encodings rather than linked structure (see
  // PACKAGE_ARCHITECTURE.md, "two families"). values[i] in preorder; node i's subtree spans
  // [i, i + subtreeSize(i)).
  //
  // The Ensure* pair exists because a store may still be GROWING (a memo capture suspended
  // mid-feed): implementations pull their underlying stream just far enough to answer. A
  // completed store (a rehydrated capture, PreorderTree's arrays) satisfies them trivially.
  public interface IPreorderStore<TValue>
  {
    // Grow the store until the node at index exists. False iff the underlying stream
    // exhausted first (no such node).
    bool EnsureBuffered(int index);

    // Grow the store until node index's subtree closes, and return its size (>= 1). The node
    // itself must already be buffered.
    int EnsureSubtreeClosed(int index);

    // 0 while node index's subtree is still open (a closed subtree's size is >= 1).
    int GetSubtreeSize(int index);

    TValue GetValue(int index);
  }
}
