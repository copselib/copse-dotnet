using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // The raw engine drain: every canonical shape, both dimensions, at the Mega tier -- the
  // baseline every operator benchmark is implicitly measured against. The SkipAll rows drain
  // with NodeTraversalStrategies.SkipNode, the cheapest possible per-node path.
  [MemoryDiagnoser]
  [BenchmarkCategory("Traversal", "Engine")]
  public class Traversal
  {
    [Benchmark]
    public void Dft_Chain() =>
      CanonicalTrees.MegaChainTree().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Chain() =>
      CanonicalTrees.MegaChainTree().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Forest() =>
      CanonicalTrees.MegaForest().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Forest() =>
      CanonicalTrees.MegaForest().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Binary() =>
      CanonicalTrees.MegaBinaryTree().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Binary() =>
      CanonicalTrees.MegaBinaryTree().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Triangle() =>
      CanonicalTrees.MegaTriangleTree().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle() =>
      CanonicalTrees.MegaTriangleTree().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_DeepChains() =>
      CanonicalTrees.MegaDeepChainsTree().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_DeepChains() =>
      CanonicalTrees.MegaDeepChainsTree().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Triangle_SkipAll()
    {
      using (var treenumerator = CanonicalTrees.MegaTriangleTree().GetDepthFirstTreenumerator())
        while (treenumerator.MoveNext(NodeTraversalStrategies.SkipNode)) ;
    }

    [Benchmark]
    public void Bft_Triangle_SkipAll()
    {
      using (var treenumerator = CanonicalTrees.MegaTriangleTree().GetBreadthFirstTreenumerator())
        while (treenumerator.MoveNext(NodeTraversalStrategies.SkipNode)) ;
    }
  }
}
