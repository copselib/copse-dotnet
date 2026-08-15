using Copse.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The door's topology, deferred (Stage C): the walkable no longer exposes its topology,
  // so machinery that must build lazily over "whatever topology this walkable's door will
  // bind" holds THIS -- the door is knocked once, at the first probe, and the bound
  // topology answers everything after. The empty forest needs no special citizen: its
  // door misses, so the result-typed probes miss honestly (no roots, no parents, no
  // children) and the one probe that MUST produce a value (GetValue) throws -- on an empty
  // forest every handle is forged, so the ask is a violation, not a miss (the two-channel
  // doctrine).
  internal sealed class AsyncDoorTopology<TValue, THandle> : IAsyncTreeTopology<TValue, THandle>
  {
    public AsyncDoorTopology(IAsyncWalkableTreenumerable<TValue, THandle> source)
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

      _Topology = door.HasWalker ? door.Walker.Topology : null;
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

    public async ValueTask<ParentResult<THandle>> TryGetParentAsync(THandle handle)
    {
      var topology = await ResolveAsync().ConfigureAwait(false);

      return topology == null ? default : await topology.TryGetParentAsync(handle).ConfigureAwait(false);
    }

    public async ValueTask<ChildResult<THandle>> TryGetChildAtAsync(THandle handle, int childIndex)
    {
      var topology = await ResolveAsync().ConfigureAwait(false);

      return topology == null ? default : await topology.TryGetChildAtAsync(handle, childIndex).ConfigureAwait(false);
    }

    public async ValueTask<ChildResult<THandle>> TryGetRootAtAsync(int rootIndex)
    {
      var topology = await ResolveAsync().ConfigureAwait(false);

      return topology == null ? default : await topology.TryGetRootAtAsync(rootIndex).ConfigureAwait(false);
    }
  }
}
