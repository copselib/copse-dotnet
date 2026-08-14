using Copse.Async;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The comonad's defining operation, made concrete: a relabeling of the SAME shape where
  // every node's new value is an arbitrary observation of its focus -- the observer receives
  // the source walkable and the handle, so it can see anything reachable from that vantage
  // (depth, ancestors, subtree facts) that a streaming Select never could. Adjacency and
  // handles are delegated untouched (extend never reshapes -- the comonad covers relabelings
  // only); the streaming half is the Walk adapter driving the source's adjacency under the
  // observer's labeling. Laws pinned by the walker comonad law suites: extend(extract) is the
  // identity, extract after extend recovers the observer, and extend co-associates.
  internal sealed class AsyncExtendWalkable<TValue, THandle, TResult> : IAsyncWalkableTreenumerable<TResult, THandle>
  {
    public AsyncExtendWalkable(
      IAsyncTreeTerrain<TValue, THandle> source,
      Func<IAsyncTreeTerrain<TValue, THandle>, THandle, ValueTask<TResult>> observer)
    {
      _Source = source;
      _Observer = observer;
      _Walk = AsyncWalkerWalk.Create(source, observer);
    }

    private readonly IAsyncTreeTerrain<TValue, THandle> _Source;
    private readonly Func<IAsyncTreeTerrain<TValue, THandle>, THandle, ValueTask<TResult>> _Observer;
    private readonly IAsyncTreenumerable<TResult> _Walk;

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() => _Walk.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() => _Walk.GetAsyncBreadthFirstTreenumerator();

    public ValueTask<TResult> GetValueAsync(THandle handle) => _Observer(_Source, handle);

    public ValueTask<ParentResult<THandle>> TryGetParentAsync(THandle handle) => _Source.TryGetParentAsync(handle);

    public ValueTask<ChildResult<THandle>> TryGetChildAtAsync(THandle handle, int childIndex) => _Source.TryGetChildAtAsync(handle, childIndex);

    public ValueTask<ChildResult<THandle>> TryGetRootAtAsync(int rootIndex) => _Source.TryGetRootAtAsync(rootIndex);

    // The door (walker factory design, Stage A): the relabeled view is its own terrain.
    public async ValueTask<AsyncTreeWalkerResult<TResult, THandle>> TryGetTreeWalkerAsync()
    {
      var rootResult = await TryGetRootAtAsync(0).ConfigureAwait(false);

      return rootResult.HasChild
        ? new AsyncTreeWalkerResult<TResult, THandle>(new AsyncTreeWalker<TResult, THandle>(this, rootResult.Child.Node))
        : default;
    }
  }
}
