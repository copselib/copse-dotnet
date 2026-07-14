using Copse.Core;
using Copse.Core.Async;

namespace Copse.Linq.Async.Treenumerables
{
  // A completed, owned, in-memory capture presented as the non-disposable ITreenumerableBuffer
  // marker: a thin wrapper that delegates both dimensions to an inner in-memory treenumerable
  // (a flat store). This is what the eager capture operators (LeaffixScan, Invert) return once
  // their store is built -- the O(n) is disclosed by the buffer type, but there is no live
  // source feed, so nothing to dispose.
  //
  // The inner may build lazily on first acquisition; "completed" is about there being no live
  // feed to retire, not about eagerness. (The flat-store treenumerable is in Copse and cannot
  // implement this Copse.Linq interface directly, hence the wrapper.)
  internal sealed class AsyncCompletedTreenumerableBuffer<TValue> : IAsyncTreenumerableBuffer<TValue>, IAsyncLayoutTaggedBuffer
  {
    public AsyncCompletedTreenumerableBuffer(IAsyncTreenumerable<TValue> capture, TreeTraversalStrategy? nativeLayout)
    {
      _Capture = capture;
      NativeLayout = nativeLayout;
    }

    private readonly IAsyncTreenumerable<TValue> _Capture;

    // Null when the layout is decided by the first pull (Invert-F's dimension dispatch) --
    // Materialize's layout guarantee then transposes conservatively rather than guessing.
    public TreeTraversalStrategy? NativeLayout { get; }

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator() => _Capture.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator() => _Capture.GetAsyncBreadthFirstTreenumerator();
  }
}
