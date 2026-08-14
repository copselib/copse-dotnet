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
      CanonicalTrees.MegaChainTree().GetLevelOrderTraversal().Consume();

    [Benchmark]
    public void Forest() =>
      CanonicalTrees.MegaForest().GetLevelOrderTraversal().Consume();

    [Benchmark]
    public void Binary() =>
      CanonicalTrees.MegaBinaryTree().GetLevelOrderTraversal().Consume();

    [Benchmark]
    public void Triangle() =>
      CanonicalTrees.MegaTriangleTree().GetLevelOrderTraversal().Consume();
  }
}
