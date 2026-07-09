using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // The mirror over a full source captures into mirrored preorder arrays (the buffer return
  // type discloses the O(n)), so per the buffer-producer rule these drain BOTH dimensions --
  // the pending regime/laziness decision for Invert will show up here as a step change in the
  // Bft rows' allocation column. Typed as plain trees for the same overload-resolution reason
  // as the Memoize replays.
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer", "Invert")]
  public class Invert
  {
    [Benchmark]
    public void Dft_Triangle()
    {
      ITreenumerable<int> mirror = CanonicalTrees.MegaTriangleTree().Invert();
      mirror.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Triangle()
    {
      ITreenumerable<int> mirror = CanonicalTrees.MegaTriangleTree().Invert();
      mirror.Consume(TreeTraversalStrategy.BreadthFirst);
    }

    [Benchmark]
    public void Dft_Chain()
    {
      ITreenumerable<int> mirror = CanonicalTrees.MegaChainTree().Invert();
      mirror.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public void Bft_Chain()
    {
      ITreenumerable<int> mirror = CanonicalTrees.MegaChainTree().Invert();
      mirror.Consume(TreeTraversalStrategy.BreadthFirst);
    }
  }
}
