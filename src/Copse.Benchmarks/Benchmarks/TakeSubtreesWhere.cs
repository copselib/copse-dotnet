using Copse;
using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Subtree selection: matched subtrees re-rooted as the result forest. Two cost classes under
  // one name, both measured: the depth-first NARROW arm streams with O(1) state (a matched
  // subtree is a contiguous preorder segment), while the composite arm is THE SCAN SPELLING
  // (2026-08-17): RootfixScan(false, kept-or-match) -> Where(.Accumulate) -> Select(.Node),
  // one SelectWhere driver over the scan engine; the composite DIMENSION-DISPATCHES: its DFT
  // arm takes the bespoke O(1) wrapper, its BFT arm the Where machinery in SUBTREE MODE (the
  // kept-region rule as one prefix read; 2026-08-17). *_Buffered row names are HISTORICAL.
  // alloc collapses wherever the result store dominated (Triangle ~41MB -> ~0.2-0.5MB; Bft
  // chain ~40MB -> ~6MB); composite DFT time DROPS below the buffer (dispatch); composite BFT
  // time rises ~1.5x (per-pull scan+driver vs buffered decode -- the streaming price). The
  // level-1 predicate makes the result nearly the whole tree; the deep predicate on the chain
  // selects one small tail (the narrow arm's suppression path doing almost all the work).
  [MemoryDiagnoser]
  [BenchmarkCategory("Filter", "TakeSubtreesWhere")]
  public class TakeSubtreesWhere
  {
    [Benchmark]
    public void Dft_Triangle_Streamed()
    {
      var selected = ((IDepthFirstTreenumerable<int>)CanonicalTrees.MegaTriangleTree())
        .TakeSubtreesWhere((n, position) => position.Depth == 1);
      selected.Consume();
    }

    [Benchmark]
    public void Dft_Triangle_Buffered()
    {
      var selected = CanonicalTrees.MegaTriangleTree().TakeSubtreesWhere((n, position) => position.Depth == 1);
      selected.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      var selected = CanonicalTrees.MegaTriangleTree().TakeSubtreesWhere((n, position) => position.Depth == 1);
      selected.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_Chain_Streamed_DeepMatch()
    {
      var selected = ((IDepthFirstTreenumerable<int>)CanonicalTrees.MegaChainTree())
        .TakeSubtreesWhere((n, position) => position.Depth == 100_000);
      selected.Consume();
    }

    [Benchmark]
    public void Dft_Chain_Buffered_DeepMatch()
    {
      var selected = CanonicalTrees.MegaChainTree().TakeSubtreesWhere((n, position) => position.Depth == 100_000);
      selected.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Chain_DeepMatch()
    {
      var selected = CanonicalTrees.MegaChainTree().TakeSubtreesWhere((n, position) => position.Depth == 100_000);
      selected.Consume(TreeTraversalStrategy.BreadthFirst);
    }
  }
}
