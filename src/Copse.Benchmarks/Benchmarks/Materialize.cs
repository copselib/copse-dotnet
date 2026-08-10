using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // The capture (build) path only: drive the source once into a completed buffer and discard
  // it -- replay cost lives in the Memoize benchmarks. Both capture dimensions, per the
  // buffer-producer rule (capture layout differs by dimension).
  //
  // Materialize is DEFERRED (2026-08-10): the call itself builds nothing, so each row forces
  // the build with a single pull in the capture's own dimension -- the deferred build runs
  // whole at the first pull, so the row keeps measuring the build path (plus one O(1) pull),
  // and the Bencher series keeps its meaning.
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer", "Materialize")]
  public class Materialize
  {
    [Benchmark]
    public ITreenumerable<int> DftCapture_Triangle()
      => ForceBuild(CanonicalTrees.MegaTriangleTree().Materialize(TreeTraversalStrategy.DepthFirst), TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public ITreenumerable<int> BftCapture_Triangle()
      => ForceBuild(CanonicalTrees.MegaTriangleTree().Materialize(TreeTraversalStrategy.BreadthFirst), TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public ITreenumerable<int> DftCapture_Chain()
      => ForceBuild(CanonicalTrees.MegaChainTree().Materialize(TreeTraversalStrategy.DepthFirst), TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public ITreenumerable<int> BftCapture_Chain()
      => ForceBuild(CanonicalTrees.MegaChainTree().Materialize(TreeTraversalStrategy.BreadthFirst), TreeTraversalStrategy.BreadthFirst);

    private static ITreenumerable<int> ForceBuild(ITreenumerable<int> buffer, TreeTraversalStrategy strategy)
    {
      using (var treenumerator = buffer.GetTreenumerator(strategy))
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);

      return buffer;
    }
  }
}
