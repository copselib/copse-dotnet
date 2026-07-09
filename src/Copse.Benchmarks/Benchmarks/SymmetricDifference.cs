using Copse.Core;
using Copse.Linq;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // Representative for the non-Union set operations (they share the merge machinery):
  // identical inputs force a full dual traversal whose OUTPUT is empty, so the rows measure
  // pure comparison/merge cost with no emission cost mixed in.
  [MemoryDiagnoser]
  [BenchmarkCategory("Merge", "SetOps")]
  public class SymmetricDifference
  {
    [Benchmark]
    public void Dft_IdenticalTriangles() =>
      CanonicalTrees.MegaTriangleTree().SymmetricDifference(CanonicalTrees.MegaTriangleTree())
        .Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_IdenticalTriangles() =>
      CanonicalTrees.MegaTriangleTree().SymmetricDifference(CanonicalTrees.MegaTriangleTree())
        .Consume(TreeTraversalStrategy.BreadthFirst);
  }
}
