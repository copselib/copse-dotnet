using Copse.Core;
using Copse.Linq;
using Copse.Trees;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Covers the memoize surfaces the Materialize benchmarks do NOT: Materialize measures the
  // capture (build) path and discards the result; these measure reading captures back. The
  // native/cross-order replay pairs put a number on the accepted locality tax, and the replay
  // rows are the RefAppendOnlyList-indexing-vs-raw-array question for the second-pass hot path.
  [MemoryDiagnoser]
  [BenchmarkCategory("LINQ", "Memoize")]
  public class Memoize
  {
    // Typed as plain trees ON PURPOSE: on ITreenumerableBuffer, Consume(strategy) resolves to
    // the interface member -- a no-op on a completed capture. The plain-tree type resolves to
    // the drain extension, which is the replay traversal being measured.
    private ITreenumerable<int> _DepthFirstCapture;
    private ITreenumerable<int> _BreadthFirstCapture;

    [GlobalSetup]
    public void Setup()
    {
      _DepthFirstCapture = PrunedTriangleTree().Materialize(TreeTraversalStrategy.DepthFirst);
      _BreadthFirstCapture = PrunedTriangleTree().Materialize(TreeTraversalStrategy.BreadthFirst);
    }

    private static ITreenumerable<int> PrunedTriangleTree()
      => new TriangleTree().PruneAfter(nodeContext => nodeContext.Position.Depth == 1448);

    // --- Second pass: replay a completed capture. Native rows ride the capture in its own
    // dimension; cross rows ride it in the other (the four-case rule's case 2). Each
    // native/cross pair over the same capture isolates the locality tax.

    [Benchmark]
    public void Replay_DepthFirst_over_DepthFirstCapture()
      => _DepthFirstCapture.Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Replay_BreadthFirst_over_DepthFirstCapture()
      => _DepthFirstCapture.Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Replay_BreadthFirst_over_BreadthFirstCapture()
      => _BreadthFirstCapture.Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Replay_DepthFirst_over_BreadthFirstCapture()
      => _BreadthFirstCapture.Consume(TreeTraversalStrategy.DepthFirst);

    // --- First pass THROUGH a replay: capture interleaved with the replay machinery (case 3)
    // -- distinct from Materialize, which drives the feed directly with no replay in the loop.
    // The breadth-first row is the only end-to-end exercise of the level-order builder under
    // load.

    [Benchmark]
    public void FirstPass_DepthFirst_TriangleTree_1448()
    {
      ITreenumerable<int> memo = PrunedTriangleTree().Memoize();
      memo.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void FirstPass_BreadthFirst_TriangleTree_1448()
    {
      ITreenumerable<int> memo = PrunedTriangleTree().Memoize();
      memo.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    // --- Laziness: a bounded prefix over the UNPRUNED (unbounded) tree captures only what the
    // replay touches. The allocation column is the claim being tested.

    [Benchmark]
    public void Partial_BreadthFirst_10K_of_unbounded_TriangleTree()
    {
      var memo = new TriangleTree().Memoize();

      using (var replay = memo.GetBreadthFirstTreenumerator())
        for (var i = 0; i < 10_000; i++)
          if (!replay.MoveNext(NodeTraversalStrategies.TraverseAll))
            break;
    }
  }
}
