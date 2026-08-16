using Copse;
using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Root-to-leaves survey pass: two-pass buffer producer (structure DFS, then top-down surveys
  // over the flat encoding), so per the buffer-producer rule the result drains both dimensions.
  // Typed as plain trees for the same overload-resolution reason as the Memoize replays.
  [MemoryDiagnoser]
  [BenchmarkCategory("Aggregate", "Rootfix")]
  public class RootfixDispatch
  {
    // Depth decoration: every member receives its family's arrival + 1 (the unified
    // subject-less survey; the virtual root family is surveyed too, so roots sit at depth 1).
    private static void DispatchDepth(int arrival, DispatchTargets<int, int> children)
    {
      foreach (var child in children)
        child.Dispatch(arrival + 1);
    }

    [Benchmark]
    public void Dft_Triangle()
    {
      ITreenumerable<NodeArrival<int, int>> dispatch = CanonicalTrees.MegaTriangleTree().RootfixDispatch(0, DispatchDepth);
      dispatch.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      ITreenumerable<NodeArrival<int, int>> dispatch = CanonicalTrees.MegaTriangleTree().RootfixDispatch(0, DispatchDepth);
      dispatch.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_Chain()
    {
      ITreenumerable<NodeArrival<int, int>> dispatch = CanonicalTrees.MegaChainTree().RootfixDispatch(0, DispatchDepth);
      dispatch.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Chain()
    {
      ITreenumerable<NodeArrival<int, int>> dispatch = CanonicalTrees.MegaChainTree().RootfixDispatch(0, DispatchDepth);
      dispatch.Consume(TreeTraversalStrategy.BreadthFirst);
    }
  }
}
