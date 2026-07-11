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
    private static int SubtreeNodeCount(NodeContext<int> nodeContext, ChildAccumulations<int> children)
    {
      var count = 1;
      foreach (var child in children)
        count += child;
      return count;
    }

    [Benchmark]
    public int Triangle() =>
      CanonicalTrees.MegaTriangleTree().LeaffixAggregate(SubtreeNodeCount, _ => 1).Sum();

    [Benchmark]
    public int Chain() =>
      CanonicalTrees.MegaChainTree().LeaffixAggregate(SubtreeNodeCount, _ => 1).Sum();

    [Benchmark]
    public int Forest() =>
      CanonicalTrees.MegaForest().LeaffixAggregate(SubtreeNodeCount, _ => 1).Sum();
  }
}
