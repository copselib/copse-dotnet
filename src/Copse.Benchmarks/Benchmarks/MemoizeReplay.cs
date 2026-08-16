using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // MEMOIZE REPLAY -- reading a settled memo back: the chunked-store replay paths
  // (RefAppendOnlyList-indexed reads over the memo's own resumable capture). NEW 2026-08-16:
  // this path had no coverage after the 2026-08-10 divergence -- the old replay grid's setup
  // called Materialize, so when Materialize stopped being Memoize + Complete, the memo's
  // replay silently stopped being measured. These rows are MaterializeReplay's grid over the
  // OTHER product; the side-by-side pair puts a continuous number on chunked-vs-flat reads
  // (the trade the lazy rewrite made, until now only measurable by archaeology).
  //
  // The memo is settled in setup (both dimensions consumed pin and complete the one capture),
  // so the timed rows measure replay only; the feed is retired, disposal is vacuous.
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer", "MemoizeReplay")]
  public class MemoizeReplay
  {
    private ITreenumerable<int> _PreorderMemo;
    private ITreenumerable<int> _LevelOrderMemo;

    [GlobalSetup]
    public void Setup()
    {
      // The first consume pins the memo's capture layout (DFT-first -> preorder,
      // BFT-first -> level-order) and completes it.
      _PreorderMemo = CanonicalTrees.MegaTriangleTree().Memoize();
      _PreorderMemo.Consume(TreeTraversalStrategy.DepthFirst);

      _LevelOrderMemo = CanonicalTrees.MegaTriangleTree().Memoize();
      _LevelOrderMemo.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_over_Preorder()
      => _PreorderMemo.Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_over_Preorder()
      => _PreorderMemo.Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Bft_over_LevelOrder()
      => _LevelOrderMemo.Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_over_LevelOrder()
      => _LevelOrderMemo.Consume(TreeTraversalStrategy.DepthFirst);
  }
}
