using System;

namespace Copse
{
  // The flat family's FORWARD-ONLY protocol for preorder-encoded trees: the streaming tier of
  // IPreorderStore (see TRAVERSAL_DIMENSION_SPLIT.md -- a random-access store buys full
  // ITreenumerable citizenship; a forward-only stream affords only the depth-first dimension,
  // and this protocol is what that narrower type rides). One pass, node by node, each node
  // delivered with its depth; subtree structure is implied by the depth sequence exactly as it
  // is by the preorder layout itself.
  //
  // TrySkipToDepth is the skip seam: consumer pruning over a forward-only source cannot seek,
  // so it discards -- implementations MUST NOT materialize (map) the values of discarded nodes,
  // making a skip cost I/O only.
  //
  // Implementations own their underlying reader; the treenumerator riding the stream owns the
  // stream and disposes it (ITreenumerator is IDisposable -- the standard ownership hook).
  public interface IPreorderStream<TValue> : IDisposable
  {
    // Read the next preorder node. False when the stream is exhausted.
    bool TryReadNext(out TValue value, out int depth);

    // Discard nodes -- WITHOUT materializing their values -- until one arrives at depth <=
    // maxDepth, and deliver that node. False when the stream exhausts first.
    bool TrySkipToDepth(int maxDepth, out TValue value, out int depth);
  }
}
