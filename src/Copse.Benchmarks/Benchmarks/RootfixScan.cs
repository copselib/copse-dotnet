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
      .RootfixScan(0, (accumulate, _) => accumulate + 1)
      .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle() =>
      CanonicalTrees.MegaTriangleTree()
      .RootfixScan(0, (accumulate, _) => accumulate + 1)
      .Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Chain() =>
      CanonicalTrees.MegaChainTree()
      .RootfixScan(0, (accumulate, _) => accumulate + 1)
      .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Chain() =>
      CanonicalTrees.MegaChainTree()
      .RootfixScan(0, (accumulate, _) => accumulate + 1)
      .Consume(TreeTraversalStrategy.BreadthFirst);

    // The composed-projection witnesses (SELECT_INTO_CAPTURES_DESIGN.md; seeded 2026-08-16
    // while this spelling STACKS a Select wrapper over the scan's treenumerator -- RootfixScan
    // streams, so unlike the leaffix witnesses the expected step is TIME-ONLY: composition
    // removes one per-pull wrapper hop, and nothing is stored either way). The spelling never
    // changes; when the scan claims the STREAMING projection citizenship, the same call's
    // route fuses. Same-run ratio against Dft/Bft_Chain prices the projection wrapper.
    [Benchmark]
    public void Select_Accumulate_Dft_Chain() =>
      CanonicalTrees.MegaChainTree()
      .RootfixScan(0, (accumulate, _) => accumulate + 1)
      .Select(x => x.Accumulate)
      .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Select_Accumulate_Bft_Chain() =>
      CanonicalTrees.MegaChainTree()
      .RootfixScan(0, (accumulate, _) => accumulate + 1)
      .Select(x => x.Accumulate)
      .Consume(TreeTraversalStrategy.BreadthFirst);
  }
}
