using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Leaf extraction in both arrival orders, on the leaf-extreme shapes: Binary (half the nodes
  // are leaves) and DeepChains (almost no leaves, found at maximum depth). The dimension is
  // selected by narrow-interface dispatch, mirroring how a caller with a narrow source pays.
  [MemoryDiagnoser]
  [BenchmarkCategory("Query", "GetLeaves")]
  public class GetLeaves
  {
    [Benchmark]
    public void Dft_Binary() =>
      ((IDepthFirstTreenumerable<int>)CanonicalTrees.MegaBinaryTree()).GetLeaves().Consume();

    [Benchmark]
    public void Bft_Binary() =>
      ((IBreadthFirstTreenumerable<int>)CanonicalTrees.MegaBinaryTree()).GetLeaves().Consume();

    [Benchmark]
    public void Dft_DeepChains() =>
      ((IDepthFirstTreenumerable<int>)CanonicalTrees.MegaDeepChainsTree()).GetLeaves().Consume();

    [Benchmark]
    public void Bft_DeepChains() =>
      ((IBreadthFirstTreenumerable<int>)CanonicalTrees.MegaDeepChainsTree()).GetLeaves().Consume();
  }
}
