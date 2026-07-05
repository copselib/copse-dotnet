namespace Copse
{
  // The flat family's store protocol for level-order-laid-out trees -- IPreorderStore's
  // structural dual (LOUDS-adjacent where that one is balanced-parentheses-adjacent). values[i]
  // in level order; each node's children sit contiguously at [firstChildIndex,
  // firstChildIndex + childCount), and the roots are the depth-0 prefix (root ordinal k IS
  // buffer index k).
  //
  // The Ensure* pair exists because a store may still be GROWING (a memo capture suspended
  // mid-feed): implementations pull their underlying stream just far enough to answer. Children
  // are served as they appear -- a span need not close unless the answer is "no more". A
  // completed store satisfies both trivially.
  public interface ILevelOrderStore<TValue>
  {
    // Grow the store until root ordinal k exists. False iff the root frontier closed first
    // (k is past the last root).
    bool EnsureRootAvailable(int k);

    // Grow the store until child ordinal k of the (already-available) parent exists. False iff
    // the parent's span closed first (k is past its last child).
    bool EnsureChildAvailable(int parentIndex, int k);

    // The buffer index of the parent's first child. Only meaningful once the parent has at
    // least one available child (a successful EnsureChildAvailable(parentIndex, 0)).
    int GetFirstChildIndex(int parentIndex);

    TValue GetValue(int index);
  }
}
