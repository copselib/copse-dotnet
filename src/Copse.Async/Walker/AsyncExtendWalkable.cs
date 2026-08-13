using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Async
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
      IAsyncWalkableTreenumerable<TValue, THandle> source,
      Func<IAsyncWalkableTreenumerable<TValue, THandle>, THandle, ValueTask<TResult>> observer)
    {
      _Source = source;
      _Observer = observer;
      _Walk = AsyncWalkerWalk.Create(source, observer);
    }

    private readonly IAsyncWalkableTreenumerable<TValue, THandle> _Source;
    private readonly Func<IAsyncWalkableTreenumerable<TValue, THandle>, THandle, ValueTask<TResult>> _Observer;
    private readonly IAsyncTreenumerable<TResult> _Walk;

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() => _Walk.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() => _Walk.GetAsyncBreadthFirstTreenumerator();

    public ValueTask<TResult> GetValueAsync(THandle handle) => _Observer(_Source, handle);

    public ValueTask<ParentResult<THandle>> GetParentAsync(THandle handle) => _Source.GetParentAsync(handle);

    public ValueTask<ChildResult<THandle>> GetChildAtAsync(THandle handle, int childIndex) => _Source.GetChildAtAsync(handle, childIndex);

    public ValueTask<ChildResult<THandle>> GetRootAtAsync(int rootIndex) => _Source.GetRootAtAsync(rootIndex);
  }
}
