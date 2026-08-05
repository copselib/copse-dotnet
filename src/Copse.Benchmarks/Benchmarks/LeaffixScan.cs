using Copse;
using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Leaves-to-root cumulative scan, fold tier (sugar over LeaffixDispatch): the delta against
  // the LeaffixDispatch benchmark is the delegation wrapper. A buffer producer, so per the
  // buffer-producer rule the result drains both dimensions. Typed as plain trees for the same
  // overload-resolution reason as the Memoize replays.
  [MemoryDiagnoser]
  [BenchmarkCategory("Aggregate", "Leaffix")]
  public class LeaffixScan
  {
    // Subtree node count on the dual shape: seed 0 at the fringe, edge-sum the children's
    // counts, node accumulator adds one for the node itself.
    private static int EdgeSum(int left, int right)
      => left + right;

    private static int CountNode(int accumulate, int node)
      => accumulate + 1;

    [Benchmark]
    public void Dft_Triangle()
    {
      var scan = CanonicalTrees.MegaTriangleTree().LeaffixScan(0, EdgeSum, CountNode);
      scan.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      var scan = CanonicalTrees.MegaTriangleTree().LeaffixScan(0, EdgeSum, CountNode);
      scan.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_Chain()
    {
      var scan = CanonicalTrees.MegaChainTree().LeaffixScan(0, EdgeSum, CountNode);
      scan.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Chain()
    {
      var scan = CanonicalTrees.MegaChainTree().LeaffixScan(0, EdgeSum, CountNode);
      scan.Consume(TreeTraversalStrategy.BreadthFirst);
    }
  }
}
