using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // The capture (build) path only: drive the source once into a completed buffer and discard
  // it -- replay cost lives in the Memoize benchmarks. Both capture dimensions, per the
  // buffer-producer rule (capture layout differs by dimension).
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer", "Materialize")]
  public class Materialize
  {
    [Benchmark]
    public ITreenumerable<int> DftCapture_Triangle()
      => CanonicalTrees.MegaTriangleTree().Materialize(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public ITreenumerable<int> BftCapture_Triangle()
      => CanonicalTrees.MegaTriangleTree().Materialize(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public ITreenumerable<int> DftCapture_Chain()
      => CanonicalTrees.MegaChainTree().Materialize(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public ITreenumerable<int> BftCapture_Chain()
      => CanonicalTrees.MegaChainTree().Materialize(TreeTraversalStrategy.BreadthFirst);
  }
}
