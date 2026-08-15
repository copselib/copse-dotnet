using Copse.Async;
using Copse.Core.Async;
using Copse.Linq.Async.Topologies;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The restriction LENS's first citizen: PruneAfter over a walkable, as a PAIR -- the ORDER
  // half is the shipped streaming operator, delegated wholesale (the composition lattice inside
  // it keeps collapsing what it always collapsed, unaware walkables exist), and the ADJACENCY
  // half is one wrapped probe: a pruned-after node hands out no children. TryGetParent, GetValue,
  // and TryGetRootAt delegate untouched -- prune-after keeps the matched handle and its ancestry,
  // and roots always survive. Lenses compose by stacking (no pairwise lens types, no lattice:
  // adjacency probes are neighborhood-priced, so there is nothing to collapse).
  //
  // Handle stance (lens semantics): the lens restricts what it HANDS OUT, not what arithmetic
  // can name -- a guessed handle below a pruned boundary still answers with the source's
  // adjacency. Handles obtained from THIS walkable's probes never cross the boundary.
  internal sealed class AsyncPruneAfterWalkable<TValue, THandle> : IAsyncWalkableTreenumerable<TValue, THandle>, IAsyncTreeTopology<TValue, THandle>
  {
    public AsyncPruneAfterWalkable(
      IAsyncWalkableTreenumerable<TValue, THandle> source,
      Func<TValue, bool> predicate)
    {
      // Stage C: the walkable no longer exposes its topology; the lens's adjacency half
      // reaches it through the deferred door (knocked once, at the first probe).
      _Source = new AsyncWalkableTopology<TValue, THandle>(source);
      _Predicate = predicate;
      // Via the streaming EXTENSION, not a direct treenumerable construction, so the
      // composition lattice inside PruneAfter keeps collapsing what it always collapsed.
      // The upcast picks the streaming overload deliberately -- on the walkable receiver
      // this constructor's own caller would win betterness and recurse.
      _PrunedStream = ((IAsyncTreenumerable<TValue>)source).PruneAfter(predicate);
    }

    private readonly IAsyncTreeTopology<TValue, THandle> _Source;
    private readonly Func<TValue, bool> _Predicate;
    private readonly IAsyncTreenumerable<TValue> _PrunedStream;

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator() => _PrunedStream.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator() => _PrunedStream.GetAsyncBreadthFirstTreenumerator();

    public ValueTask<TValue> GetValueAsync(THandle handle) => _Source.GetValueAsync(handle);

    public ValueTask<ParentResult<THandle>> TryGetParentAsync(THandle handle) => _Source.TryGetParentAsync(handle);

    public async ValueTask<ChildResult<THandle>> TryGetChildAtAsync(THandle handle, int childIndex)
      => _Predicate(await _Source.GetValueAsync(handle).ConfigureAwait(false))
        ? default
        : await _Source.TryGetChildAtAsync(handle, childIndex).ConfigureAwait(false);

    public ValueTask<ChildResult<THandle>> TryGetRootAtAsync(int rootIndex) => _Source.TryGetRootAtAsync(rootIndex);

    // The door (walker factory design, Stage A): the lens IS its own topology -- the walker
    // navigates the pruned view.
    public async ValueTask<AsyncTreeWalkerResult<TValue, THandle>> TryGetTreeWalkerAsync()
    {
      var rootResult = await TryGetRootAtAsync(0).ConfigureAwait(false);

      return rootResult.HasChild
        ? new AsyncTreeWalkerResult<TValue, THandle>(new AsyncTreeWalker<TValue, THandle>(this, rootResult.Child.Node))
        : default;
    }
  }
}
