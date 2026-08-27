using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // The PruneDescendantsWhere dual: Forest_All never visits any root (maximum prune throughput);
  // Triangle_HalfDepth excludes the node AND its subtree at the cut.
  [MemoryDiagnoser]
  [BenchmarkCategory("Streaming", "Prune")]
  public class PruneSubtreesWhere
  {
    private const int HalfDepth = 724;

    [Benchmark]
    public void Dft_Forest_All() =>
      CanonicalTrees.MegaForest().PruneSubtreesWhere(_ => true).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Forest_All() =>
      CanonicalTrees.MegaForest().PruneSubtreesWhere(_ => true).Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Triangle_HalfDepth() =>
      CanonicalTrees.MegaTriangleTree()
      .PruneSubtreesWhere((n, position) => position.Depth == HalfDepth)
      .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle_HalfDepth() =>
      CanonicalTrees.MegaTriangleTree()
      .PruneSubtreesWhere((n, position) => position.Depth == HalfDepth)
      .Consume(TreeTraversalStrategy.BreadthFirst);
  }
}
