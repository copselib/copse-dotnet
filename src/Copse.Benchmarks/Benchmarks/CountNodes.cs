using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  [MemoryDiagnoser]
  [BenchmarkCategory("Query", "CountNodes")]
  public class CountNodes
  {
    [Benchmark]
    public int Dft_Chain() =>
      CanonicalTrees.MegaChainTree().CountNodes(_ => true, TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public int Bft_Chain() =>
      CanonicalTrees.MegaChainTree().CountNodes(_ => true, TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public int Dft_Forest() =>
      CanonicalTrees.MegaForest().CountNodes(_ => true, TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public int Bft_Forest() =>
      CanonicalTrees.MegaForest().CountNodes(_ => true, TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public int Dft_Binary() =>
      CanonicalTrees.MegaBinaryTree().CountNodes(_ => true, TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public int Bft_Binary() =>
      CanonicalTrees.MegaBinaryTree().CountNodes(_ => true, TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public int Dft_Triangle() =>
      CanonicalTrees.MegaTriangleTree().CountNodes(_ => true, TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public int Bft_Triangle() =>
      CanonicalTrees.MegaTriangleTree().CountNodes(_ => true, TreeTraversalStrategy.BreadthFirst);
  }
}
