using Copse.Async;
using Copse.Async.Treenumerables;
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
  internal sealed class AsyncExtendWalkable<TNode, THandle, TResult> : IAsyncWalkableTreenumerable<TResult, THandle>, IAsyncTreeTopology<TResult, THandle>
  {
    public AsyncExtendWalkable(
      IAsyncTreeTopology<TNode, THandle> source,
      Func<IAsyncTreeTopology<TNode, THandle>, THandle, ValueTask<TResult>> observer)
    {
      _Source = source;
      _Observer = observer;
      // Self-feed: this view IS a topology whose GetValue is the observation, so walking
      // itself streams the relabeling -- the labeling arrow Tree.FromTopology resolves
      // during each pull is exactly the observer (the reason no labeled overload exists).
      _Walk = AsyncTree.FromTopology(this);
    }

    private readonly IAsyncTreeTopology<TNode, THandle> _Source;
    private readonly Func<IAsyncTreeTopology<TNode, THandle>, THandle, ValueTask<TResult>> _Observer;
    private readonly IAsyncTreenumerable<TResult> _Walk;

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() => _Walk.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() => _Walk.GetAsyncBreadthFirstTreenumerator();

    public ValueTask<TResult> GetValueAsync(THandle handle) => _Observer(_Source, handle);

    public ValueTask<Option<THandle>> TryGetParentAsync(THandle handle) => _Source.TryGetParentAsync(handle);

    public ValueTask<Option<NodeAndSiblingIndex<THandle>>> TryGetChildAtAsync(THandle handle, int childIndex) => _Source.TryGetChildAtAsync(handle, childIndex);

    public ValueTask<Option<NodeAndSiblingIndex<THandle>>> TryGetRootAtAsync(int rootIndex) => _Source.TryGetRootAtAsync(rootIndex);

    // The door (walker factory design, Stage A): the relabeled view is its own topology.
    public ValueTask<AsyncTreeWalker<TResult, THandle>> GetTreeWalkerAsync()
      => new ValueTask<AsyncTreeWalker<TResult, THandle>>(new AsyncTreeWalker<TResult, THandle>(this));
  }
}
