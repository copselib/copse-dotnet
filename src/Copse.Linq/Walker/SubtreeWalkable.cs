using Copse;
using Copse.Core;
using Copse.Linq.Walker;
using System.Collections.Generic;

namespace Copse.Linq.Treenumerables
{
  // The re-rooted view -- the region floor's first lens landed, as a label type: the source
  // seen with one node as the sole root, upward sight severed AT that node and nowhere else.
  // Nothing is copied and nothing re-addressed -- handles are the source's own, descendants
  // answer every probe by delegation, and exactly two answers are rewritten: the root's
  // GetParent says "none", and the virtual forest-root's child group is the single root.
  // The streaming half is the Walk adapter driving THIS view's adjacency, so positions come
  // out re-rooted for free (depth 0, sibling 0 at the root -- the engine computes them from
  // the walk, no arithmetic here).
  //
  // The root comparison is HANDLE equality, never value equality: positional identity on the
  // provider's own terms (the contract's clause -- ordinals by index, references by
  // identity), so the no-node-equality pledge is untouched. Handles from outside the subtree
  // are not reachable from this view's root; probing with one is answered by blind
  // delegation, unspecified like any foreign-handle probe.
  internal sealed class SubtreeWalkable<TValue, THandle> : IWalkableTreenumerable<TValue, THandle>
  {
    public SubtreeWalkable(IWalkableTreenumerable<TValue, THandle> source, THandle root)
    {
      _Source = source;
      _Root = root;
      _Walk = WalkerWalk.Create(this);
    }

    private readonly IWalkableTreenumerable<TValue, THandle> _Source;
    private readonly THandle _Root;
    private readonly ITreenumerable<TValue> _Walk;

    public ITreenumerator<TValue> GetDepthFirstTreenumerator() => _Walk.GetDepthFirstTreenumerator();

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator() => _Walk.GetBreadthFirstTreenumerator();

    public TValue GetValue(THandle handle) => _Source.GetValue(handle);

    public ParentResult<THandle> GetParent(THandle handle)
      => EqualityComparer<THandle>.Default.Equals(handle, _Root)
        ? default
        : _Source.GetParent(handle);

    public ChildResult<THandle> GetChildAt(THandle handle, int childIndex) => _Source.GetChildAt(handle, childIndex);

    public ChildResult<THandle> GetRootAt(int rootIndex)
      => rootIndex == 0
        ? new ChildResult<THandle>(new NodeAndSiblingIndex<THandle>(_Root, 0))
        : default;
  }
}
