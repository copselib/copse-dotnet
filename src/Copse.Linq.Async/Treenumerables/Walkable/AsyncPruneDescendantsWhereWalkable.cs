using Copse;
using Copse.Core;
using Copse.Topologies;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Treenumerables
{
  // The restriction LENS's first citizen: PruneDescendantsWhere over a walkable, as a PAIR -- the ORDER
  // half is the shipped streaming operator, delegated wholesale (the composition lattice inside
  // it keeps collapsing what it always collapsed, unaware walkables exist), and the ADJACENCY
  // half is one wrapped probe: a pruned-after node hands out no children. TryGetParent, GetNode,
  // and TryGetRootAt delegate untouched -- prune-after keeps the matched handle and its ancestry,
  // and roots always survive. Lenses compose by stacking (no pairwise lens types, no lattice:
  // adjacency probes are neighborhood-priced, so there is nothing to collapse).
  //
  // Handle stance (lens semantics): the lens restricts what it HANDS OUT, not what arithmetic
  // can name -- a guessed handle below a pruned boundary still answers with the source's
  // adjacency. Handles obtained from THIS walkable's probes never cross the boundary.
  internal sealed class AsyncPruneDescendantsWhereWalkable<TNode, THandle> : IAsyncWalkableTreenumerable<TNode, THandle>, IAsyncTreeTopology<TNode, THandle>
  {
    public AsyncPruneDescendantsWhereWalkable(
      IAsyncWalkableTreenumerable<TNode, THandle> source,
      Func<TNode, bool> predicate)
    {
      // The walkable does not expose its topology; navigation goes through the walker; the lens's adjacency half
      // reaches it through the deferred door (knocked once, at the first probe).
      _Source = AsyncTreeTopology.Lazy(source);
      _Predicate = predicate;
      // Via the streaming EXTENSION, not a direct treenumerable construction, so the
      // composition lattice inside PruneDescendantsWhere keeps collapsing what it always collapsed.
      // The upcast picks the streaming overload deliberately -- on the walkable receiver
      // this constructor's own caller would win betterness and recurse.
      _PrunedStream = ((IAsyncTreenumerable<TNode>)source).PruneDescendantsWhere(predicate);
    }

    private readonly IAsyncTreeTopology<TNode, THandle> _Source;
    private readonly Func<TNode, bool> _Predicate;
    private readonly IAsyncTreenumerable<TNode> _PrunedStream;

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() => _PrunedStream.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() => _PrunedStream.GetAsyncBreadthFirstTreenumerator();

    public ValueTask<TNode> GetNodeAsync(THandle handle) => _Source.GetNodeAsync(handle);

    public ValueTask<Option<THandle>> TryGetParentAsync(THandle handle) => _Source.TryGetParentAsync(handle);

    public async ValueTask<Option<HandleAndSiblingIndex<THandle>>> TryGetChildAtAsync(THandle handle, int childIndex)
      => _Predicate(await _Source.GetNodeAsync(handle).ConfigureAwait(false))
        ? default
        : await _Source.TryGetChildAtAsync(handle, childIndex).ConfigureAwait(false);

    public ValueTask<Option<HandleAndSiblingIndex<THandle>>> TryGetRootAtAsync(int rootIndex) => _Source.TryGetRootAtAsync(rootIndex);

    // The door: the lens IS its own topology -- the walker
    // navigates the pruned view.
    public ValueTask<AsyncTreeWalker<TNode, THandle>> GetTreeWalkerAsync()
      => new ValueTask<AsyncTreeWalker<TNode, THandle>>(new AsyncTreeWalker<TNode, THandle>(this));
  }
}
