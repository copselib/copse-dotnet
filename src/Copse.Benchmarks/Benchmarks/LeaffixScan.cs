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
    // Subtree node count on the dual shape: each leaf counts as one (the canonical count
    // fringe), edge-sum the children's counts, node accumulator adds one for the node itself.
    private static int LeafCount(int node)
      => 1;

    private static int EdgeSum(int left, int right)
      => left + right;

    private static int CountNode(int accumulate, int node)
      => accumulate + 1;

    [Benchmark]
    public void Dft_Triangle()
    {
      var scan = CanonicalTrees.MegaTriangleTree().LeaffixScan(LeafCount, EdgeSum, CountNode);
      scan.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      var scan = CanonicalTrees.MegaTriangleTree().LeaffixScan(LeafCount, EdgeSum, CountNode);
      scan.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_Chain()
    {
      var scan = CanonicalTrees.MegaChainTree().LeaffixScan(LeafCount, EdgeSum, CountNode);
      scan.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Chain()
    {
      var scan = CanonicalTrees.MegaChainTree().LeaffixScan(LeafCount, EdgeSum, CountNode);
      scan.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    // The composed-projection witnesses (SELECT_INTO_CAPTURES_DESIGN.md; seeded 2026-08-16
    // while this spelling is a stream VENEER: the full pair buffer is built underneath and
    // the projection is per-pull). The spelling never changes -- when scan results claim the
    // projection citizenship, the same call's route flips to a composed 1-wide build and
    // this series shows the step. Same-run ratio against Dft/Bft_Chain prices the projection.
    [Benchmark]
    public void Select_Accumulate_Dft_Chain()
    {
      var projected = CanonicalTrees.MegaChainTree().LeaffixScan(LeafCount, EdgeSum, CountNode).Select(x => x.Accumulate);
      projected.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Select_Accumulate_Bft_Chain()
    {
      var projected = CanonicalTrees.MegaChainTree().LeaffixScan(LeafCount, EdgeSum, CountNode).Select(x => x.Accumulate);
      projected.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    // The compose-left witnesses (SELECT_INTO_CAPTURES_DESIGN.md; seeded 2026-08-17 while an
    // upstream Select is a wrapper layer on every capture pull). The spelling never changes;
    // when the scan learns to unwrap a projection source (the compose-left door), the same
    // call's capture walks the un-projected inner raw and this series shows the step.
    // Same-run ratio against Dft/Bft_Chain prices the upstream wrapper.
    [Benchmark]
    public void FromSelect_Dft_Chain()
    {
      var scan = CanonicalTrees.MegaChainTree().Select(node => node * 2).LeaffixScan(LeafCount, EdgeSum, CountNode);
      scan.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void FromSelect_Bft_Chain()
    {
      var scan = CanonicalTrees.MegaChainTree().Select(node => node * 2).LeaffixScan(LeafCount, EdgeSum, CountNode);
      scan.Consume(TreeTraversalStrategy.BreadthFirst);
    }
  }
}
