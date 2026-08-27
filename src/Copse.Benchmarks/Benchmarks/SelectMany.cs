using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // SELECTMANY -- the pointed bind, measured two ways in one class so the ratios are
  // same-run:
  //  - THE THEOREM ROWS: bind spelled as each derived operator, beside the shipped operator
  //    it reproduces (Return∘g vs Select, Return/Promote vs Where, Return/Drop vs
  //    PruneSubtreesWhere). That ratio is the general bind machine's cost over the bespoke
  //    collapsed machines -- the number the composition question needs (bind as the front
  //    door, the local collapse lattice as its lookup table).
  //  - THE GENERAL ROWS: every node expands to a small forest with the children under its
  //    last root -- the case no existing operator can spell -- across the frame stack's
  //    depth (Chain) and fan-out (Binary, Triangle) profiles. The breadth-first row rides the
  //    composite overload's documented capture, so the capture has a measured price.
  // Allocation columns tell the machine's story: a frame per open source node, the pending
  // queue, and one expansion treenumerator per node (Return's single-node tree included).
  [MemoryDiagnoser]
  [BenchmarkCategory("Streaming", "SelectMany")]
  public class SelectMany
  {
    // A two-root forest, reused as every node's expansion: fresh enumerator per
    // acquisition, nothing shared between nodes but the definition.
    private static readonly ITreenumerable<int> TwoRoots = new[] { 1, 2 }.ToTrivialForest();

    // ------------------------------------------------------------- the theorem rows

    [Benchmark]
    public void Dft_Triangle_Select() =>
      CanonicalTrees.MegaTriangleTree().Select(n => n + 1).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Dft_Triangle_AsSelect() =>
      CanonicalTrees.MegaTriangleTree().SelectMany(n => Expansion.Return(n + 1)).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Dft_Triangle_Where() =>
      CanonicalTrees.MegaTriangleTree().Where(n => n % 2 == 0).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Dft_Triangle_AsWhere() =>
      CanonicalTrees.MegaTriangleTree().SelectMany(n => n % 2 == 0 ? Expansion.Return(n) : Expansion.Promote<int>()).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Dft_Triangle_PruneSubtreesWhere() =>
      CanonicalTrees.MegaTriangleTree().PruneSubtreesWhere(n => n % 7 == 6).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Dft_Triangle_AsPruneSubtreesWhere() =>
      CanonicalTrees.MegaTriangleTree().SelectMany(n => n % 7 == 6 ? Expansion.Drop<int>() : Expansion.Return(n)).Consume(TreeTraversalStrategy.DepthFirst);

    // ------------------------------------------------------------- the general rows

    [Benchmark]
    public void Dft_Triangle_Forest() =>
      CanonicalTrees.MegaTriangleTree().SelectMany(n => Expansion.Of(TwoRoots, SlotPlacement.UnderLastRoot)).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Dft_Chain_Forest() =>
      CanonicalTrees.MegaChainTree().SelectMany(n => Expansion.Of(TwoRoots, SlotPlacement.UnderLastRoot)).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Dft_Binary_Forest() =>
      CanonicalTrees.MegaBinaryTree().SelectMany(n => Expansion.Of(TwoRoots, SlotPlacement.UnderLastRoot)).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle_Forest() =>
      CanonicalTrees.MegaTriangleTree().SelectMany(n => Expansion.Of(TwoRoots, SlotPlacement.UnderLastRoot)).Consume(TreeTraversalStrategy.BreadthFirst);
  }
}
