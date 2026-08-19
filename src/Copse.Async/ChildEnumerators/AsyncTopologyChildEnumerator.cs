using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Async.ChildEnumerators
{
  // The frame Tree.FromTopology hands the engine: one handle, one advancing child index --
  // the topology's indexed probe is the pull, and the label resolves during it (the
  // engine's node is the (handle, value) pair; its map arrow is the synchronous .Value
  // read). The pull method is NOT async, so the index mutation lands on the real struct
  // (the engine's path state holds frames by ref); the awaited tail reads only readonly
  // fields from its state-machine copy.
  internal struct AsyncTopologyChildEnumerator<TValue, THandle> : IAsyncChildEnumerator<HandleAndValue<THandle, TValue>>
  {
    public AsyncTopologyChildEnumerator(
      IAsyncTreeTopology<TValue, THandle> topology,
      THandle parentHandle)
    {
      _Topology = topology;
      _ParentHandle = parentHandle;
      _NextChildIndex = 0;
    }

    private readonly IAsyncTreeTopology<TValue, THandle> _Topology;
    private readonly THandle _ParentHandle;
    private int _NextChildIndex;

    public ValueTask<Option<NodeAndSiblingIndex<HandleAndValue<THandle, TValue>>>> MoveNextAsync()
    {
      var childIndex = _NextChildIndex;
      _NextChildIndex++;
      return PullAsync(childIndex);
    }

    private async ValueTask<Option<NodeAndSiblingIndex<HandleAndValue<THandle, TValue>>>> PullAsync(int childIndex)
    {
      var childResult = await _Topology.TryGetChildAtAsync(_ParentHandle, childIndex).ConfigureAwait(false);

      if (!childResult.HasValue)
        return default;

      var value = await _Topology.GetValueAsync(childResult.Value.Node).ConfigureAwait(false);

      return new Option<NodeAndSiblingIndex<HandleAndValue<THandle, TValue>>>(
        new NodeAndSiblingIndex<HandleAndValue<THandle, TValue>>(
          new HandleAndValue<THandle, TValue>(childResult.Value.Node, value),
          childResult.Value.SiblingIndex));
    }

    public void Dispose()
    {
    }

    // codegen: begin async-only
    public ValueTask DisposeAsync() => default;
    // codegen: end async-only
  }
}
