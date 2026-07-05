using Copse.Core;
using Copse.Linq;
using Copse.Trees;
using BenchmarkDotNet.Attributes;
using System.Linq;

namespace Copse.Benchmarks
{
  [MemoryDiagnoser]
  [BenchmarkCategory("LINQ", "Invert")]
  public class Invert
  {
    // The streaming mirror composes for free, so these drain it: a full breadth-first pass over
    // the mirrored tree (the old rows measured compose-time materialization, which no longer
    // exists).
    [Benchmark]
    public void TriangleTree_1448()
      => new TriangleTree()
        .PruneAfter(nodeContext => nodeContext.Position.Depth == 1448)
        .Invert()
        .Consume();

    [Benchmark]
    public void DegenerateTree_1M()
      => Enumerable.Range(0, 1_000_000).ToDegenerateTree().Invert().Consume();
  }
}
