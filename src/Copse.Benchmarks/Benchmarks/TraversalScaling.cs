using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // The Stress tier (~2^22 nodes) exists ONLY here: these rows answer "does the engine scale
  // linearly", which needs a second size point; operator benchmarks stay at Mega where the
  // per-node cost question lives. Compare against the Traversal rows: 4x the nodes should be
  // ~4x the time -- superlinear growth here means engine state management regressed.
  [MemoryDiagnoser]
  [BenchmarkCategory("Traversal", "Scaling")]
  public class TraversalScaling
  {
    [Benchmark]
    public void Dft_Chain() =>
      CanonicalTrees.StressChainTree().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Chain() =>
      CanonicalTrees.StressChainTree().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Forest() =>
      CanonicalTrees.StressForest().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Forest() =>
      CanonicalTrees.StressForest().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Binary() =>
      CanonicalTrees.StressBinaryTree().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Binary() =>
      CanonicalTrees.StressBinaryTree().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Triangle() =>
      CanonicalTrees.StressTriangleTree().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle() =>
      CanonicalTrees.StressTriangleTree().Consume(TreeTraversalStrategy.BreadthFirst);
  }
}
