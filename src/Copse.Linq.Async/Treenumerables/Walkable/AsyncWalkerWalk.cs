using Copse.Async;
using Copse.Async.Treenumerables;
using Copse.Core.Async;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The Walk() adapter -- the tower's named build dependency, and a thin composition over the
  // EXISTING hierarchical engine: a walkable's indexed child probe IS a child pull, so driving
  // the engine from adjacency needs only this frame struct, a roots iterator, and a labeling.
  // Serves every view that has no streaming-operator twin to delegate to (Extend, the severed
  // subtree view; the future region lenses), and its conformance is pinned by the comonad law
  // suite: walking a store-backed walkable through the adapter reproduces the store's native
  // visit streams (the degenerate-tower pin, engine-conformant by construction).
  //
  // The labeling arrow is a probe, so labels are resolved DURING the pull: the engine's node
  // type is the (handle, value) pair, filled in as each child is pulled, and the engine's own
  // node-to-value map is the synchronous .Value read. (The engine's map arrow is synchronous
  // by design; resolving at pull time is what lets an arbitrary observer label it.)
  internal static class AsyncWalkerWalk
  {
    // A walk over the walkable's own labeling: the identity relabeling. (Callers holding a
    // walkable rarely need this -- the walkable IS a treenumerable -- but views built from
    // adjacency alone get their streaming half here.)
    internal static IAsyncTreenumerable<TValue> Create<TValue, THandle>(IAsyncTreeTopology<TValue, THandle> walkable)
      => Create(walkable, (source, handle) => source.GetValueAsync(handle));

    // A walk under a DIFFERENT labeling of the same shape -- Extend's streaming half: the
    // engine drives the walkable's adjacency, and every emitted handle is labeled through
    // the given observer.
    internal static IAsyncTreenumerable<TResult> Create<TValue, THandle, TResult>(
      IAsyncTreeTopology<TValue, THandle> walkable,
      Func<IAsyncTreeTopology<TValue, THandle>, THandle, ValueTask<TResult>> labeling)
      => new AsyncTreenumerable<TResult, HandleAndValue<THandle, TResult>, AsyncWalkableChildEnumerator<TValue, THandle, TResult>>(
        context => new AsyncWalkableChildEnumerator<TValue, THandle, TResult>(walkable, labeling, context.Node.Handle),
        labeledNode => labeledNode.Value,
        Roots(walkable, labeling));

    private static async IAsyncEnumerable<HandleAndValue<THandle, TResult>> Roots<TValue, THandle, TResult>(
      IAsyncTreeTopology<TValue, THandle> walkable,
      Func<IAsyncTreeTopology<TValue, THandle>, THandle, ValueTask<TResult>> labeling)
    {
      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootResult = await walkable.TryGetRootAtAsync(rootIndex).ConfigureAwait(false);

        if (!rootResult.HasChild)
          yield break;

        var label = await labeling(walkable, rootResult.Child.Node).ConfigureAwait(false);

        yield return new HandleAndValue<THandle, TResult>(rootResult.Child.Node, label);
      }
    }
  }

  // The frame the engine drives: one handle, one advancing child index -- the indexed probe
  // is the pull. The pull method is NOT async, so the index mutation lands on the real struct
  // (the engine's path state holds frames by ref); the awaited tail reads only readonly
  // fields from its state-machine copy.
  internal struct AsyncWalkableChildEnumerator<TValue, THandle, TResult> : IAsyncChildEnumerator<HandleAndValue<THandle, TResult>>
  {
    public AsyncWalkableChildEnumerator(
      IAsyncTreeTopology<TValue, THandle> walkable,
      Func<IAsyncTreeTopology<TValue, THandle>, THandle, ValueTask<TResult>> labeling,
      THandle parentHandle)
    {
      _Walkable = walkable;
      _Labeling = labeling;
      _ParentHandle = parentHandle;
      _NextChildIndex = 0;
    }

    private readonly IAsyncTreeTopology<TValue, THandle> _Walkable;
    private readonly Func<IAsyncTreeTopology<TValue, THandle>, THandle, ValueTask<TResult>> _Labeling;
    private readonly THandle _ParentHandle;
    private int _NextChildIndex;

    public ValueTask<ChildResult<HandleAndValue<THandle, TResult>>> MoveNextAsync()
    {
      var childIndex = _NextChildIndex;
      _NextChildIndex++;
      return PullAsync(childIndex);
    }

    private async ValueTask<ChildResult<HandleAndValue<THandle, TResult>>> PullAsync(int childIndex)
    {
      var childResult = await _Walkable.TryGetChildAtAsync(_ParentHandle, childIndex).ConfigureAwait(false);

      if (!childResult.HasChild)
        return default;

      var label = await _Labeling(_Walkable, childResult.Child.Node).ConfigureAwait(false);

      return new ChildResult<HandleAndValue<THandle, TResult>>(
        new NodeAndSiblingIndex<HandleAndValue<THandle, TResult>>(
          new HandleAndValue<THandle, TResult>(childResult.Child.Node, label),
          childResult.Child.SiblingIndex));
    }

    public void Dispose()
    {
    }

    // codegen: begin async-only
    public ValueTask DisposeAsync() => default;
    // codegen: end async-only
  }
}
