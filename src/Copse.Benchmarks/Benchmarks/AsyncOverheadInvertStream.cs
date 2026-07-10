using BenchmarkDotNet.Attributes;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq;
using System.Threading.Tasks;

namespace Copse.Benchmarks
{
  // The streaming mirror pair: a breadth-first-ONLY source's Invert streams tier by tier
  // (no capture), so the ratio prices the seam on the InvertedLevelOrderStream serving path.
  [MemoryDiagnoser]
  [BenchmarkCategory("AsyncOverhead")]
  public class AsyncOverheadInvertStream
  {
    private const int Depth = 14;

    [Benchmark(Baseline = true)]
    public void Sync()
    {
      IBreadthFirstTreenumerable<int> narrow = AsyncOverheadSources.GetSyncBinaryTree(Depth);
      narrow.Invert().Consume();
    }

    [Benchmark]
    public async ValueTask Async()
    {
      IAsyncBreadthFirstTreenumerable<int> narrow = AsyncOverheadSources.GetAsyncBinaryTree(Depth);
      await narrow.Invert().ConsumeAsync();
    }
  }
}
