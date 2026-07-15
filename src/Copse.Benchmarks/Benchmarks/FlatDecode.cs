using Copse.Stores;
using BenchmarkDotNet.Attributes;
using Copse;
using Copse.Core;
using Copse.Treenumerators;
using System.Collections.Generic;

namespace Copse.Benchmarks
{
  // The flat family's DECODERS, measured bare: the same canonical encodings (built once in
  // setup) synthesized back into visit streams by the four store decoders (each over flat
  // arrays AND the chunked D4c candidate backing) and the two forward-only stream decoders.
  // These primitives sit under every memo replay, buffer drain, and deserialize, but until this
  // family their cost only appeared blended into operator rows -- a 6x stream-vs-store decoder
  // gap hid inside Invert/Memoize for a full release cycle.
  //
  // Two shapes on purpose: Binary is the small-family pole (a million 2-child groups -- any
  // per-group and per-node bookkeeping dominates), Triangle the wide-family pole (1448-child
  // groups -- per-item costs amortize). Store classes carry both dimensions per the dimension
  // discipline (native + cross-order decode); stream classes are dimension-locked by the
  // traversal-dimension split, so their rows are unprefixed.
  internal static class FlatEncodings
  {
    public static (int[] Values, int[] SubtreeSizes, int[] Depths) BuildPreorder(ITreenumerable<int> tree)
    {
      var values = new List<int>();
      var subtreeSizes = new List<int>();
      var depths = new List<int>();
      var open = new Stack<int>();

      using (var treenumerator = tree.GetDepthFirstTreenumerator())
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          // Returning to this depth (or shallower) closes every deeper open node: its subtree
          // size is the count of nodes appended since it.
          while (open.Count > treenumerator.Position.Depth)
          {
            var closed = open.Pop();
            subtreeSizes[closed] = values.Count - closed;
          }

          open.Push(values.Count);
          values.Add(treenumerator.Node);
          subtreeSizes.Add(0);
          depths.Add(treenumerator.Position.Depth);
        }
      }

      while (open.Count > 0)
      {
        var closed = open.Pop();
        subtreeSizes[closed] = values.Count - closed;
      }

