using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // The preorder visit-stream adapter over the depth-first drain (dimension-locked: preorder is
  // a DFT-derived order, so rows carry no Dft_/Bft_ prefix).
  [MemoryDiagnoser]
  [BenchmarkCategory("VisitStream", "Preorder")]
  public class PreorderTraversal
  {
    [Benchmark]
    public void Chain() =>
      CanonicalTrees.MegaChainTree().GetPreorderTraversal().Consume();

    [Benchmark]
    public void Forest() =>
      CanonicalTrees.MegaForest().GetPreorderTraversal().Consume();

    [Benchmark]
    public void Binary() =>
      CanonicalTrees.MegaBinaryTree().GetPreorderTraversal().Consume();

    [Benchmark]
    public void Triangle() =>
      CanonicalTrees.MegaTriangleTree().GetPreorderTraversal().Consume();
  }
}
