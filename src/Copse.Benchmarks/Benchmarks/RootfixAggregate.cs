using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;
using System.Linq;

namespace Copse.Benchmarks
{
  // Root-to-leaves aggregation to a flat sequence (the LeaffixAggregate dual; dimension-locked).
  // The accumulator computes each node's depth from its parent's.
  [MemoryDiagnoser]
  [BenchmarkCategory("Aggregate", "Rootfix")]
  public class RootfixAggregate
  {
    [Benchmark]
    public int Triangle() =>
      CanonicalTrees.MegaTriangleTree()
      .RootfixAggregate((accumulation, _) => accumulation.Node + 1, 0)
      .Sum();

    [Benchmark]
    public int Chain() =>
      CanonicalTrees.MegaChainTree()
      .RootfixAggregate((accumulation, _) => accumulation.Node + 1, 0)
      .Sum();

    [Benchmark]
    public int Forest() =>
      CanonicalTrees.MegaForest()
      .RootfixAggregate((accumulation, _) => accumulation.Node + 1, 0)
      .Sum();
  }
}
