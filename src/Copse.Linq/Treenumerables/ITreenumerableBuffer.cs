using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // A treenumerable backed by an owned, lazily-growing capture of another treenumerable
  // (see Memoize). IDisposable because the buffer holds live inner treenumerators paused
  // mid-traversal over the source -- the captured data itself is just managed memory.
  // Disposing stops all future source consumption: enumerators already replaying keep
  // working over the captured region, but one that needs data beyond it throws
  // ObjectDisposedException.
  public interface ITreenumerableBuffer<TValue> : ITreenumerable<TValue>, IDisposable
  {
    // True once either dimension's capture is complete: the whole tree is held and the source
    // is permanently retired -- no future enumeration, in either dimension, touches it again.
    bool IsComplete { get; }

    // Nodes captured so far in the given dimension's buffer (0 if that dimension has never been
    // enumerated). Both dimensions count toward the same total, so the larger count is the
    // cheaper capture to finish -- the comparison Consume()/Materialize() policy runs on. Not a
    // progress fraction: the tree's size is unknown until a capture completes.
    int GetBufferedCount(TreeTraversalStrategy strategy);

    // Drive the given dimension's capture to completion (MoreLINQ's Consume, aimed at one
    // dimension). Obedient below the invariant: a no-op iff IsComplete -- a retired source is
    // never re-enumerated, no argument overrides the memo's single-shape guarantee. See the
    // parameterless Consume() extension for the count-comparison policy.
    void Consume(TreeTraversalStrategy strategy);
  }
}
