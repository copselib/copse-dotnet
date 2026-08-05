using Copse;
using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Leaves-to-root cumulative scan, sibling-complete tier: the survey reads its children through
  // the no-copy ChildAccumulations view (subtree-span hops, struct enumerator). Same rule as the
  // LeaffixScan benchmark so the two tiers' costs are directly comparable -- the delta is the
  // view walk versus the fold-into-slot.
  [MemoryDiagnoser]
  [BenchmarkCategory("Aggregate", "Leaffix")]
  public class LeaffixDispatch
  {
    // Each node accumulates its own subtree node count from its children's counts.
    private static int SubtreeNodeCount(int node, DispatchSources<int, int> children)
    {
      var count = 1;
      foreach (var child in children)
        count += child.Accumulate;
      return count;
    }

    [Benchmark]
    public void Dft_Triangle()
    {
      var dispatch = CanonicalTrees.MegaTriangleTree().LeaffixDispatch(_ => 1, SubtreeNodeCount);
      dispatch.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      var dispatch = CanonicalTrees.MegaTriangleTree().LeaffixDispatch(_ => 1, SubtreeNodeCount);
      dispatch.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_Chain()
    {
      var dispatch = CanonicalTrees.MegaChainTree().LeaffixDispatch(_ => 1, SubtreeNodeCount);
      dispatch.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Chain()
    {
      var dispatch = CanonicalTrees.MegaChainTree().LeaffixDispatch(_ => 1, SubtreeNodeCount);
      dispatch.Consume(TreeTraversalStrategy.BreadthFirst);
    }
  }
}