      return (values.ToArray(), subtreeSizes.ToArray(), depths.ToArray());
    }

    // One linear pass, walking each node's children by subtree-size hop and claiming output
    // slots in enqueue (== level) order; the running dequeue count is the dequeued node's slot.
    public static (int[] Values, int[] FirstChildIndices, int[] ChildCounts, int RootCount) BuildLevelOrder(
      int[] preorderValues,
      int[] subtreeSizes)
    {
      var count = preorderValues.Length;
      var values = new int[count];
      var firstChildIndices = new int[count];
      var childCounts = new int[count];
      var queue = new Queue<int>();
      var written = 0;
      var rootCount = 0;

      for (var root = 0; root < count; root += subtreeSizes[root])
      {
        values[written] = preorderValues[root];
        queue.Enqueue(root);
        written++;
        rootCount++;
      }

      var slot = 0;

      while (queue.Count > 0)
      {
        var node = queue.Dequeue();
        var end = node + subtreeSizes[node];

        firstChildIndices[slot] = written;

        for (var child = node + 1; child < end; child += subtreeSizes[child])
        {
          values[written] = preorderValues[child];
          queue.Enqueue(child);
          written++;
          childCounts[slot]++;
        }

        slot++;
      }

      return (values, firstChildIndices, childCounts, rootCount);
    }

    // The level-order encoding as its group sequence: group 0 is the roots, group j+1 the
    // children of level-order node j (empty for leaves).
    public static int[][] BuildGroups(int[] values, int[] firstChildIndices, int[] childCounts, int rootCount)
    {
      var groups = new int[values.Length + 1][];

      groups[0] = new int[rootCount];
      for (var root = 0; root < rootCount; root++)
        groups[0][root] = values[root];

      for (var node = 0; node < values.Length; node++)
      {
        var group = new int[childCounts[node]];

        for (var child = 0; child < group.Length; child++)
          group[child] = values[firstChildIndices[node] + child];

        groups[node + 1] = group;
      }

      return groups;
    }

    public static RefAppendOnlyList<int> ToChunked(int[] array)
    {
      var chunked = new RefAppendOnlyList<int>();

      for (var index = 0; index < array.Length; index++)
        chunked.AddLast(array[index]);

      return chunked;
    }

    // The D4c candidate read path (STORE_FAMILY_REVIEW.md): the completed stores' contract
    // answered straight from the chunked RefAppendOnlyList partitions the capture factories
    // build into, skipping the factories' final ToArray flattening. The same-run ratio against
    // the *ArrayStore classes is the replay price of that skipped copy -- the number that
    // decides whether D4c ships. Kept benchmark-private until it does.
    public readonly struct PreorderChunkedStore : IPreorderStore<int>
    {
      public PreorderChunkedStore(RefAppendOnlyList<int> values, RefAppendOnlyList<int> subtreeSizes)
      {
        _Values = values;
        _SubtreeSizes = subtreeSizes;
      }

      private readonly RefAppendOnlyList<int> _Values;
      private readonly RefAppendOnlyList<int> _SubtreeSizes;

      public bool EnsureBuffered(int index) => index < _Values.Count;

      public int EnsureSubtreeClosed(int index) => _SubtreeSizes[index];

      public int GetSubtreeSize(int index) => _SubtreeSizes[index];

      public int GetValue(int index) => _Values[index];
    }

    public readonly struct LevelOrderChunkedStore : ILevelOrderStore<int>
    {
      public LevelOrderChunkedStore(
        RefAppendOnlyList<int> values,
        RefAppendOnlyList<int> firstChildIndices,
        RefAppendOnlyList<int> childCounts,
        int rootCount)
      {
        _Values = values;
        _FirstChildIndices = firstChildIndices;
        _ChildCounts = childCounts;
        _RootCount = rootCount;
      }

      private readonly RefAppendOnlyList<int> _Values;
      private readonly RefAppendOnlyList<int> _FirstChildIndices;
      private readonly RefAppendOnlyList<int> _ChildCounts;
      private readonly int _RootCount;

      public bool EnsureRootAvailable(int k) => k < _RootCount;

      public bool EnsureChildAvailable(int parentIndex, int k) => k < _ChildCounts[parentIndex];

      public int GetFirstChildIndex(int parentIndex) => _FirstChildIndices[parentIndex];

      public int GetValue(int index) => _Values[index];
    }

    // The cheapest possible IPreorderStream: a cursor over prebuilt (value, depth) arrays, so
    // the decoder's own cost dominates the row.
    public sealed class ArrayPreorderStream : IPreorderStream<int>
    {
      public ArrayPreorderStream(int[] values, int[] depths)
      {
        _Values = values;
        _Depths = depths;
      }

      private readonly int[] _Values;
      private readonly int[] _Depths;
      private int _Position;

      public PreorderRead<int> TryReadNext()
      {
        if (_Position >= _Values.Length)
          return default;

        var read = new PreorderRead<int>(_Values[_Position], _Depths[_Position]);
        _Position++;

        return read;
      }

      public PreorderRead<int> TrySkipToDepth(int maxDepth)
      {
        while (_Position < _Values.Length && _Depths[_Position] > maxDepth)
          _Position++;

        return TryReadNext();
      }

      public void Dispose() { }
    }

    // The cheapest possible ILevelOrderStream: a cursor over prebuilt groups.
    public sealed class GroupArrayStream : ILevelOrderStream<int>
    {
      public GroupArrayStream(int[][] groups)
      {
        _Groups = groups;
      }

      private readonly int[][] _Groups;
      private int _Group;
      private int _Item;

      public LevelOrderRead<int> TryReadNextInGroup()
      {
        var group = _Groups[_Group];

        if (_Item >= group.Length)
          return default;

        var value = group[_Item];
        _Item++;

        return new LevelOrderRead<int>(value);
      }

      public int SkipGroupRemainder()
      {
        var remaining = _Groups[_Group].Length - _Item;
        _Item = _Groups[_Group].Length;

        return remaining;
      }

      public bool TryMoveToNextGroup()
      {
        if (_Group + 1 >= _Groups.Length)
          return false;

        _Group++;
        _Item = 0;

        return true;
      }

      public void Dispose() { }
    }
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("FlatDecode")]
  public class PreorderStoreDecode
  {
    private PreorderArrayStore<int> _Binary;
    private PreorderArrayStore<int> _Triangle;

    [GlobalSetup]
    public void Setup()
    {
      var (binaryValues, binarySizes, _) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaBinaryTree());
      _Binary = new PreorderArrayStore<int>(binaryValues, binarySizes);

      var (triangleValues, triangleSizes, _) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaTriangleTree());
      _Triangle = new PreorderArrayStore<int>(triangleValues, triangleSizes);
    }

    [Benchmark]
    public void Dft_Binary()
    {
      using (var treenumerator = new PreorderStoreDepthFirstTreenumerator<int, PreorderArrayStore<int>>(_Binary))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Bft_Binary()
    {
      using (var treenumerator = new PreorderStoreBreadthFirstTreenumerator<int, PreorderArrayStore<int>>(_Binary))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Dft_Triangle()
    {
      using (var treenumerator = new PreorderStoreDepthFirstTreenumerator<int, PreorderArrayStore<int>>(_Triangle))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      using (var treenumerator = new PreorderStoreBreadthFirstTreenumerator<int, PreorderArrayStore<int>>(_Triangle))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("FlatDecode")]
  public class LevelOrderStoreDecode
  {
    private LevelOrderArrayStore<int> _Binary;
    private LevelOrderArrayStore<int> _Triangle;

    [GlobalSetup]
    public void Setup()
    {
      var (binaryValues, binarySizes, _) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaBinaryTree());
      var binary = FlatEncodings.BuildLevelOrder(binaryValues, binarySizes);
      _Binary = new LevelOrderArrayStore<int>(binary.Values, binary.FirstChildIndices, binary.ChildCounts, binary.RootCount);

      var (triangleValues, triangleSizes, _) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaTriangleTree());
      var triangle = FlatEncodings.BuildLevelOrder(triangleValues, triangleSizes);
      _Triangle = new LevelOrderArrayStore<int>(triangle.Values, triangle.FirstChildIndices, triangle.ChildCounts, triangle.RootCount);
    }

    [Benchmark]
    public void Bft_Binary()
    {
      using (var treenumerator = new LevelOrderStoreBreadthFirstTreenumerator<int, LevelOrderArrayStore<int>>(_Binary))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Dft_Binary()
    {
      using (var treenumerator = new LevelOrderStoreDepthFirstTreenumerator<int, LevelOrderArrayStore<int>>(_Binary))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      using (var treenumerator = new LevelOrderStoreBreadthFirstTreenumerator<int, LevelOrderArrayStore<int>>(_Triangle))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Dft_Triangle()
    {
      using (var treenumerator = new LevelOrderStoreDepthFirstTreenumerator<int, LevelOrderArrayStore<int>>(_Triangle))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }
  }

  // The chunked twins of the two store classes above: identical decode work over
  // RefAppendOnlyList-backed stores. Chunked-vs-flat replay had no direct A/B anywhere in the
  // suite before these rows -- the memo rows blend it with growth guards and async ratios.
  [MemoryDiagnoser]
  [BenchmarkCategory("FlatDecode")]
  public class PreorderChunkedStoreDecode
  {
    private FlatEncodings.PreorderChunkedStore _Binary;
    private FlatEncodings.PreorderChunkedStore _Triangle;

    [GlobalSetup]
    public void Setup()
    {
      var (binaryValues, binarySizes, _) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaBinaryTree());
      _Binary = new FlatEncodings.PreorderChunkedStore(FlatEncodings.ToChunked(binaryValues), FlatEncodings.ToChunked(binarySizes));

      var (triangleValues, triangleSizes, _) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaTriangleTree());
      _Triangle = new FlatEncodings.PreorderChunkedStore(FlatEncodings.ToChunked(triangleValues), FlatEncodings.ToChunked(triangleSizes));
    }

    [Benchmark]
    public void Dft_Binary()
    {
      using (var treenumerator = new PreorderStoreDepthFirstTreenumerator<int, FlatEncodings.PreorderChunkedStore>(_Binary))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Bft_Binary()
    {
      using (var treenumerator = new PreorderStoreBreadthFirstTreenumerator<int, FlatEncodings.PreorderChunkedStore>(_Binary))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Dft_Triangle()
    {
      using (var treenumerator = new PreorderStoreDepthFirstTreenumerator<int, FlatEncodings.PreorderChunkedStore>(_Triangle))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      using (var treenumerator = new PreorderStoreBreadthFirstTreenumerator<int, FlatEncodings.PreorderChunkedStore>(_Triangle))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("FlatDecode")]
  public class LevelOrderChunkedStoreDecode
  {
    private FlatEncodings.LevelOrderChunkedStore _Binary;
    private FlatEncodings.LevelOrderChunkedStore _Triangle;

    [GlobalSetup]
    public void Setup()
    {
      var (binaryValues, binarySizes, _) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaBinaryTree());
      var binary = FlatEncodings.BuildLevelOrder(binaryValues, binarySizes);
      _Binary = new FlatEncodings.LevelOrderChunkedStore(
        FlatEncodings.ToChunked(binary.Values), FlatEncodings.ToChunked(binary.FirstChildIndices), FlatEncodings.ToChunked(binary.ChildCounts), binary.RootCount);

      var (triangleValues, triangleSizes, _) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaTriangleTree());
      var triangle = FlatEncodings.BuildLevelOrder(triangleValues, triangleSizes);
      _Triangle = new FlatEncodings.LevelOrderChunkedStore(
        FlatEncodings.ToChunked(triangle.Values), FlatEncodings.ToChunked(triangle.FirstChildIndices), FlatEncodings.ToChunked(triangle.ChildCounts), triangle.RootCount);
    }

    [Benchmark]
    public void Bft_Binary()
    {
      using (var treenumerator = new LevelOrderStoreBreadthFirstTreenumerator<int, FlatEncodings.LevelOrderChunkedStore>(_Binary))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Dft_Binary()
    {
      using (var treenumerator = new LevelOrderStoreDepthFirstTreenumerator<int, FlatEncodings.LevelOrderChunkedStore>(_Binary))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      using (var treenumerator = new LevelOrderStoreBreadthFirstTreenumerator<int, FlatEncodings.LevelOrderChunkedStore>(_Triangle))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Dft_Triangle()
    {
      using (var treenumerator = new LevelOrderStoreDepthFirstTreenumerator<int, FlatEncodings.LevelOrderChunkedStore>(_Triangle))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("FlatDecode")]
  public class PreorderStreamDecode
  {
    private int[] _BinaryValues;
    private int[] _BinaryDepths;
    private int[] _TriangleValues;
    private int[] _TriangleDepths;

    [GlobalSetup]
    public void Setup()
    {
      var (binaryValues, _, binaryDepths) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaBinaryTree());
      _BinaryValues = binaryValues;
      _BinaryDepths = binaryDepths;

      var (triangleValues, _, triangleDepths) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaTriangleTree());
      _TriangleValues = triangleValues;
      _TriangleDepths = triangleDepths;
    }

    [Benchmark]
    public void Binary()
    {
      using (var treenumerator = new PreorderStreamDepthFirstTreenumerator<int, FlatEncodings.ArrayPreorderStream>(
        new FlatEncodings.ArrayPreorderStream(_BinaryValues, _BinaryDepths)))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Triangle()
    {
      using (var treenumerator = new PreorderStreamDepthFirstTreenumerator<int, FlatEncodings.ArrayPreorderStream>(
        new FlatEncodings.ArrayPreorderStream(_TriangleValues, _TriangleDepths)))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("FlatDecode")]
  public class LevelOrderStreamDecode
  {
    private int[][] _BinaryGroups;
    private int[][] _TriangleGroups;

    [GlobalSetup]
    public void Setup()
    {
      var (binaryValues, binarySizes, _) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaBinaryTree());
      var binary = FlatEncodings.BuildLevelOrder(binaryValues, binarySizes);
      _BinaryGroups = FlatEncodings.BuildGroups(binary.Values, binary.FirstChildIndices, binary.ChildCounts, binary.RootCount);

      var (triangleValues, triangleSizes, _) = FlatEncodings.BuildPreorder(CanonicalTrees.MegaTriangleTree());
      var triangle = FlatEncodings.BuildLevelOrder(triangleValues, triangleSizes);
      _TriangleGroups = FlatEncodings.BuildGroups(triangle.Values, triangle.FirstChildIndices, triangle.ChildCounts, triangle.RootCount);
    }

    [Benchmark]
    public void Binary()
    {
      using (var treenumerator = new LevelOrderStreamBreadthFirstTreenumerator<int, FlatEncodings.GroupArrayStream>(
        new FlatEncodings.GroupArrayStream(_BinaryGroups)))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public void Triangle()
    {
      using (var treenumerator = new LevelOrderStreamBreadthFirstTreenumerator<int, FlatEncodings.GroupArrayStream>(
        new FlatEncodings.GroupArrayStream(_TriangleGroups)))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }
  }
}
