using Copse;
using Copse.Core;
using System;

namespace Copse.Linq
{
  // A no-copy, no-allocation view over a surveyed node's children as write-handles, handed to a
  // RootfixDispatch survey. Backed by the build's child-index (pass 1½ gathers each parent's
  // children's preorder indices into one contiguous slice -- CSR over the preorder encoding),
  // so Count and the indexer are honestly O(1) and enumeration is a flat slice walk. [2026-08-02:
  // the index replaced subtree-span hopping, under which an indexer could only be O(k) -- the
  // dishonest-complexity shape -- and indexing scenarios paid a ToArray per survey.]
  //
  // Deliberately NOT IEnumerable<T> / IReadOnlyList<T>: foreach binds to the public struct
  // Enumerator by pattern (zero allocation), while every interface path would box the view AND
  // its enumerator on every survey -- the silent per-survey allocation this library exists to
  // avoid. ToArray() is the explicit bridge to interface-shaped APIs (LINQ, IEnumerable
  // parameters): the same order of cost any interface path pays, made visible at the call site.
  public readonly struct DispatchTargets<TSource, TDispatch>
  {
    internal DispatchTargets(
      TSource[] values,
      int[] childIndices,
      int[] childOffsets,
      TDispatch[] arrivals,
      bool[] written,
      int parentIndex,
      int childDepth)
    {
      _Values = values;
      _ChildIndices = childIndices;
      _ChildOffsets = childOffsets;
      _Arrivals = arrivals;
      _Written = written;
      _ParentIndex = parentIndex;
      _ChildDepth = childDepth;
    }

    private readonly TSource[] _Values;
    private readonly int[] _ChildIndices;
    private readonly int[] _ChildOffsets;
    private readonly TDispatch[] _Arrivals;
    private readonly bool[] _Written;
    private readonly int _ParentIndex;
    private readonly int _ChildDepth;

    /// <summary>The number of children. O(1) -- read off the child-index offsets.</summary>
    public int Count => _ChildOffsets[_ParentIndex + 1] - _ChildOffsets[_ParentIndex];

    /// <summary>
    /// The child's write-handle by sibling index. O(1). Handles fetched repeatedly share the
    /// build's backing state, so exactly-once dispatch holds across every copy.
    /// </summary>
    public DispatchTarget<TSource, TDispatch> this[int index]
    {
      get
      {
        if ((uint)index >= (uint)Count)
          throw new ArgumentOutOfRangeException(
            nameof(index), $"Sibling index {index} is outside the surveyed node's {Count} children.");

        var childIndex = _ChildIndices[_ChildOffsets[_ParentIndex] + index];

        // POSITIONS ARE DERIVED, NOT STORED (2026-08-05, the perf re-baseline's verdict): a
        // child's sibling index IS its offset in the parent's span (the child-index preserves
        // sibling order by construction), and its depth is the pass's walk depth plus one --
        // so the build allocates no positions array.
        return new DispatchTarget<TSource, TDispatch>(
          new NodeContext<TSource>(_Values[childIndex], new NodePosition(index, _ChildDepth)), _Arrivals, _Written, childIndex);
      }
    }

    /// <summary>
    /// The explicit bridge to interface-shaped APIs (LINQ, <c>IEnumerable</c> parameters):
    /// materializes the write-handles into an array -- ONE allocation, visible at the call
    /// site; the foreach and indexer paths stay allocation-free. The handles share the
    /// build's backing state, so exactly-once dispatch holds across the copies.
    /// </summary>
    public DispatchTarget<TSource, TDispatch>[] ToArray()
    {
      var targets = new DispatchTarget<TSource, TDispatch>[Count];

      for (var index = 0; index < targets.Length; index++)
        targets[index] = this[index];

      return targets;
    }

    public Enumerator GetEnumerator() => new Enumerator(this);

    public struct Enumerator
    {
      internal Enumerator(in DispatchTargets<TSource, TDispatch> targets)
      {
        _Targets = targets;
        _Index = -1;
      }

      private readonly DispatchTargets<TSource, TDispatch> _Targets;
      private int _Index;

      public DispatchTarget<TSource, TDispatch> Current => _Targets[_Index];

      public bool MoveNext() => ++_Index < _Targets.Count;
    }
  }
}
