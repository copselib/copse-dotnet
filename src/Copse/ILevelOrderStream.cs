using System;

namespace Copse
{
  // The flat family's FORWARD-ONLY protocol for level-order-encoded trees: the streaming tier
  // of ILevelOrderStore, and IPreorderStream's structural dual. The encoding is a sequence of
  // child GROUPS in level order -- group 0 is the roots, and group k+1 holds the children of
  // the k-th node in level order (LOUDS-style). Positions are load-bearing: every node ever
  // delivered owns exactly one later group, so a consumer that discards a node must still
  // consume (and count) that node's group when its turn comes.
  //
  // The stream starts positioned inside group 0. SkipGroupRemainder is the skip seam:
  // implementations MUST NOT materialize (map) the values of discarded entries, but MUST
  // report how many there were (the count keeps the group/owner alignment intact downstream).
  //
  // Implementations own their underlying reader; the treenumerator riding the stream owns the
  // stream and disposes it.
  public interface ILevelOrderStream<TValue> : IDisposable
  {
    // Read the next value in the current group. False at the end of the group.
    bool TryReadNextInGroup(out TValue value);

    // Discard the remainder of the current group -- WITHOUT materializing values -- and return
    // how many entries were discarded.
    int SkipGroupRemainder();

    // Advance to the start of the next group; the current group must already be finished (read
    // or skipped to its end). False when the stream is exhausted -- every remaining group is
    // empty (trailing empty groups may be elided by the encoding).
    bool TryMoveToNextGroup();
  }
}
