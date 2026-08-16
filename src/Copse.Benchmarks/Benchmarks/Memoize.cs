using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // MEMOIZE CONSTRUCTION -- building the memo's capture: the first pass (capture interleaved
  // with the replay machinery -- distinct from Materialize, which drives the feed directly
  // with no replay in the loop) and the bounded-prefix laziness claim. Reading a settled memo
  // back is MemoizeReplay's job (the four-class taxonomy, 2026-08-16: {Materialize, Memoize}
  // x {construction, replay}). The replay grid that lived here until 2026-08-16 measured
  // MATERIALIZE's product (its setup called Materialize, from the era when Materialize WAS
  // Memoize + Complete) -- it moved to MaterializeReplay with its history.
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer", "Memoize")]
  public class Memoize
  {
    // The breadth-first row is the only end-to-end exercise of the level-order builder under
    // load.

    [Benchmark]
    public void FirstPass_Dft_Triangle()
    {
      ITreenumerable<int> memo = CanonicalTrees.MegaTriangleTree().Memoize();
      memo.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void FirstPass_Bft_Triangle()
    {
      ITreenumerable<int> memo = CanonicalTrees.MegaTriangleTree().Memoize();
      memo.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    // --- Laziness: a bounded prefix over an UNBOUNDED source captures only what the replay
    // touches (the unpruned TriangleTree, deliberately outside the canonical tiers -- the whole
    // point is that no tier bounds it). The allocation column is the claim being tested; the
    // prefix is 2^19 pulls so the time column also clears the noise floor.

    [Benchmark]
    public void Partial_Bft_512K_of_UnboundedTriangle()
    {
      var memo = new Copse.Trees.TriangleTree().Memoize();

      using (var replay = memo.GetBreadthFirstTreenumerator())
        for (var i = 0; i < 1 << 19; i++)
          if (!replay.MoveNext(NodeTraversalStrategies.TraverseAll))
            break;
    }
  }
}
