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
  internal sealed class AsyncSubtreeWalkable<TNode, THandle> : IAsyncWalkableTreenumerable<TNode, THandle>, IAsyncTreeTopology<TNode, THandle>
  {
    public AsyncSubtreeWalkable(IAsyncTreeTopology<TNode, THandle> source, THandle root)
    {
      _Source = source;
      _Root = root;
      _Walk = AsyncTree.FromTopology(this);
    }

    private readonly IAsyncTreeTopology<TNode, THandle> _Source;
    private readonly THandle _Root;
    private readonly IAsyncTreenumerable<TNode> _Walk;

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() => _Walk.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() => _Walk.GetAsyncBreadthFirstTreenumerator();

    public ValueTask<TNode> GetValueAsync(THandle handle) => _Source.GetValueAsync(handle);

    public ValueTask<Option<THandle>> TryGetParentAsync(THandle handle)
      => EqualityComparer<THandle>.Default.Equals(handle, _Root)
        ? default
        : _Source.TryGetParentAsync(handle);

    public ValueTask<Option<HandleAndSiblingIndex<THandle>>> TryGetChildAtAsync(THandle handle, int childIndex) => _Source.TryGetChildAtAsync(handle, childIndex);

    public ValueTask<Option<HandleAndSiblingIndex<THandle>>> TryGetRootAtAsync(int rootIndex)
      => rootIndex == 0
        ? new ValueTask<Option<HandleAndSiblingIndex<THandle>>>(new Option<HandleAndSiblingIndex<THandle>>(new HandleAndSiblingIndex<THandle>(_Root, 0)))
        : default;

    // The door: this view's OWN unfocused stance -- above the severed root, where the severing
    // put the top of the world. The single root is its child group.
    public ValueTask<AsyncTreeWalker<TNode, THandle>> GetTreeWalkerAsync()
      => new ValueTask<AsyncTreeWalker<TNode, THandle>>(new AsyncTreeWalker<TNode, THandle>(this));
  }
}
