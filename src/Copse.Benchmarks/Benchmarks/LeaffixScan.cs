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
    // as a stream VENEER, flipped by the citizenship merge, flipped again by THE THIN SHAPE
    // 2026-08-17): the route is now the projected buffer's one counted array map off the
    // scan's completed store -- composed ≈ plain on time; the narrow store is the +8MB step
    // in the Alloc column. Same-run ratio against Dft/Bft_Chain prices the projection.
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
    // upstream Select was a wrapper layer on every capture pull; the door landed with the
    // citizenship merge and survived the thin-shape refactor). The scan unwraps a projection
    // source and its capture walks the un-projected inner raw. Same-run ratio against
    // Dft/Bft_Chain prices what remains of the route.
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

    // The chained-projection witness (the functor law's benchmark row, seeded 2026-08-17):
    // two Selects over the scan's buffer must collapse to ONE product build whatever
    // machinery implements the citizenship -- since the thin shape, ComposeSelect composes
    // the selector and the build stays one map (SELECT_INTO_CAPTURES_DESIGN.md section 4a).
    // The spelling never changes; a route that double-materializes shows here as a step up
    // (the row must stay FLAT against Select_Accumulate_Dft_Chain, time and alloc alike).
    [Benchmark]
    public void Select_Select_Dft_Chain()
    {
      var projected = CanonicalTrees.MegaChainTree()
        .LeaffixScan(LeafCount, EdgeSum, CountNode)
        .Select(x => x.Accumulate)
        .Select(count => count * 2);
      projected.Consume(TreeTraversalStrategy.DepthFirst);
    }

    // The scan-of-scan witness (seeded 2026-08-17 at 231.9ms/272MB: the citizen buffer type
    // missed the span fast path's concrete sniff and the fold ran through the walker
    // probes). The thin-shape refactor healed it -- scans return plain buffers again, and
    // the dispatch tier's probes-at-birth wiring hands the second scan its raw store, so
    // the fold runs the span path (~101ms/108MB local). A step back UP here means a scan
    // result stopped exposing its store.
    [Benchmark]
    public void Twice_Dft_Chain()
    {
      var rescanned = CanonicalTrees.MegaChainTree()
        .LeaffixScan(LeafCount, EdgeSum, CountNode)
        .LeaffixScan(pair => 1, EdgeSum, (accumulate, pair) => accumulate + 1);
      rescanned.Consume(TreeTraversalStrategy.DepthFirst);
    }
  }
}
