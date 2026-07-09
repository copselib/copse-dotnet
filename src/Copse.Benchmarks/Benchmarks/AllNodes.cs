using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // The predicate-terminal drain. There is deliberately no AnyNodes twin: AnyNodes(_ => false)
  // is workload-identical to AllNodes(_ => true) (a full drain that never exits early), and the
  // short-circuiting form measures nothing (it exits on the first node).
  [MemoryDiagnoser]
  [BenchmarkCategory("Query", "AllNodes")]
  public class AllNodes
  {
    [Benchmark]
    public void Dft_Chain() =>
      CanonicalTrees.MegaChainTree().AllNodes(_ => true, TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Chain() =>
      CanonicalTrees.MegaChainTree().AllNodes(_ => true, TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Forest() =>
      CanonicalTrees.MegaForest().AllNodes(_ => true, TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Forest() =>
      CanonicalTrees.MegaForest().AllNodes(_ => true, TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Binary() =>
      CanonicalTrees.MegaBinaryTree().AllNodes(_ => true, TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Binary() =>
      CanonicalTrees.MegaBinaryTree().AllNodes(_ => true, TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Triangle() =>
      CanonicalTrees.MegaTriangleTree().AllNodes(_ => true, TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle() =>
      CanonicalTrees.MegaTriangleTree().AllNodes(_ => true, TreeTraversalStrategy.BreadthFirst);
  }
}
