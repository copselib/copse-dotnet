using Copse;
using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Leaves-to-root cumulative scan, fold tier (sugar over LeaffixDispatch): the delta against
  // the LeaffixDispatch benchmark is the delegation wrapper. A buffer producer, so per the
  // buffer-producer rule the result drains both dimensions. Typed as plain trees for the same
  // overload-resolution reason as the Memoize replays.
  [MemoryDiagnoser]
  [BenchmarkCategory("Aggregate", "Leaffix")]
  public class LeaffixScan
  {
    // Each node projects to 1 and folds in each child's completed subtree node count.
    private static int SubtreeNodeCount(int accumulate, int childAccumulate)
      => accumulate + childAccumulate;

    [Benchmark]
    public void Dft_Triangle()
    {
      ITreenumerable<int> scan = CanonicalTrees.MegaTriangleTree().LeaffixScan(_ => 1, SubtreeNodeCount);
      scan.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      ITreenumerable<int> scan = CanonicalTrees.MegaTriangleTree().LeaffixScan(_ => 1, SubtreeNodeCount);
      scan.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_Chain()
    {
      ITreenumerable<int> scan = CanonicalTrees.MegaChainTree().LeaffixScan(_ => 1, SubtreeNodeCount);
      scan.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Chain()
    {
      ITreenumerable<int> scan = CanonicalTrees.MegaChainTree().LeaffixScan(_ => 1, SubtreeNodeCount);
      scan.Consume(TreeTraversalStrategy.BreadthFirst);
    }
  }
}
