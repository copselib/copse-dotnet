using Copse.Async;
using Copse.Async.Stores;
using Copse.Linq.Async.Stores;
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
      IAsyncTreeTerrain<TValue, int> adjacencyProbes)
    {
      _Capture = capture;
      NativeLayout = nativeLayout;
      _AdjacencyProbes = adjacencyProbes;
    }

    private readonly IAsyncTreenumerable<TValue> _Capture;
    private IAsyncTreeTerrain<TValue, int> _AdjacencyProbes;

    // Null when the layout is decided by the first pull (Invert-F's dimension dispatch) --
    // Materialize's layout guarantee then transposes conservatively rather than guessing.
    public BufferLayout? NativeLayout { get; }

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator() => _Capture.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator() => _Capture.GetAsyncBreadthFirstTreenumerator();

    public async ValueTask<TValue> GetValueAsync(int handle)
      => await (await EnsureAdjacencyProbesAsync().ConfigureAwait(false)).GetValueAsync(handle).ConfigureAwait(false);

    public async ValueTask<ParentResult<int>> TryGetParentAsync(int handle)
      => await (await EnsureAdjacencyProbesAsync().ConfigureAwait(false)).TryGetParentAsync(handle).ConfigureAwait(false);

    public async ValueTask<ChildResult<int>> TryGetChildAtAsync(int handle, int childIndex)
      => await (await EnsureAdjacencyProbesAsync().ConfigureAwait(false)).TryGetChildAtAsync(handle, childIndex).ConfigureAwait(false);

    public async ValueTask<ChildResult<int>> TryGetRootAtAsync(int rootIndex)
      => await (await EnsureAdjacencyProbesAsync().ConfigureAwait(false)).TryGetRootAtAsync(rootIndex).ConfigureAwait(false);

    // The door (walker factory design, Stage A): terrain-at-birth -- the walker holds the
    // adjacency INDEX directly, so navigation never routes through this wrapper (one
    // dispatch: walker -> index -> arithmetic; the walkable exits the call path).
    public async ValueTask<AsyncTreeWalkerResult<TValue, int>> TryGetTreeWalkerAsync()
    {
      var terrain = await EnsureAdjacencyProbesAsync().ConfigureAwait(false);
      var rootResult = await terrain.TryGetRootAtAsync(0).ConfigureAwait(false);

      return rootResult.HasChild
        ? new AsyncTreeWalkerResult<TValue, int>(new AsyncTreeWalker<TValue, int>(terrain, rootResult.Child.Node))
        : default;
    }

    // The settle respects the declared layout: handles are ordinals in the CAPTURE'S OWN
    // encoding (the per-capture clause), so a level-order buffer's probes speak level-order
    // ordinals; the undecided case settles preorder (probing is consumption; the fresh-memo
    // pin rule's shape).
    // The bulk-fold fast path's door (the receiver-smart operators: LeaffixScan, Invert):
    // a preorder-settled buffer hands whole-tree algorithms its raw store -- Materialize's
    // `is ITreenumerableBuffer` receiver-smart idiom, one level deeper. Tuple-shaped
    // because `out` cannot cross an `await` -- the async spelling of the try-pattern.
    internal async ValueTask<(bool HasStore, AsyncPreorderArrayStore<TValue> Store)> TryGetPreorderStoreAsync()
    {
      if (NativeLayout == BufferLayout.LevelOrder)
        return (false, default);

      var adjacencyProbes = await EnsureAdjacencyProbesAsync().ConfigureAwait(false);

      if (adjacencyProbes is AsyncPreorderAdjacencyIndex<TValue, AsyncPreorderArrayStore<TValue>> arrayIndex)
        return (true, arrayIndex.Store);

      // A Materialize-built buffer's probes ride its own lazy store (probes-at-birth);
      // forcing hands over the same arrays the stream half built or will build.
      if (adjacencyProbes is AsyncPreorderAdjacencyIndex<TValue, AsyncLazyPreorderStore<TValue>> lazyIndex)
        return (true, await lazyIndex.Store.EnsureBuiltStoreAsync().ConfigureAwait(false));

      return (false, default);
    }

    private async ValueTask<IAsyncTreeTerrain<TValue, int>> EnsureAdjacencyProbesAsync()
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
