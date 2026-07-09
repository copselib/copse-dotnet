using BenchmarkDotNet.Attributes;
using Copse.Async;
using Copse.Async.Treenumerables;
using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq;
using Copse.SimpleSerializer;
using Copse.Trees;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Copse.Benchmarks
{
  // The async-overhead pairs: each class benchmarks ONE workload in both colors, with the sync
  // side as Baseline, so the Ratio column IS the cost of the ValueTask seam (state-machine
  // transitions per pull) on that workload. The async sources complete synchronously on every
  // pull -- a suspending source would benchmark the scheduler, not the library. Because the
  // sync twins are generated from these exact async sources, any ratio drift is pure seam cost,
  // never logic divergence.
  //
  // Same-run ratios are the trustworthy number on shared CI runners (absolute times are not),
  // which is why each pair lives in one class and the whole category runs in one CI leg.

  internal static class AsyncOverheadSources
  {
    // The async mirror of CompleteBinaryTree: children of n are 2n+1 / 2n+2, unbounded, cut by
    // PruneBefore at the requested depth exactly like Treenumerables.GetWideTree. Every pull
    // completes synchronously.
    public readonly struct AsyncBinaryChildEnumerator : IAsyncChildEnumerator<int>
    {
      private readonly int _Node;
      private readonly int[] _Index; // single-cell state so the struct can advance

      public AsyncBinaryChildEnumerator(int node)
      {
        _Node = node;
        _Index = new int[1];
      }

      public ValueTask<ChildResult<int>> MoveNextAsync()
      {
        var index = _Index[0];

        if (index >= 2)
          return default;

        _Index[0] = index + 1;
        return new ValueTask<ChildResult<int>>(
          new ChildResult<int>(new NodeAndSiblingIndex<int>(2 * _Node + 1 + index, index)));
      }

      public void Dispose() { }
      public ValueTask DisposeAsync() => default;
    }

    public static IAsyncTreenumerable<int> GetAsyncBinaryTree(int depth)
      => new AsyncTreenumerable<int, int, AsyncBinaryChildEnumerator>(
          nodeContext => new AsyncBinaryChildEnumerator(nodeContext.Node),
          node => node,
          RootAsync())
        .PruneBefore(nodeContext => nodeContext.Position.Depth == depth);

    public static ITreenumerable<int> GetSyncBinaryTree(int depth)
      => new CompleteBinaryTree()
        .PruneBefore(nodeContext => nodeContext.Position.Depth == depth);

    private static async IAsyncEnumerable<int> RootAsync()
    {
      await Task.CompletedTask;
      yield return 0;
    }

    // Presents a completed PreorderArrayStore through the async store SPI (every grow answers
    // with a completed ValueTask) -- isolates the DECODER seam on identical data.
    public readonly struct CompletedAsyncPreorderStore : IAsyncPreorderStore<int>
    {
      private readonly PreorderArrayStore<int> _Store;
      public CompletedAsyncPreorderStore(PreorderArrayStore<int> store) { _Store = store; }
      public ValueTask<bool> EnsureBufferedAsync(int index) => new ValueTask<bool>(_Store.EnsureBuffered(index));
      public ValueTask<int> EnsureSubtreeClosedAsync(int index) => new ValueTask<int>(_Store.EnsureSubtreeClosed(index));
      public int GetSubtreeSize(int index) => _Store.GetSubtreeSize(index);
      public int GetValue(int index) => _Store.GetValue(index);
    }

    // Flat preorder arrays of a complete binary tree of the given height (node count 2^(h+1)-1):
    // node i's subtree size depends only on its depth, so fill by recursion.
    public static (int[] Values, int[] SubtreeSizes) BuildBinaryPreorderArrays(int height)
    {
      var count = (1 << (height + 1)) - 1;
      var values = new int[count];
      var subtreeSizes = new int[count];
      var next = 0;

      void Fill(int node, int remainingHeight)
      {
        var index = next++;
        values[index] = node;
        subtreeSizes[index] = (1 << (remainingHeight + 1)) - 1;

        if (remainingHeight == 0)
          return;

        Fill(2 * node + 1, remainingHeight - 1);
        Fill(2 * node + 2, remainingHeight - 1);
      }

      Fill(0, height);
      return (values, subtreeSizes);
    }
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("AsyncOverhead")]
  public class AsyncOverheadDepthFirstEngine
  {
    private const int Depth = 14;

    [Benchmark(Baseline = true)]
    public void Sync() => AsyncOverheadSources.GetSyncBinaryTree(Depth).Consume();

    [Benchmark]
    public async ValueTask Async() => await AsyncOverheadSources.GetAsyncBinaryTree(Depth).ConsumeAsync();
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("AsyncOverhead")]
  public class AsyncOverheadBreadthFirstEngine
  {
    private const int Depth = 14;

    [Benchmark(Baseline = true)]
    public void Sync() => AsyncOverheadSources.GetSyncBinaryTree(Depth).Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public async ValueTask Async() => await AsyncOverheadSources.GetAsyncBinaryTree(Depth).ConsumeAsync(TreeTraversalStrategy.BreadthFirst);
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("AsyncOverhead")]
  public class AsyncOverheadFlatDecode
  {
    private PreorderArrayStore<int> _Store;

    [GlobalSetup]
    public void Setup()
    {
      var (values, subtreeSizes) = AsyncOverheadSources.BuildBinaryPreorderArrays(16);
      _Store = new PreorderArrayStore<int>(values, subtreeSizes);
    }

    [Benchmark(Baseline = true)]
    public void Sync()
    {
      using var treenumerator = new Copse.Treenumerators.PreorderStoreDepthFirstTreenumerator<int, PreorderArrayStore<int>>(_Store);
      while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
    }

    [Benchmark]
    public async ValueTask Async()
    {
      var treenumerator = new AsyncPreorderStoreDepthFirstTreenumerator<int, AsyncOverheadSources.CompletedAsyncPreorderStore>(
        new AsyncOverheadSources.CompletedAsyncPreorderStore(_Store));
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false)) { }
    }
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("AsyncOverhead")]
  public class AsyncOverheadOperatorStack
  {
    private const int Depth = 12;

    [Benchmark(Baseline = true)]
    public void Sync()
      => AsyncOverheadSources.GetSyncBinaryTree(Depth)
        .Where(nodeContext => nodeContext.Node % 3 != 0)
        .Select(nodeContext => nodeContext.Node * 2)
        .Consume();

    [Benchmark]
    public async ValueTask Async()
      => await AsyncOverheadSources.GetAsyncBinaryTree(Depth)
        .Where(nodeContext => nodeContext.Node % 3 != 0)
        .Select(nodeContext => nodeContext.Node * 2)
        .ConsumeAsync();
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("AsyncOverhead")]
  public class AsyncOverheadSerializerRoundTrip
  {
    private string _Payload;

    [GlobalSetup]
    public void Setup() => _Payload = AsyncOverheadSources.GetSyncBinaryTree(12).SerializeDepthFirstTree(node => node.ToString());

    [Benchmark(Baseline = true)]
    public string Sync()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree(() => new StringReader(_Payload));
      var writer = new StringWriter();
      tree.SerializeDepthFirstTree(writer);
      return writer.ToString();
    }

    [Benchmark]
    public async ValueTask<string> Async()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTreeAsync(() => new StringReader(_Payload));
      var writer = new StringWriter();
      await tree.SerializeDepthFirstTreeAsync(writer);
      return writer.ToString();
    }
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("AsyncOverhead")]
  public class AsyncOverheadMaterializeReplay
  {
    private const int Depth = 12;

    [Benchmark(Baseline = true)]
    public void Sync()
    {
      var buffer = AsyncOverheadSources.GetSyncBinaryTree(Depth).Materialize();
      buffer.Consume();
    }

    [Benchmark]
    public async ValueTask Async()
    {
      var buffer = await AsyncOverheadSources.GetAsyncBinaryTree(Depth).MaterializeAsync();
      await buffer.ConsumeAsync();
    }
  }
}
