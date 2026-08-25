using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Async.ChildEnumerators
{
  // The frame Tree.FromTopology hands the engine: one handle, one advancing child index --
  // the topology's indexed probe is the pull, and the label resolves during it (the
  // engine's handle is the enriched (handle, value) pair, because the sync map arrow
  // cannot await a value resolution; the map is the .Value read). The pull method is NOT
  // async, so the index mutation lands on the real struct
  // (the engine's path state holds frames by ref); the awaited tail reads only readonly
  // fields from its state-machine copy.
  internal struct AsyncTopologyChildEnumerator<TNode, THandle> : IAsyncChildEnumerator<HandleAndValue<THandle, TNode>>
  {
    public AsyncTopologyChildEnumerator(
      IAsyncTreeTopology<TNode, THandle> topology,
      THandle parentHandle)
    {
      _Topology = topology;
      _ParentHandle = parentHandle;
      _NextChildIndex = 0;
    }

    private readonly IAsyncTreeTopology<TNode, THandle> _Topology;
    private readonly THandle _ParentHandle;
    private int _NextChildIndex;

    public ValueTask<Option<NodeAndSiblingIndex<HandleAndValue<THandle, TNode>>>> MoveNextAsync()
    {
      var childIndex = _NextChildIndex;
      _NextChildIndex++;
      return PullAsync(childIndex);
    }

    private async ValueTask<Option<NodeAndSiblingIndex<HandleAndValue<THandle, TNode>>>> PullAsync(int childIndex)
    {
      var childResult = await _Topology.TryGetChildAtAsync(_ParentHandle, childIndex).ConfigureAwait(false);

      if (!childResult.HasValue)
        return default;

      var value = await _Topology.GetValueAsync(childResult.Value.Node).ConfigureAwait(false);

      return new Option<NodeAndSiblingIndex<HandleAndValue<THandle, TNode>>>(
        new NodeAndSiblingIndex<HandleAndValue<THandle, TNode>>(
          new HandleAndValue<THandle, TNode>(childResult.Value.Node, value),
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
