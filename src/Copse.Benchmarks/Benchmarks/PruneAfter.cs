using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Forest_All prunes every root after its visit (maximum prune throughput); Triangle_HalfDepth
  // cuts the triangle at half depth (a realistic partial prune: roughly a quarter of the nodes
  // survive, and the cut runs through live traversal state).
  [MemoryDiagnoser]
  [BenchmarkCategory("Streaming", "Prune")]
  public class PruneAfter
  {
    private const int HalfDepth = 724;

    [Benchmark]
    public void Dft_Forest_All() =>
      CanonicalTrees.MegaForest().PruneAfter(_ => true).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Forest_All() =>
      CanonicalTrees.MegaForest().PruneAfter(_ => true).Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Triangle_HalfDepth() =>
      CanonicalTrees.MegaTriangleTree()
      .PruneAfter(nodeContext => nodeContext.Position.Depth == HalfDepth)
      .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle_HalfDepth() =>
      CanonicalTrees.MegaTriangleTree()
      .PruneAfter(nodeContext => nodeContext.Position.Depth == HalfDepth)
      .Consume(TreeTraversalStrategy.BreadthFirst);
  }
}
