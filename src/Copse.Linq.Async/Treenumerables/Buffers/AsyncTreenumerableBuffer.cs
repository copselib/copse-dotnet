using Copse.Async;
using Copse.Async.Stores;
using Copse.Core;
using Copse.Core.Async;
using System.Threading.Tasks;

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
  //
  // The adjacency half (the buffer re-parent): construction sites that hold the store pass an
  // adjacency engine over it, and probes delegate. The dimension-dispatched case (layout
  // undecided until the first pull -- the inner is an opaque lazy build, no store in hand)
  // has no engine; its first PROBE settles by capturing the inner into a preorder store and
  // probing that -- one extra O(n) capture, paid once, only on the rare undecided path, and
  // only if anyone probes at all. The stream half is untouched by the settle (replays keep
  // riding the inner), and NativeLayout keeps reporting the stream side's truth.
  internal sealed class AsyncTreenumerableBuffer<TValue> : IAsyncTreenumerableBuffer<TValue>
  {
    public AsyncTreenumerableBuffer(IAsyncTreenumerable<TValue> capture, BufferLayout? nativeLayout)
      : this(capture, nativeLayout, null)
    {
    }

    public AsyncTreenumerableBuffer(
      IAsyncTreenumerable<TValue> capture,
      BufferLayout? nativeLayout,
      IAsyncAdjacencyProbes<TValue> adjacencyProbes)
    {
      _Capture = capture;
      NativeLayout = nativeLayout;
      _AdjacencyProbes = adjacencyProbes;
    }

    private readonly IAsyncTreenumerable<TValue> _Capture;
    private IAsyncAdjacencyProbes<TValue> _AdjacencyProbes;

    // Null when the layout is decided by the first pull (Invert-F's dimension dispatch) --
    // Materialize's layout guarantee then transposes conservatively rather than guessing.
    public BufferLayout? NativeLayout { get; }

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator() => _Capture.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator() => _Capture.GetAsyncBreadthFirstTreenumerator();

    public async ValueTask<TValue> GetValueAsync(int handle)
      => await (await EnsureAdjacencyProbesAsync().ConfigureAwait(false)).GetValueAsync(handle).ConfigureAwait(false);

    public async ValueTask<ParentResult<int>> GetParentAsync(int handle)
      => await (await EnsureAdjacencyProbesAsync().ConfigureAwait(false)).GetParentAsync(handle).ConfigureAwait(false);

    public async ValueTask<ChildResult<int>> GetChildAtAsync(int handle, int childIndex)
      => await (await EnsureAdjacencyProbesAsync().ConfigureAwait(false)).GetChildAtAsync(handle, childIndex).ConfigureAwait(false);

    public async ValueTask<ChildResult<int>> GetRootAtAsync(int rootIndex)
      => await (await EnsureAdjacencyProbesAsync().ConfigureAwait(false)).GetRootAtAsync(rootIndex).ConfigureAwait(false);

    // The settle respects the declared layout: handles are ordinals in the CAPTURE'S OWN
    // encoding (the per-capture clause), so a level-order buffer's probes speak level-order
    // ordinals; the undecided case settles preorder (probing is consumption; the fresh-memo
    // pin rule's shape).
    private async ValueTask<IAsyncAdjacencyProbes<TValue>> EnsureAdjacencyProbesAsync()
    {
      if (_AdjacencyProbes != null)
        return _AdjacencyProbes;

      if (NativeLayout == BufferLayout.LevelOrder)
      {
        var levelOrderStore = await AsyncLevelOrderCapture.CaptureFromAsync(_Capture).ConfigureAwait(false);

        _AdjacencyProbes = new AsyncLevelOrderAdjacencyIndex<TValue, AsyncLevelOrderArrayStore<TValue>>(levelOrderStore);

        return _AdjacencyProbes;
      }

      var preorderStore = await AsyncPreorderCapture.CaptureFromAsync(_Capture).ConfigureAwait(false);

      _AdjacencyProbes = new AsyncPreorderAdjacencyIndex<TValue, AsyncPreorderArrayStore<TValue>>(preorderStore);

      return _AdjacencyProbes;
    }
  }
}
