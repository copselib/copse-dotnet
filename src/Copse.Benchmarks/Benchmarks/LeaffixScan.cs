using Copse;
using Copse.Core;
using Copse.Linq;
using Copse.Trees;
using BenchmarkDotNet.Attributes;
using System.Linq;

namespace Copse.Benchmarks
{
  [MemoryDiagnoser]
  [BenchmarkCategory("LINQ", "Leaffix")]
  public class LeaffixScan
  {
    // Each node accumulates its own subtree node count from its children's counts.
    private static int SubtreeNodeCount(NodeContext<int> nodeContext, ChildAccumulations<int> children)
    {
      var count = 1;
      foreach (var child in children)
        count += child;
      return count;
    }

    [Benchmark]
    public void TriangleTree_1448()
      => new TriangleTree()
        .PruneAfter(nodeContext => nodeContext.Position.Depth == 1448)
        .LeaffixScan(_ => 1, SubtreeNodeCount).Consume();

    [Benchmark]
    public void DegenerateTree_1M()
      => Enumerable.Range(0, 1_000_000).ToDegenerateTree()
        .LeaffixScan(_ => 1, SubtreeNodeCount).Consume();
  }
}
