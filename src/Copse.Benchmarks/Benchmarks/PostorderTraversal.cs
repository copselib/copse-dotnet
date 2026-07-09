using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // The postorder visit-stream adapter over the depth-first drain (dimension-locked: postorder
  // is a DFT-derived order, so rows carry no Dft_/Bft_ prefix).
  [MemoryDiagnoser]
  [BenchmarkCategory("VisitStream", "Postorder")]
  public class PostorderTraversal
  {
    [Benchmark]
    public void Chain() =>
      CanonicalTrees.MegaChainTree().PostorderTraversal().Consume();

    [Benchmark]
    public void Forest() =>
      CanonicalTrees.MegaForest().PostorderTraversal().Consume();

    [Benchmark]
    public void Binary() =>
      CanonicalTrees.MegaBinaryTree().PostorderTraversal().Consume();

    [Benchmark]
    public void Triangle() =>
      CanonicalTrees.MegaTriangleTree().PostorderTraversal().Consume();
  }
}
