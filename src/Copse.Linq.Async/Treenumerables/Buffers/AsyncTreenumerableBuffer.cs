using Copse;
using Copse.Stores;
using Copse.Linq.Stores;
using Copse.Core;
using Copse.Linq.Topologies;
using System.Threading.Tasks;

namespace Copse.Linq.Treenumerables
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
  internal sealed class AsyncTreenumerableBuffer<TNode> : IAsyncTreenumerableBuffer<TNode>
  {
    public AsyncTreenumerableBuffer(IAsyncTreenumerable<TNode> capture, BufferLayout? nativeLayout)
      : this(capture, nativeLayout, null)
    {
    }

    public AsyncTreenumerableBuffer(
      IAsyncTreenumerable<TNode> capture,
      BufferLayout? nativeLayout,
      IAsyncTreeTopology<TNode, int> adjacencyProbes)
    {
      _Capture = capture;
      NativeLayout = nativeLayout;
      _Topology = adjacencyProbes;
    }

    private readonly IAsyncTreenumerable<TNode> _Capture;
    private IAsyncTreeTopology<TNode, int> _Topology;

    // Null when the layout is decided by the first pull (Invert-F's dimension dispatch) --
    // Materialize's layout guarantee then transposes conservatively rather than guessing.
    public BufferLayout? NativeLayout { get; }

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() => _Capture.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() => _Capture.GetAsyncBreadthFirstTreenumerator();

    // The door binds the topology (the index) directly; nothing else asks this wrapper
    // adjacency questions.

    // The door: topology-at-birth -- the walker holds the
    // adjacency INDEX directly, so navigation never routes through this wrapper (one
    // dispatch: walker -> index -> arithmetic; the walkable exits the call path).
    public async ValueTask<AsyncTreeWalker<TNode, int>> GetTreeWalkerAsync()
      => new AsyncTreeWalker<TNode, int>(await EnsureTopologyAsync().ConfigureAwait(false));

    // The settle respects the declared layout: handles are ordinals in the CAPTURE'S OWN
    // encoding (the per-capture clause), so a level-order buffer's probes speak level-order
    // ordinals; the undecided case settles preorder (probing is consumption; the fresh-memo
    // pin rule's shape).
    // The bulk-fold fast path's door (the receiver-smart operators: LeaffixScan, Invert):
    // a preorder-settled buffer hands whole-tree algorithms its raw store -- Materialize's
    // `is ITreenumerableBuffer` receiver-smart idiom, one level deeper. A level-order buffer
    // and an un-settled one decline, so the miss is an expected answer, not a violation.
    internal async ValueTask<Option<AsyncPreorderArrayStore<TNode>>> TryGetPreorderStoreAsync()
    {
      if (NativeLayout == BufferLayout.LevelOrder)
        return default;

      var adjacencyProbes = await EnsureTopologyAsync().ConfigureAwait(false);

      if (adjacencyProbes is AsyncPreorderArrayTopology<TNode> arrayTopology)
        return new Option<AsyncPreorderArrayStore<TNode>>(arrayTopology.Store);

      // A Materialize-built buffer's probes ride its own lazy store (probes-at-birth);
      // forcing hands over the same arrays the stream half built or will build.
      if (adjacencyProbes is AsyncPreorderAdjacencyIndex<TNode, AsyncLazyPreorderStore<TNode>> lazyIndex)
        return new Option<AsyncPreorderArrayStore<TNode>>(
          await lazyIndex.Store.EnsureBuiltStoreAsync().ConfigureAwait(false));

      return default;
    }

    // The counted-source door (the presize fast-path): the exact node count, when
    // a completed store underneath already knows it -- a pure READ, never a build: the
    // undecided case and a lazy store not yet built decline (forcing an O(n) build to answer a
    // count question would be a surprising side effect; the uncounted capture path handles
    // those correctly). Callers presize transpose captures with the answer.
    internal Option<int> TryGetNodeCount()
    {
      if (_Topology == null)
        return default;

      UpgradeTopology();

      if (_Topology is AsyncPreorderArrayTopology<TNode> preorderTopology)
        return new Option<int>(preorderTopology.Store.Count);

      if (_Topology is AsyncLevelOrderArrayTopology<TNode> levelOrderTopology)
        return new Option<int>(levelOrderTopology.Store.Count);

      if (_Topology is AsyncPreorderAdjacencyIndex<TNode, AsyncLazyPreorderStore<TNode>> lazyPreorder
        && lazyPreorder.Store.IsBuilt)
        return new Option<int>(lazyPreorder.Store.BuiltStore.Count);

      if (_Topology is AsyncLevelOrderAdjacencyIndex<TNode, AsyncLazyLevelOrderStore<TNode>> lazyLevelOrder
        && lazyLevelOrder.Store.IsBuilt)
        return new Option<int>(lazyLevelOrder.Store.BuiltStore.Count);

      return default;
    }

    private async ValueTask<IAsyncTreeTopology<TNode, int>> EnsureTopologyAsync()
    {
      if (_Topology != null)
      {
        UpgradeTopology();
        return _Topology;
      }

      if (NativeLayout == BufferLayout.LevelOrder)
      {
        var levelOrderStore = await AsyncLevelOrderCapture.CaptureFromAsync(_Capture).ConfigureAwait(false);

        _Topology = new AsyncLevelOrderArrayTopology<TNode>(levelOrderStore);

        return _Topology;
      }

      var preorderStore = await AsyncPreorderCapture.CaptureFromAsync(_Capture).ConfigureAwait(false);

      _Topology = new AsyncPreorderArrayTopology<TNode>(preorderStore);

      return _Topology;
    }

    // The probes-at-birth reclaim (the history-bench finding, CHANGELOG_BENCHMARKS.md): a birth-bound
    // index rides the LAZY store and pays a delegation on every probe forever, where the
    // old settle's index rode the raw array store directly. Once the stream half has run
    // the one-shot build -- the common order: pull first, probe later -- and no probe has
    // advanced the index's scan, re-seat the index over the BUILT array store: the
    // settle-index's direct arithmetic, with none of its double capture. A scan already in
    // progress keeps its index (correct, merely indirect). Walkers minted after the
    // upgrade carry the fast index for life (topology-at-birth binds at door time).
    private void UpgradeTopology()
    {
      if (_Topology is AsyncPreorderAdjacencyIndex<TNode, AsyncLazyPreorderStore<TNode>> lazyPreorder
        && lazyPreorder.ScanUntouched
        && lazyPreorder.Store.IsBuilt)
      {
        _Topology = new AsyncPreorderArrayTopology<TNode>(lazyPreorder.Store.BuiltStore);
        return;
      }

      if (_Topology is AsyncLevelOrderAdjacencyIndex<TNode, AsyncLazyLevelOrderStore<TNode>> lazyLevelOrder
        && lazyLevelOrder.ScanUntouched
        && lazyLevelOrder.Store.IsBuilt)
      {
        _Topology = new AsyncLevelOrderArrayTopology<TNode>(lazyLevelOrder.Store.BuiltStore);
      }
    }
  }
}
