using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Composition rows stack four projections (measures layer collapse / per-layer cost);
  // the Binary rows are the single-projection baseline.
  [MemoryDiagnoser]
  [BenchmarkCategory("Streaming", "Select")]
  public class Select
  {
    [Benchmark]
    public void Dft_Forest_Composition() =>
      CanonicalTrees.MegaForest()
      .Select(x => x * 2)
      .Select(x => x + 'a')
      .Select(x => x + 1)
      .Select(x => (char)x)
      .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Forest_Composition() =>
      CanonicalTrees.MegaForest()
      .Select(x => x * 2)
      .Select(x => x + 'a')
      .Select(x => x + 1)
      .Select(x => (char)x)
      .Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Binary() =>
      CanonicalTrees.MegaBinaryTree()
      .Select(x => x * 2)
      .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Binary() =>
      CanonicalTrees.MegaBinaryTree()
      .Select(x => x * 2)
      .Consume(TreeTraversalStrategy.BreadthFirst);
  }
}
