using Copse;
using Copse.Core;
using System;

namespace Copse.Linq
{
  // Backed by the build's child-index (pass 1½ gathers each parent's children's preorder
  // indices into one contiguous slice -- CSR over the preorder encoding). Deliberately NOT
  // IEnumerable<T> / IReadOnlyList<T>: foreach binds to the public struct Enumerator by
  // pattern (zero allocation), while every interface path would box the view AND its
  // enumerator on every survey.
  /// <summary>
  /// The view a <c>RootfixDispatch</c> survey receives: all of the surveyed node's children at
  /// once, each as a write-handle that must be dispatched to exactly once.
  /// <see cref="Count"/> and the indexer are O(1), and <c>foreach</c> over the view allocates
  /// nothing; <see cref="ToArray"/> is the bridge when an API needs an <c>IEnumerable</c>.
  /// </summary>
  public readonly struct DispatchTargets<TNode, TDispatch>
  {
    internal DispatchTargets(
      TNode[] nodes,
      int[] childIndices,
      int[] childOffsets,
      TDispatch[] arrivals,
      bool[] written,
      int parentIndex,
      int childDepth)
    {
      _Nodes = nodes;
      _ChildIndices = childIndices;
      _ChildOffsets = childOffsets;
      _Arrivals = arrivals;
      _Written = written;
      _ParentIndex = parentIndex;
      _ChildDepth = childDepth;
    }

    private readonly TNode[] _Nodes;
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
    public DispatchTarget<TNode, TDispatch> this[int index]
    {
      get
      {
        if ((uint)index >= (uint)Count)
          throw new ArgumentOutOfRangeException(
            nameof(index), $"Sibling index {index} is outside the surveyed node's {Count} children.");

        var childIndex = _ChildIndices[_ChildOffsets[_ParentIndex] + index];

        // Positions are derived, not stored: a
        // child's sibling index IS its offset in the parent's span (the child-index preserves
        // sibling order by construction), and its depth is the pass's walk depth plus one --
        // so the build allocates no positions array.
        return new DispatchTarget<TNode, TDispatch>(
          new NodeContext<TNode>(_Nodes[childIndex], new NodePosition(index, _ChildDepth)), _Arrivals, _Written, childIndex);
      }
    }

    /// <summary>
    /// The explicit bridge to interface-shaped APIs (LINQ, <c>IEnumerable</c> parameters):
    /// materializes the write-handles into an array -- ONE allocation, visible at the call
    /// site; the foreach and indexer paths stay allocation-free. The handles share the
    /// build's backing state, so exactly-once dispatch holds across the copies.
    /// </summary>
    public DispatchTarget<TNode, TDispatch>[] ToArray()
    {
      var targets = new DispatchTarget<TNode, TDispatch>[Count];

      for (var index = 0; index < targets.Length; index++)
        targets[index] = this[index];

      return targets;
    }

    /// <summary>The allocation-free enumerator <c>foreach</c> binds to.</summary>
    public Enumerator GetEnumerator() => new Enumerator(this);

    /// <summary>Enumerates the write-handles in sibling order without allocating.</summary>
    public struct Enumerator
    {
      internal Enumerator(in DispatchTargets<TNode, TDispatch> targets)
      {
        _Targets = targets;
        _Index = -1;
      }

      private readonly DispatchTargets<TNode, TDispatch> _Targets;
      private int _Index;

      /// <summary>The write-handle at the current sibling index.</summary>
      public DispatchTarget<TNode, TDispatch> Current => _Targets[_Index];

      /// <summary>Advances to the next child; false past the last.</summary>
      public bool MoveNext() => ++_Index < _Targets.Count;
    }
  }
}
