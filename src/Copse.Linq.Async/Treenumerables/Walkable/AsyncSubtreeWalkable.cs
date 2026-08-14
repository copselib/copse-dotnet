using Copse.Async;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The re-rooted view -- the region floor's first lens, and the label type of the cofree
  // duplicate (Subtrees): the source seen with one node as the sole root, upward sight severed
  // AT that node and nowhere else. Nothing is copied and nothing re-addressed -- handles are
  // the source's own, descendants answer every probe by delegation, and exactly two answers
  // are rewritten: the root's parent probe says "none", and the virtual forest-root's child
  // group is the single root. The streaming half is the Walk adapter driving THIS view's
  // adjacency, so positions come out re-rooted for free (depth 0, sibling 0 at the root --
  // the engine computes them from the walk, no arithmetic here).
  //
  // The root comparison is HANDLE equality, never value equality: positional identity on the
  // provider's own terms (the contract's clause), the identity axiom untouched. Handles from
  // outside the subtree are not reachable from this view's root; probing with one is answered
  // by blind delegation, unspecified like any foreign-handle probe.
  internal sealed class AsyncSubtreeWalkable<TValue, THandle> : IAsyncWalkableTreenumerable<TValue, THandle>
  {
    public AsyncSubtreeWalkable(IAsyncTreeTopology<TValue, THandle> source, THandle root)
    {
      _Source = source;
      _Root = root;
      _Walk = AsyncWalkerWalk.Create(this);
    }

    private readonly IAsyncTreeTopology<TValue, THandle> _Source;
    private readonly THandle _Root;
    private readonly IAsyncTreenumerable<TValue> _Walk;

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator() => _Walk.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator() => _Walk.GetAsyncBreadthFirstTreenumerator();

    public ValueTask<TValue> GetValueAsync(THandle handle) => _Source.GetValueAsync(handle);

    public ValueTask<ParentResult<THandle>> TryGetParentAsync(THandle handle)
      => EqualityComparer<THandle>.Default.Equals(handle, _Root)
        ? default
        : _Source.TryGetParentAsync(handle);

    public ValueTask<ChildResult<THandle>> TryGetChildAtAsync(THandle handle, int childIndex) => _Source.TryGetChildAtAsync(handle, childIndex);

    public ValueTask<ChildResult<THandle>> TryGetRootAtAsync(int rootIndex)
      => rootIndex == 0
        ? new ValueTask<ChildResult<THandle>>(new ChildResult<THandle>(new NodeAndSiblingIndex<THandle>(_Root, 0)))
        : default;

    // The door (walker factory design, Stage A): the severed view has exactly one root --
    // the walker stands there, never missing.
    public ValueTask<AsyncTreeWalkerResult<TValue, THandle>> TryGetTreeWalkerAsync()
      => new ValueTask<AsyncTreeWalkerResult<TValue, THandle>>(
        new AsyncTreeWalkerResult<TValue, THandle>(new AsyncTreeWalker<TValue, THandle>(this, _Root)));
  }
}
