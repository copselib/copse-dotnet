using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Covers the memoize surfaces the Materialize benchmarks do NOT: Materialize measures the
  // capture (build) path and discards the result; these measure reading captures back. The
  // native/cross-order replay pairs put a number on the accepted locality tax, and the replay
  // rows are the RefAppendOnlyList-indexing-vs-raw-array question for the second-pass hot path.
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer", "Memoize")]
  public class Memoize
  {
    // Consume is MECHANICAL (walks a treenumerator unconditionally, buffers included), so
    // these rows measure exactly what their names say: the replay traversal over a capture.
    private ITreenumerable<int> _DftCapture;
    private ITreenumerable<int> _BftCapture;

    [GlobalSetup]
    public void Setup()
    {
      _DftCapture = CanonicalTrees.MegaTriangleTree().Materialize(TreeTraversalStrategy.DepthFirst);
      _BftCapture = CanonicalTrees.MegaTriangleTree().Materialize(TreeTraversalStrategy.BreadthFirst);
    }

    // --- Second pass: replay a completed capture. Native rows ride the capture in its own
    // dimension; cross rows ride it in the other (the four-case rule's case 2). Each
    // native/cross pair over the same capture isolates the locality tax.

    [Benchmark]
    public void Replay_Dft_over_DftCapture()
      => _DftCapture.Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Replay_Bft_over_DftCapture()
      => _DftCapture.Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Replay_Bft_over_BftCapture()
      => _BftCapture.Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Replay_Dft_over_BftCapture()
      => _BftCapture.Consume(TreeTraversalStrategy.DepthFirst);

    // --- First pass THROUGH a replay: capture interleaved with the replay machinery (case 3)
    // -- distinct from Materialize, which drives the feed directly with no replay in the loop.
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
