using Copse.Core;
using Copse.Linq.Stores;
using Copse.Linq.Treenumerables;
using Copse.Stores;
using Copse.Treenumerables;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    /// <summary>
    /// The walker escalation -- the FINITE-IZING act, and the type says so: the return is the
    /// intersection <see cref="IWalkableTreenumerableBuffer{TValue, TNode}"/>, adjacency
    /// manufactured AND the structure captured (docs/WALKER_DESIGN.md, the finiteness law).
    /// Deferred per the lazy-Materialize law: nothing is enumerated at the call -- the capture
    /// (one depth-first walk into preorder arrays) runs through the lazy store's grow seam at
    /// the first pull, streaming or adjacency alike. The layout is the walker default,
    /// preorder -- the ancestry-cheap capture, per the adjacency-first rider. (PoC scope: this
    /// is the capture rung and the intersection probe; the declared-layout form, the organic
    /// dimension dispatch, and the buffer-recovery rung of the probe ladder arrive with the
    /// buffer-store plumbing.)
    ///
    /// <para>Probes first, like Materialize: an intersection citizen is returned as-is --
    /// never re-captured, never re-walked.</para>
    /// </summary>
    public static IWalkableTreenumerableBuffer<TValue, int> MaterializeWalkable<TValue>(this ITreenumerable<TValue> source)
    {
      if (source is IWalkableTreenumerableBuffer<TValue, int> walkableBuffer)
        return walkableBuffer;

      return new WalkableTreenumerableBuffer<TValue>(
        new WalkablePreorderTreenumerable<TValue, LazyPreorderStore<TValue>>(
          new LazyPreorderStore<TValue>(() => PreorderCapture.CaptureFrom(source))),
        BufferLayout.Preorder);
    }
  }
}
