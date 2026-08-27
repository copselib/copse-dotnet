using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Copse.Collections
{
  // ACCESS-COST CONTRACT: head/tail operations (AddLast/RemoveLast/RemoveFirst/GetFirst/GetLast)
  // and near-end indexing are O(1); GetFromBack/GetFromFront at an ARBITRARY index resolve their
  // partition by walking the partition chain -- O(partitions), a pointer chase per hop. Every
  // caller in the library indexes near an end or on a cold path. A workload that random-indexes
  // a large live range wants a different shape: the level-order stream decoder's window did
  // exactly that and profiled at 87% of its whole drain here before moving to a masked ring
  // (see LevelOrderStreamBreadthFirstTreenumerator).
  /// <summary>
  /// A double-ended collection over chunked arrays whose accessors return <c>ref T</c>, so
  /// elements are read and mutated in place rather than copied -- the state container the
  /// traversal machinery builds its stacks and queues on. Add at the back with
  /// <see cref="AddLast"/>; consume from either end (<see cref="RemoveLast"/> for stack use,
  /// <see cref="RemoveFirst"/> for queue use). Growth allocates additional fixed-size chunks;
  /// existing elements never move, so a returned ref stays valid until its element is removed
  /// or overwritten by later operations. Single-threaded.
  ///
  /// <para>Costs: the end operations (<see cref="AddLast"/>, <see cref="RemoveLast"/>,
  /// <see cref="RemoveFirst"/>, <see cref="GetFirst"/>, <see cref="GetLast"/>) and near-end
  /// indexing are O(1); <see cref="GetFromBack"/> at an arbitrary index walks the chunk chain.
  /// For workloads that random-index a large live range, prefer a flat structure.</para>
  /// </summary>
  [DebuggerDisplay("Count = {Count}")]
  [DebuggerTypeProxy(typeof(RefSemiDeque<>.DebugView))]
  public class RefSemiDeque<T>
  {
    /// <summary>Creates an empty deque with an initial chunk of 8 elements.</summary>
    public RefSemiDeque() : this(8) { }

    /// <summary>Creates an empty deque whose initial chunk holds
    /// <paramref name="capacity"/> elements.</summary>
    public RefSemiDeque(int capacity)
    {
      Capacity = capacity;
      _Partitions = new LinkedList<T[]>();
      _Partitions.AddLast(new T[capacity]);
      _CurrentPartitionNode = _Partitions.First;
      _CurrentPartition = _CurrentPartitionNode.Value;
    }

    /// <summary>The total number of element slots currently allocated.</summary>
    public int Capacity { get; private set; }

    /// <summary>The number of elements currently in the deque.</summary>
    public int Count { get; private set; }

    private LinkedList<T[]> _Partitions;
    private LinkedListNode<T[]> _CurrentPartitionNode;

    // The current (tail) partition's array, cached so the hot accessors pay a field read
    // instead of a LinkedListNode dereference per touch -- the same pattern as
    // RefAppendOnlyList's _WritePartition. Must be updated wherever _CurrentPartitionNode is.
    private T[] _CurrentPartition;

    private int _TailPointerOffset = 0;
    private int _HeadPointerOffset = 0;

    // Upper bound on individual partition size, in elements. Pure geometric growth makes the
    // largest partition ~half the deque's peak element count -- a multi-MB (potentially huge)
    // Large Object Heap allocation on very wide/deep trees, plus up to ~2x peak over-allocation
    // at the power-of-two boundary. Capping bounds both the largest partition and the overshoot.
    //
    // Deliberately a fixed element count rather than a byte budget that would force every
    // partition sub-LOH: large element types belong on the LOH (a few big long-lived blocks are
    // cheaper than many small ones churning through Gen0), so we bound the count and let the GC
    // place each partition by its actual size. At 4096 a partition spans 4096 * sizeof(T) bytes,
    // so small-element partitions (int, references) stay sub-LOH in Gen0 while only larger value
    // types reach the LOH -- and then only as a bounded handful of blocks.
    private const int MaxPartitionSize = 4096;

    /// <summary>A ref to the front element. O(1). Throws <see cref="InvalidOperationException"/>
    /// when empty.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetFirst()
    {
      if (Count == 0)
        throw new InvalidOperationException("The stack is empty.");

      return ref _Partitions.First.Value[_TailPointerOffset];
    }

    /// <summary>Removes the front element and returns a ref to the slot it occupied -- readable
    /// until a later operation overwrites it. O(1). Throws
    /// <see cref="InvalidOperationException"/> when empty.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T RemoveFirst()
    {
      if (Count == 0)
        throw new InvalidOperationException("The stack is empty.");

      ref var result = ref _Partitions.First.Value[_TailPointerOffset];

      Count--;
      _TailPointerOffset++;

      if (Count == 0)
      {
        _TailPointerOffset = 0;
        _HeadPointerOffset = 0;
      }
      else if (_TailPointerOffset == _Partitions.First.Value.Length)
      {
        var node = _Partitions.First;
        _Partitions.RemoveFirst();
        _Partitions.AddLast(node);
        _TailPointerOffset = 0;
      }

      return ref result;
    }

    /// <summary>Appends <paramref name="item"/> at the back. Amortized O(1); existing elements
    /// never move.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddLast(T item)
    {
      if (_CurrentPartition.Length == _HeadPointerOffset)
        AddPartitionOrMoveToNextPartition();

      _CurrentPartition[_HeadPointerOffset] = item;
      _HeadPointerOffset++;
      Count++;
    }

    /// <summary>Removes the back element and returns a ref to the slot it occupied -- readable
    /// until a later operation overwrites it. O(1). Throws
    /// <see cref="InvalidOperationException"/> when empty.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T RemoveLast()
    {
      if (Count == 0)
        throw new InvalidOperationException("The stack is empty.");

      Count--;
      _HeadPointerOffset--;

      ref var item = ref _CurrentPartition[_HeadPointerOffset];

      if (Count == 0)
      {
        _CurrentPartitionNode = _Partitions.First;
        _CurrentPartition = _CurrentPartitionNode.Value;
        _HeadPointerOffset = 0;
        _TailPointerOffset = 0;
      }
      else if (_HeadPointerOffset == 0)
      {
        _CurrentPartitionNode = _CurrentPartitionNode.Previous;
        _CurrentPartition = _CurrentPartitionNode.Value;
        _HeadPointerOffset = _CurrentPartition.Length;
      }

      return ref item;
    }

    /// <summary>A ref to the back element. O(1). Throws <see cref="InvalidOperationException"/>
    /// when empty.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetLast()
    {
      if (Count == 0)
        throw new InvalidOperationException("The stack is empty.");

      return ref _CurrentPartition[_HeadPointerOffset - 1];
    }

    /// <summary>A ref to the element <paramref name="index"/> positions from the back (0 is the
    /// back element). O(1) near the back; walks the chunk chain for distant indices. Throws
    /// <see cref="InvalidOperationException"/> when empty,
    /// <see cref="IndexOutOfRangeException"/> for an index outside [0, <see cref="Count"/>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetFromBack(int index)
    {
      if (Count == 0)
        throw new InvalidOperationException("The stack is empty.");

      if (index < 0 || index >= Count)
        throw new IndexOutOfRangeException();

      GetPartitionAndOffset(index, out var partition, out var offset);

      return ref partition[offset];
    }

    private void GetPartitionAndOffset(int index, out T[] partition, out int offset)
    {
      if (index < _HeadPointerOffset)
      {
        partition = _CurrentPartition;
        offset = _HeadPointerOffset - 1 - index;
        return;
      }

      index -= _HeadPointerOffset;

      var node = _CurrentPartitionNode.Previous;

      while (node.Value.Length <= index)
      {
        index -= node.Value.Length;
        node = node.Previous;
      }

      partition = node.Value;
      offset = node.Value.Length - 1 - index;
    }

    private void AddPartitionOrMoveToNextPartition()
    {
      if (_CurrentPartitionNode == _Partitions.Last)
      {
        var newPartitionSize = Math.Min(Capacity, MaxPartitionSize);
        var newPartition = new T[newPartitionSize];
        _Partitions.AddLast(newPartition);
        Capacity += newPartition.Length;
      }

      _CurrentPartitionNode = _CurrentPartitionNode.Next;
      _CurrentPartition = _CurrentPartitionNode.Value;
      _HeadPointerOffset = 0;
    }

    // Point-in-time copy of the deque's contents in front-to-back order. This type's accessors all
    // return `ref T` for in-place mutation; a snapshot is deliberately by-value and is NOT a live
    // view -- it exists for tests/debugging, not the traversal hot path. Intentionally not an
    // IEnumerable/LINQ surface: enumerating a ref-cell arena by value silently copies large structs
    // and offers no concurrent-mutation guard, both of which contradict the type's contract.
    internal T[] Snapshot()
    {
      var result = new T[Count];

      if (Count == 0)
        return result;

      var index = 0;

      if (_CurrentPartitionNode == _Partitions.First)
      {
        for (var offset = _TailPointerOffset; offset < _HeadPointerOffset; offset++)
          result[index++] = _CurrentPartition[offset];

        return result;
      }

      for (var offset = _TailPointerOffset; offset < _Partitions.First.Value.Length; offset++)
        result[index++] = _Partitions.First.Value[offset];

      var node = _Partitions.First.Next;

      while (node != _CurrentPartitionNode)
      {
        for (var offset = 0; offset < node.Value.Length; offset++)
          result[index++] = node.Value[offset];

        node = node.Next;
      }

      for (var offset = 0; offset < _HeadPointerOffset; offset++)
        result[index++] = _CurrentPartition[offset];

      return result;
    }

    private sealed class DebugView
    {
      private readonly RefSemiDeque<T> _Deque;

      public DebugView(RefSemiDeque<T> deque) => _Deque = deque;

      [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
      public T[] Items => _Deque.Snapshot();
    }
  }
}
