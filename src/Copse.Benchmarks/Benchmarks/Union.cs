using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;
using System.Linq;

namespace Copse.Benchmarks
{
  // Structural merge over the canonical pairings: identical trees (maximum overlap), disjoint
  // extremes (chains, forests), asymmetric shape (wide vs deep), and asymmetric size
  // (forest vs half forest).
  [MemoryDiagnoser]
  [BenchmarkCategory("Merge", "Union")]
  public class Union
  {
    private static ITreenumerable<int> HalfForest()
      => Enumerable.Range(0, CanonicalTrees.MegaChain / 2).ToTrivialForest();

    [Benchmark]
    public void Dft_IdenticalTriangles() =>
      CanonicalTrees.MegaTriangleTree().Union(CanonicalTrees.MegaTriangleTree())
        .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_IdenticalTriangles() =>
      CanonicalTrees.MegaTriangleTree().Union(CanonicalTrees.MegaTriangleTree())
        .Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Chains() =>
      CanonicalTrees.MegaChainTree().Union(CanonicalTrees.MegaChainTree())
        .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Chains() =>
      CanonicalTrees.MegaChainTree().Union(CanonicalTrees.MegaChainTree())
        .Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Forests() =>
      CanonicalTrees.MegaForest().Union(CanonicalTrees.MegaForest())
        .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Forests() =>
      CanonicalTrees.MegaForest().Union(CanonicalTrees.MegaForest())
        .Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_BinaryVsChain() =>
      CanonicalTrees.MegaBinaryTree().Union(CanonicalTrees.MegaChainTree())
        .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_BinaryVsChain() =>
      CanonicalTrees.MegaBinaryTree().Union(CanonicalTrees.MegaChainTree())
        .Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_ForestVsHalfForest() =>
      CanonicalTrees.MegaForest().Union(HalfForest())
        .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_ForestVsHalfForest() =>
      CanonicalTrees.MegaForest().Union(HalfForest())
        .Consume(TreeTraversalStrategy.BreadthFirst);
  }
}
