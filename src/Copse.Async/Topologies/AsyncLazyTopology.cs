using System.Threading.Tasks;

namespace Copse.Async.Topologies
{
  // TreeTopology.Lazy's engine: a walkable's topology, call-by-need. Anything that must
  // build lazily over "whatever topology this walkable's door will bind" -- a view whose
  // constructor may neither await nor force the source -- holds this: the knock happens
  // once, at the first probe (Tree.Lazy's semantics at the topology tier: cached, never
  // re-knocked -- the contract does not promise cheap or idempotent doors, so the cache
  // is what keeps a view honest against the weakest citizen), and every answer after
  // flows through the topology the knock bound. The door is total (the void stance), so
  // the empty forest needs no special citizen here either: its bound topology answers the
  // probes itself -- no roots, no parents, no children, and GetValue's own violation
  // channel. Internal sealed like every topology implementation: the factory hands out
  // the contract, never the encoding (the store policy's rule).
  internal sealed class AsyncLazyTopology<TNode, THandle> : IAsyncTreeTopology<TNode, THandle>
  {
    public AsyncLazyTopology(IAsyncWalkableTreenumerable<TNode, THandle> source)
    {
      _Source = source;
    }

    private readonly IAsyncWalkableTreenumerable<TNode, THandle> _Source;
    private IAsyncTreeTopology<TNode, THandle> _Topology;

    private async ValueTask<IAsyncTreeTopology<TNode, THandle>> ResolveAsync()
    {
      if (_Topology != null)
        return _Topology;

      var door = await _Source.GetTreeWalkerAsync().ConfigureAwait(false);

      // The door yields a stance; its public Topology property is the bound physics
      // (the frame-of-reference ruling).
      _Topology = door.Topology;

      return _Topology;
    }

    public async ValueTask<TNode> GetValueAsync(THandle handle)
      => await (await ResolveAsync().ConfigureAwait(false)).GetValueAsync(handle).ConfigureAwait(false);

    public async ValueTask<Option<THandle>> TryGetParentAsync(THandle handle)
      => await (await ResolveAsync().ConfigureAwait(false)).TryGetParentAsync(handle).ConfigureAwait(false);

    public async ValueTask<Option<NodeAndSiblingIndex<THandle>>> TryGetChildAtAsync(THandle handle, int childIndex)
      => await (await ResolveAsync().ConfigureAwait(false)).TryGetChildAtAsync(handle, childIndex).ConfigureAwait(false);

    public async ValueTask<Option<NodeAndSiblingIndex<THandle>>> TryGetRootAtAsync(int rootIndex)
      => await (await ResolveAsync().ConfigureAwait(false)).TryGetRootAtAsync(rootIndex).ConfigureAwait(false);
  }
}
