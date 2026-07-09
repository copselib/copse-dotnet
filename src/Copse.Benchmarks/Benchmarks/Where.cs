using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Structural filtering with child promotion, both dimensions in one class (they are entirely
  // different algorithms -- see the Where sections of CLAUDE.md). KeepAll/DropAll bracket the
  // predicate extremes on the width/depth extreme shapes; Triangle_Mixed is the realistic
  // 50% case where promotion bookkeeping actually engages.
  [MemoryDiagnoser]
  [BenchmarkCategory("Streaming", "Where")]
  public class Where
  {
    [Benchmark]
    public void Dft_Triangle_Mixed() =>
      CanonicalTrees.MegaTriangleTree()
      .Where(nodeContext => (nodeContext.Position.Depth + nodeContext.Position.SiblingIndex) % 2 == 0)
      .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle_Mixed() =>
      CanonicalTrees.MegaTriangleTree()
      .Where(nodeContext => (nodeContext.Position.Depth + nodeContext.Position.SiblingIndex) % 2 == 0)
      .Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Chain_KeepAll() =>
      CanonicalTrees.MegaChainTree().Where(_ => true).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Chain_KeepAll() =>
      CanonicalTrees.MegaChainTree().Where(_ => true).Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Chain_DropAll() =>
      CanonicalTrees.MegaChainTree().Where(_ => false).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Chain_DropAll() =>
      CanonicalTrees.MegaChainTree().Where(_ => false).Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Forest_KeepAll() =>
      CanonicalTrees.MegaForest().Where(_ => true).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Forest_KeepAll() =>
      CanonicalTrees.MegaForest().Where(_ => true).Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Forest_DropAll() =>
      CanonicalTrees.MegaForest().Where(_ => false).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Forest_DropAll() =>
      CanonicalTrees.MegaForest().Where(_ => false).Consume(TreeTraversalStrategy.BreadthFirst);
  }
}
