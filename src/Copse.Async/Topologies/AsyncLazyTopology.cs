using Copse.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Async.Topologies
{
  // TreeTopology.Lazy's engine: a walkable's topology, call-by-need. Anything that must
  // build lazily over "whatever topology this walkable's door will bind" -- a view whose
  // constructor may neither await nor force the source -- holds this: the knock happens
  // once, at the first probe (Tree.Lazy's semantics at the topology tier: cached, never
  // re-knocked -- the contract does not promise cheap or idempotent doors, so the cache
  // is what keeps a view honest against the weakest citizen), and every answer after
  // flows through the walker the knock produced. The empty forest needs no special
  // citizen: its door misses, so the result-typed probes miss honestly (no roots, no
  // parents, no children) and the one probe that MUST produce a value (GetValue) throws
  // -- on an empty forest every handle is forged, so the ask is a violation, not a miss
  // (the two-channel doctrine). Internal sealed like every topology implementation: the
  // factory hands out the contract, never the encoding (the store policy's rule).
  internal sealed class AsyncLazyTopology<TValue, THandle> : IAsyncTreeTopology<TValue, THandle>
  {
    public AsyncLazyTopology(IAsyncWalkableTreenumerable<TValue, THandle> source)
    {
      _Source = source;
    }

    private readonly IAsyncWalkableTreenumerable<TValue, THandle> _Source;
    private IAsyncTreeTopology<TValue, THandle> _Topology;
    private bool _Resolved;

    private async ValueTask<IAsyncTreeTopology<TValue, THandle>> ResolveAsync()
    {
      if (_Resolved)
        return _Topology;

      var door = await _Source.TryGetTreeWalkerAsync().ConfigureAwait(false);

      // The door yields a vantage; its public Topology property is the bound physics
      // (the frame-of-reference ruling, 2026-08-15).
      _Topology = door.HasValue ? door.Value.Topology : null;
      _Resolved = true;

      return _Topology;
    }

    public async ValueTask<TValue> GetValueAsync(THandle handle)
    {
      var topology = await ResolveAsync().ConfigureAwait(false);

      if (topology == null)
        throw new InvalidOperationException("The empty forest has no nodes; no handle can be valid here (the foreign-handle clause).");

      return await topology.GetValueAsync(handle).ConfigureAwait(false);
    }

    public async ValueTask<Option<THandle>> TryGetParentAsync(THandle handle)
    {
      var topology = await ResolveAsync().ConfigureAwait(false);

      return topology == null ? default : await topology.TryGetParentAsync(handle).ConfigureAwait(false);
    }

    public async ValueTask<Option<NodeAndSiblingIndex<THandle>>> TryGetChildAtAsync(THandle handle, int childIndex)
    {
      var topology = await ResolveAsync().ConfigureAwait(false);

      return topology == null ? default : await topology.TryGetChildAtAsync(handle, childIndex).ConfigureAwait(false);
    }

    public async ValueTask<Option<NodeAndSiblingIndex<THandle>>> TryGetRootAtAsync(int rootIndex)
    {
      var topology = await ResolveAsync().ConfigureAwait(false);

      return topology == null ? default : await topology.TryGetRootAtAsync(rootIndex).ConfigureAwait(false);
    }
  }
}
