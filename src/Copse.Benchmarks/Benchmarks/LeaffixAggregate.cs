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
    // Subtree node count on the dual shape: seed 0 at the fringe, edge-sum the children's
    // counts, node accumulator adds one for the node itself.
    private static int EdgeSum(int left, int right)
      => left + right;

    private static int CountNode(int accumulate, int node)
      => accumulate + 1;

    [Benchmark]
    public int Triangle() =>
      CanonicalTrees.MegaTriangleTree().LeaffixAggregate(0, EdgeSum, CountNode).Sum(pairing => pairing.Accumulate);

    [Benchmark]
    public int Chain() =>
      CanonicalTrees.MegaChainTree().LeaffixAggregate(0, EdgeSum, CountNode).Sum(pairing => pairing.Accumulate);

    [Benchmark]
    public int Forest() =>
      CanonicalTrees.MegaForest().LeaffixAggregate(0, EdgeSum, CountNode).Sum(pairing => pairing.Accumulate);
  }
}
