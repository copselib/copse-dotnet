using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Root-to-leaves cumulative scan (the LeaffixScan dual): each node's accumulation depends
  // only on its ancestors, so the scan STREAMS -- no buffer -- and drains both dimensions.
  // The accumulator computes each node's depth from its parent's.
  [MemoryDiagnoser]
  [BenchmarkCategory("Aggregate", "Rootfix")]
  public class RootfixScan
  {
    [Benchmark]
    public void Dft_Triangle() =>
      CanonicalTrees.MegaTriangleTree()
      .RootfixScan((accumulation, _) => accumulation.Node + 1, 0)
      .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle() =>
      CanonicalTrees.MegaTriangleTree()
      .RootfixScan((accumulation, _) => accumulation.Node + 1, 0)
      .Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Chain() =>
      CanonicalTrees.MegaChainTree()
      .RootfixScan((accumulation, _) => accumulation.Node + 1, 0)
      .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Chain() =>
      CanonicalTrees.MegaChainTree()
      .RootfixScan((accumulation, _) => accumulation.Node + 1, 0)
      .Consume(TreeTraversalStrategy.BreadthFirst);
  }
}
