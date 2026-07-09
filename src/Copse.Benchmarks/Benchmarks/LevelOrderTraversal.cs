using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // The level-order visit-stream adapter over the breadth-first drain (dimension-locked:
  // level-order is a BFT-derived order, so rows carry no Dft_/Bft_ prefix).
  [MemoryDiagnoser]
  [BenchmarkCategory("VisitStream", "LevelOrder")]
  public class LevelOrderTraversal
  {
    [Benchmark]
    public void Chain() =>
      CanonicalTrees.MegaChainTree().LevelOrderTraversal().Consume();

    [Benchmark]
    public void Forest() =>
      CanonicalTrees.MegaForest().LevelOrderTraversal().Consume();

    [Benchmark]
    public void Binary() =>
      CanonicalTrees.MegaBinaryTree().LevelOrderTraversal().Consume();

    [Benchmark]
    public void Triangle() =>
      CanonicalTrees.MegaTriangleTree().LevelOrderTraversal().Consume();
  }
}
