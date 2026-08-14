using Copse;
using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // The restriction LENS's first citizen: PruneAfter over a walkable, as a PAIR -- the ORDER
  // half is the shipped streaming operator, delegated wholesale (the composition lattice inside
  // it keeps collapsing what it always collapsed, unaware walkables exist), and the ADJACENCY
  // half is one wrapped probe: a pruned-after node hands out no children. GetParent, GetValue,
  // and GetRootAt delegate untouched -- prune-after keeps the matched handle and its ancestry,
  // and roots always survive. Lenses compose by stacking (no pairwise lens types, no lattice:
  // adjacency probes are neighborhood-priced, so there is nothing to collapse).
  //
  // Handle stance (lens semantics): the lens restricts what it HANDS OUT, not what arithmetic
  // can name -- a guessed handle below a pruned boundary still answers with the source's
  // adjacency. Handles obtained from THIS walkable's probes never cross the boundary.
  internal sealed class PruneAfterWalkable<TValue, THandle> : IWalkableTreenumerable<TValue, THandle>
  {
    public PruneAfterWalkable(
      IWalkableTreenumerable<TValue, THandle> source,
      Func<TValue, bool> predicate)
    {
      _Source = source;
      _Predicate = predicate;
      // Via the streaming EXTENSION, not a direct treenumerable construction, so the
      // composition lattice inside PruneAfter keeps collapsing what it always collapsed.
      // The upcast picks the streaming overload deliberately -- on the walkable receiver
      // this constructor's own caller would win betterness and recurse.
      _PrunedStream = ((ITreenumerable<TValue>)source).PruneAfter(predicate);
    }

    private readonly IWalkableTreenumerable<TValue, THandle> _Source;
    private readonly Func<TValue, bool> _Predicate;
    private readonly ITreenumerable<TValue> _PrunedStream;

    public ITreenumerator<TValue> GetDepthFirstTreenumerator() => _PrunedStream.GetDepthFirstTreenumerator();

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator() => _PrunedStream.GetBreadthFirstTreenumerator();

    public TValue GetValue(THandle handle) => _Source.GetValue(handle);

    public ParentResult<THandle> GetParent(THandle handle) => _Source.GetParent(handle);

    public ChildResult<THandle> GetChildAt(THandle handle, int childIndex)
      => _Predicate(_Source.GetValue(handle))
        ? default
        : _Source.GetChildAt(handle, childIndex);

    public ChildResult<THandle> GetRootAt(int rootIndex) => _Source.GetRootAt(rootIndex);
  }
}
