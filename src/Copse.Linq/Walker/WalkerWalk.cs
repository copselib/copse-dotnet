using Copse;
using Copse.Core;
using Copse.Treenumerables;
using System;
using System.Collections.Generic;

namespace Copse.Linq.Walker
{
  // The Walk() adapter -- the tower's named build dependency, and a thin composition over the
  // EXISTING hierarchical engine: a walkable's indexed child probe IS a child pull
  // (GetChildAt(parent, k) returns the engine's ChildResult directly), so driving the engine
  // from adjacency needs only this cursor struct, a roots iterator, and a labeling. Serves
  // every view that has no streaming-operator twin to delegate to (Extend below; the future
  // region lenses), and its conformance is pinned by the comonad law suite: walking a store
  // walkable through the adapter reproduces the store's native visit streams (the
  // degenerate-tower pin, engine-conformant by construction).
  internal static class WalkerWalk
  {
    // A walk over the walkable's own labeling: the identity relabeling. (Callers holding a
    // walkable rarely need this -- the walkable IS a treenumerable -- but views built from
    // adjacency alone get their streaming half here.)
    internal static ITreenumerable<TValue> Create<TValue, THandle>(IWalkableTreenumerable<TValue, THandle> walkable)
      => Create(walkable, walkable.GetValue);

    // A walk under a DIFFERENT labeling of the same shape -- Extend's streaming half: the
    // engine drives the walkable's adjacency, and every emitted handle is labeled through
    // the given function.
    internal static ITreenumerable<TResult> Create<TValue, THandle, TResult>(
      IWalkableTreenumerable<TValue, THandle> walkable,
      Func<THandle, TResult> labeling)
      => new Treenumerable<TResult, THandle, WalkableChildEnumerator<TValue, THandle>>(
        context => new WalkableChildEnumerator<TValue, THandle>(walkable, context.Node),
        labeling,
        Roots(walkable));

    private static IEnumerable<THandle> Roots<TValue, THandle>(IWalkableTreenumerable<TValue, THandle> walkable)
    {
      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootResult = walkable.GetRootAt(rootIndex);

        if (!rootResult.HasChild)
          yield break;

        yield return rootResult.Child.Node;
      }
    }
  }

  // The cursor the engine drives: one handle, one advancing child index -- the indexed probe
  // is the pull, so MoveNext is a single delegation. By-value result, no allocation, nothing
  // held between pulls.
  internal struct WalkableChildEnumerator<TValue, THandle> : IChildEnumerator<THandle>
  {
    public WalkableChildEnumerator(IWalkableTreenumerable<TValue, THandle> walkable, THandle parentHandle)
    {
      _Walkable = walkable;
      _ParentHandle = parentHandle;
      _NextChildIndex = 0;
    }

    private readonly IWalkableTreenumerable<TValue, THandle> _Walkable;
    private readonly THandle _ParentHandle;
    private int _NextChildIndex;

    public ChildResult<THandle> MoveNext()
    {
      var childResult = _Walkable.GetChildAt(_ParentHandle, _NextChildIndex);
      _NextChildIndex++;
      return childResult;
    }

    public void Dispose()
    {
    }
  }
}
