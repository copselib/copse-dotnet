using Copse;
using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Leaves-to-root cumulative scan: a buffer producer (the scan must see the whole subtree
  // before its root emits), so per the buffer-producer rule the result drains both dimensions.
  // Typed as plain trees for the same overload-resolution reason as the Memoize replays.
  [MemoryDiagnoser]
  [BenchmarkCategory("Aggregate", "Leaffix")]
  public class LeaffixScan
  {
    // Each node accumulates its own subtree node count from its children's counts.
    private static int SubtreeNodeCount(NodeContext<int> nodeContext, ChildAccumulations<int> children)
    {
      var count = 1;
      foreach (var child in children)
        count += child;
      return count;
    }

    [Benchmark]
    public void Dft_Triangle()
    {
      ITreenumerable<int> scan = CanonicalTrees.MegaTriangleTree().LeaffixScan(SubtreeNodeCount, _ => 1);
      scan.Drain(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      ITreenumerable<int> scan = CanonicalTrees.MegaTriangleTree().LeaffixScan(SubtreeNodeCount, _ => 1);
      scan.Drain(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_Chain()
    {
      ITreenumerable<int> scan = CanonicalTrees.MegaChainTree().LeaffixScan(SubtreeNodeCount, _ => 1);
      scan.Drain(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Chain()
    {
      ITreenumerable<int> scan = CanonicalTrees.MegaChainTree().LeaffixScan(SubtreeNodeCount, _ => 1);
      scan.Drain(TreeTraversalStrategy.BreadthFirst);
    }
  }
}
