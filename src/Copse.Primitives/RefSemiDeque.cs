using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Copse
{
  [DebuggerDisplay("Count = {Count}")]
  [DebuggerTypeProxy(typeof(RefSemiDeque<>.DebugView))]
  public class RefSemiDeque<T>
  {
    public RefSemiDeque() : this(8) { }

    public RefSemiDeque(int capacity)
    {
      Capacity = capacity;
      _Partitions = new LinkedList<T[]>();
      _Partitions.AddLast(new T[capacity]);
      CurrentPartition = _Partitions.First;
    }

    public int Capacity { get; private set; }
    public int Count { get; private set; }

    private LinkedList<T[]> _Partitions;
    private LinkedListNode<T[]> CurrentPartition { get; set; }
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

    public ref T GetFirst()
    {
      if (Count == 0)
        throw new InvalidOperationException("The stack is empty.");

      return ref _Partitions.First.Value[_TailPointerOffset];
    }

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

    public void AddLast(T item)
    {
      if (CurrentPartition.Value.Length == _HeadPointerOffset)
        AddPartitionOrMoveToNextPartition();

      CurrentPartition.Value[_HeadPointerOffset] = item;
      _HeadPointerOffset++;
      Count++;
    }

    public ref T RemoveLast()
    {
      if (Count == 0)
        throw new InvalidOperationException("The stack is empty.");

      Count--;
      _HeadPointerOffset--;

      ref var item = ref CurrentPartition.Value[_HeadPointerOffset];

      if (Count == 0)
      {
        CurrentPartition = _Partitions.First;
        _HeadPointerOffset = 0;
        _TailPointerOffset = 0;
      }
      else if (_HeadPointerOffset == 0)
      {
        CurrentPartition = CurrentPartition.Previous;
        _HeadPointerOffset = CurrentPartition.Value.Length;
      }

      return ref item;
    }

    public ref T GetLast()
    {
      if (Count == 0)
        throw new InvalidOperationException("The stack is empty.");

      return ref CurrentPartition.Value[_HeadPointerOffset - 1];
    }

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
        partition = CurrentPartition.Value;
        offset = _HeadPointerOffset - 1 - index;
        return;
      }

      index -= _HeadPointerOffset;

      var node = CurrentPartition.Previous;

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
      if (CurrentPartition == _Partitions.Last)
      {
        var newPartitionSize = Math.Min(Capacity, MaxPartitionSize);
        var newPartition = new T[newPartitionSize];
        _Partitions.AddLast(newPartition);
        Capacity += newPartition.Length;
      }

      CurrentPartition = CurrentPartition.Next;
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

      if (CurrentPartition == _Partitions.First)
      {
        for (var offset = _TailPointerOffset; offset < _HeadPointerOffset; offset++)
          result[index++] = CurrentPartition.Value[offset];

        return result;
      }

      for (var offset = _TailPointerOffset; offset < _Partitions.First.Value.Length; offset++)
        result[index++] = _Partitions.First.Value[offset];

      var node = _Partitions.First.Next;

      while (node != CurrentPartition)
      {
        for (var offset = 0; offset < node.Value.Length; offset++)
          result[index++] = node.Value[offset];

        node = node.Next;
      }

      for (var offset = 0; offset < _HeadPointerOffset; offset++)
        result[index++] = CurrentPartition.Value[offset];

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
