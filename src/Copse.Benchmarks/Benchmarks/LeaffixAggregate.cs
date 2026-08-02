using Copse;
using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;
using System.Linq;

namespace Copse.Benchmarks
{
  // Leaves-to-root aggregation to a flat sequence (dimension-locked: the aggregation order is
  // inherently leaffix, so rows carry no Dft_/Bft_ prefix).
  [MemoryDiagnoser]
  [BenchmarkCategory("Aggregate", "Leaffix")]
  public class LeaffixAggregate
  {
    // Each node projects to 1 and folds in each child's completed subtree node count.
    private static int SubtreeNodeCount(int accumulate, int childAccumulate)
      => accumulate + childAccumulate;

    [Benchmark]
    public int Triangle() =>
      CanonicalTrees.MegaTriangleTree().LeaffixAggregate(_ => 1, SubtreeNodeCount).Sum(pairing => pairing.Accumulate);

    [Benchmark]
    public int Chain() =>
      CanonicalTrees.MegaChainTree().LeaffixAggregate(_ => 1, SubtreeNodeCount).Sum(pairing => pairing.Accumulate);

    [Benchmark]
    public int Forest() =>
      CanonicalTrees.MegaForest().LeaffixAggregate(_ => 1, SubtreeNodeCount).Sum(pairing => pairing.Accumulate);
  }
}
