using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // MATERIALIZE REPLAY -- reading a materialized capture back: the flat-store decode paths
  // over completed array stores. Native rows ride the capture in its own dimension; cross
  // rows ride it in the other (the four-case rule's case 2); each native/cross pair over the
  // same capture isolates the locality tax.
  //
  // Provenance (2026-08-16): these four rows lived in the Memoize class from the era when
  // Materialize WAS Memoize + Complete -- one artifact answered both replay questions. The
  // 2026-08-10 lazy rewrite split the products (Materialize builds flat array stores; a memo
  // keeps its chunked stores), the rows silently followed Materialize's product, and the
  // memo's own replay path fell out of coverage (MemoizeReplay re-covers it). Moved and
  // renamed here, history carried in gh-pages and Bencher. The original class comment posed
  // these rows as "the RefAppendOnlyList-indexing-vs-raw-array question"; the 2026-08-16
  // dashboard answered it -- raw arrays by 25-35% on same silicon.
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer", "MaterializeReplay")]
  public class MaterializeReplay
  {
    private ITreenumerable<int> _PreorderCapture;
    private ITreenumerable<int> _LevelOrderCapture;

    [GlobalSetup]
    public void Setup()
    {
      _PreorderCapture = CanonicalTrees.MegaTriangleTree().Materialize(BufferLayout.Preorder);
      _LevelOrderCapture = CanonicalTrees.MegaTriangleTree().Materialize(BufferLayout.LevelOrder);

      // Materialize is deferred (2026-08-10): settle the captures here so the timed rows keep
      // measuring replay only.
      _PreorderCapture.Consume(TreeTraversalStrategy.DepthFirst);
      _LevelOrderCapture.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_over_Preorder()
      => _PreorderCapture.Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_over_Preorder()
      => _PreorderCapture.Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Bft_over_LevelOrder()
      => _LevelOrderCapture.Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_over_LevelOrder()
      => _LevelOrderCapture.Consume(TreeTraversalStrategy.DepthFirst);
  }
}
