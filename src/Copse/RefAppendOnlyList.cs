using System;
using System.Diagnostics;
#if NET8_0_OR_GREATER
using System.Numerics;
#endif

namespace Copse
{
  // A chunked, append-only list with ref-returning indexed access. "Append-only" is structural,
  // not semantic: slots never move (partitions are fixed-length arrays that are never resized,
  // relocated, or copied, unlike List<T>'s doubling), but contents stay mutable in place through
  // the ref indexer -- the memo builders backfill subtree sizes and child spans into
  // already-appended slots. That property split is what separates this type from its ancestor,
  // Aocl (github.com/jasonmcboyd/Aocl, MIT): Aocl's lock-free reads rest on "a published element
  // never changes," which a ref indexer would break, so this type keeps Aocl's layout math and
  // deliberately drops its synchronization. Single-threaded by contract.
  //
  // Partition sizes double without a cap -- 2^b, 2^b, 2^(b+1), ..., 2^30; 31 partitions cover
  // int.MaxValue elements -- so cumulative capacities are exact powers of two and
  // index -> (partition, offset) is one integer log2. Contrast RefSemiDeque's MaxPartitionSize:
  // that type backs churning traversal paths whose peak is transient, so it caps partitions to
  // bound LOH exposure; this type's buffers grow monotonically and live as long as their owner,
  // so a few large long-lived blocks are the cheap outcome and O(1) index math is worth the
  // uncapped doubling.
  [DebuggerDisplay("Count = {Count}")]
  [DebuggerTypeProxy(typeof(RefAppendOnlyList<>.DebugView))]
  public class RefAppendOnlyList<T>
  {
    public RefAppendOnlyList() : this(4) { }

    // Initial capacity is 2^bitness elements.
    public RefAppendOnlyList(int bitness)
    {
      if (bitness < 1 || bitness > 30)
        throw new ArgumentOutOfRangeException(nameof(bitness), "Must be between 1 and 30 inclusive.");

      _Bitness = bitness;
      _NextPartitionBitness = bitness;
      _Partitions = new T[MaxPartitionCount][];
      _Partitions[0] = new T[1 << bitness];
      _PartitionCount = 1;
    }

    // Worst case is bitness 1: partition sizes 2, 2, 4, 8, ... reach a combined capacity of 2^31
    // at the 31st partition -- enough for int.MaxValue elements. Larger bitnesses need fewer.
    private const int MaxPartitionCount = 31;

    private readonly T[][] _Partitions;
    private readonly int _Bitness;
    private int _NextPartitionBitness;
    private int _PartitionCount;
    private int _WriteOffset;

    public int Count { get; private set; }

    public void AddLast(T item)
    {
      var current = _Partitions[_PartitionCount - 1];

      if (_WriteOffset == current.Length)
        current = AddPartition();

      current[_WriteOffset] = item;
      _WriteOffset++;
      Count++;
    }

    public ref T this[int index]
    {
      get
      {
        if (index < 0 || index >= Count)
          throw new IndexOutOfRangeException();

        if (index < _Partitions[0].Length)
          return ref _Partitions[0][index];

        // Cumulative partition capacities are exact powers of two, so the most significant bit
        // of the index names its partition and the remainder below it is the offset.
        var mostSignificantBitIndex = Log2(index);

        return ref _Partitions[mostSignificantBitIndex - (_Bitness - 1)][index - (1 << mostSignificantBitIndex)];
      }
    }

    private T[] AddPartition()
    {
      var partition = new T[1 << _NextPartitionBitness];
      _Partitions[_PartitionCount] = partition;
      _PartitionCount++;
      _NextPartitionBitness++;
      _WriteOffset = 0;
      return partition;
    }

#if NET8_0_OR_GREATER
    private static int Log2(int value) => BitOperations.Log2((uint)value);
#else
    // De Bruijn floor-log2 (Sean Eron Anderson, "Bit Twiddling Hacks"); net48 has no
    // BitOperations.Log2. The table and the 0x07C4ACDD multiplier are a matched set and must
    // not be changed independently. Defined for positive inputs only, which the indexer's
    // bounds check guarantees.
    private static readonly int[] DeBruijnLog2 =
    {
       0,  9,  1, 10, 13, 21,  2, 29, 11, 14, 16, 18, 22, 25,  3, 30,
       8, 12, 20, 28, 15, 17, 24,  7, 19, 27, 23,  6, 26,  5,  4, 31
    };

    private static int Log2(int value)
    {
      var v = (uint)value;

      v |= v >> 1;
      v |= v >> 2;
      v |= v >> 4;
      v |= v >> 8;
      v |= v >> 16;

      return DeBruijnLog2[(v * 0x07C4ACDDu) >> 27];
    }
#endif

    // Point-in-time by-value copy, front-to-back; for tests/debugging, not the hot path
    // (same contract and rationale as RefSemiDeque.Snapshot).
    internal T[] Snapshot()
    {
      var result = new T[Count];

      for (var i = 0; i < Count; i++)
        result[i] = this[i];

      return result;
    }

    private sealed class DebugView
    {
      private readonly RefAppendOnlyList<T> _List;

      public DebugView(RefAppendOnlyList<T> list) => _List = list;

      [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
      public T[] Items => _List.Snapshot();
    }
  }
}
