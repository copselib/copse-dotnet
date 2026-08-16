using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // MATERIALIZE CONSTRUCTION -- the capture (build) path only: drive the source once into a
  // completed buffer and discard it. Reading the capture back is MaterializeReplay's job (the
  // four-class taxonomy, 2026-08-16: {Materialize, Memoize} x {construction, replay}, one
  // class per question). Both capture layouts, per the buffer-producer rule.
  //
  // Row names speak the operator's own vocabulary (renamed 2026-08-16, history carried in
  // gh-pages and Bencher): the declared BufferLayout plus the tree -- Preorder_Triangle is
  // Materialize(BufferLayout.Preorder) on the mega triangle, forced by one native-dimension
  // pull. The old DftCapture/BftCapture spellings named a capture flavor instead of the
  // call, which is how the replay grid ended up mislabeled for a week.
  //
  // Materialize is DEFERRED (2026-08-10): the call itself builds nothing, so each row forces
  // the build with a single pull in the capture's own dimension -- the deferred build runs
  // whole at the first pull, so the row keeps measuring the build path (plus one O(1) pull),
  // and the Bencher series keeps its meaning.
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer", "Materialize")]
  public class Materialize
  {
    private ITreenumerable<int> _PreorderCapture;
    private ITreenumerable<int> _LevelOrderCapture;

    [GlobalSetup]
    public void Setup()
    {
      // The transpose rows' sources: settled captures, so those rows time the transpose
      // capture alone -- a COUNTED source (the completed store knows its length), unlike the
      // engine sources above, whose length no capture can know in advance. Seeded 2026-08-16
      // ahead of the presize fast-path so its 2n -> 1n build-allocation step lands on rows
      // that can see it (the engine-source rows correctly cannot -- unknown length keeps the
      // chunked build buffer).
      _PreorderCapture = CanonicalTrees.MegaTriangleTree().Materialize(BufferLayout.Preorder);
      _LevelOrderCapture = CanonicalTrees.MegaTriangleTree().Materialize(BufferLayout.LevelOrder);
      _PreorderCapture.Consume(TreeTraversalStrategy.DepthFirst);
      _LevelOrderCapture.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public ITreenumerable<int> Preorder_Triangle()
      => ForceBuild(CanonicalTrees.MegaTriangleTree().Materialize(BufferLayout.Preorder), TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public ITreenumerable<int> LevelOrder_Triangle()
      => ForceBuild(CanonicalTrees.MegaTriangleTree().Materialize(BufferLayout.LevelOrder), TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public ITreenumerable<int> Preorder_Chain()
      => ForceBuild(CanonicalTrees.MegaChainTree().Materialize(BufferLayout.Preorder), TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public ITreenumerable<int> LevelOrder_Chain()
      => ForceBuild(CanonicalTrees.MegaChainTree().Materialize(BufferLayout.LevelOrder), TreeTraversalStrategy.BreadthFirst);

    // The transpose pair: a mismatched declared layout re-captures FROM the settled buffer
    // (the surface map's transpose clause). The source replay is flat-store decode; the row's
    // cost is the transpose capture build.
    [Benchmark]
    public ITreenumerable<int> Preorder_from_LevelOrder()
      => ForceBuild(_LevelOrderCapture.Materialize(BufferLayout.Preorder), TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public ITreenumerable<int> LevelOrder_from_Preorder()
      => ForceBuild(_PreorderCapture.Materialize(BufferLayout.LevelOrder), TreeTraversalStrategy.BreadthFirst);

    private static ITreenumerable<int> ForceBuild(ITreenumerable<int> buffer, TreeTraversalStrategy strategy)
    {
      using (var treenumerator = buffer.GetTreenumerator(strategy))
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);

      return buffer;
    }
  }
}
