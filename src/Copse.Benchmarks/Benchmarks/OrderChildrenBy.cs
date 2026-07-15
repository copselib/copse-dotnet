using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // OrderChildrenBy is Invert generalized (the mirror is OrderChildrenByDescending over the
  // source sibling index), so these rows mirror the Invert family exactly -- same trees, same
  // drains -- and the same-run ratio against Invert's rows is the SORT PREMIUM: what the
  // keyed capture plus per-group stable sort costs over the zero-allocation reversal.
  // Triangle is the sort-heavy pole (1448-child groups), Chain the group-machinery pole (a
  // million single-child groups -- nothing to sort, all bookkeeping).
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer", "OrderChildrenBy")]
  public class OrderChildrenBy
  {
    [Benchmark]
    public void Dft_Triangle()
    {
      ITreenumerable<int> ordered = CanonicalTrees.MegaTriangleTree().OrderChildrenBy(nodeContext => nodeContext.Node);
      ordered.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      ITreenumerable<int> ordered = CanonicalTrees.MegaTriangleTree().OrderChildrenBy(nodeContext => nodeContext.Node);
      ordered.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_Chain()
    {
      ITreenumerable<int> ordered = CanonicalTrees.MegaChainTree().OrderChildrenBy(nodeContext => nodeContext.Node);
      ordered.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Chain()
    {
      ITreenumerable<int> ordered = CanonicalTrees.MegaChainTree().OrderChildrenBy(nodeContext => nodeContext.Node);
      ordered.Consume(TreeTraversalStrategy.BreadthFirst);
    }
  }

  // The breadth-first-narrow entry: the streaming level-permutation build (one source walk,
  // one buffered level, level-order result). Bft rows drain the result's NATIVE layout; Dft
  // rows price the cross-order decode on top. The same-run ratio against the class above is
  // capture-and-emit (preorder path) vs stream-into-encoding (level-order path).
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer", "OrderChildrenBy")]
  public class OrderChildrenByBreadthFirstEntry
  {
    [Benchmark]
    public void Bft_Triangle()
    {
      var narrowSource = (IBreadthFirstTreenumerable<int>)CanonicalTrees.MegaTriangleTree();
      narrowSource.OrderChildrenBy(nodeContext => nodeContext.Node).Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_Triangle()
    {
      var narrowSource = (IBreadthFirstTreenumerable<int>)CanonicalTrees.MegaTriangleTree();
      narrowSource.OrderChildrenBy(nodeContext => nodeContext.Node).Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Chain()
    {
      var narrowSource = (IBreadthFirstTreenumerable<int>)CanonicalTrees.MegaChainTree();
      narrowSource.OrderChildrenBy(nodeContext => nodeContext.Node).Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_Chain()
    {
      var narrowSource = (IBreadthFirstTreenumerable<int>)CanonicalTrees.MegaChainTree();
      narrowSource.OrderChildrenBy(nodeContext => nodeContext.Node).Consume(TreeTraversalStrategy.DepthFirst);
    }
  }
}
