using Copse.Core;
using Copse.Linq;
using Copse.Trees;
using BenchmarkDotNet.Attributes;
using System.Linq;

namespace Copse.Benchmarks
{
  // Benchmarks for the breadth-first StructuralMerge engine (the engine behind Union and, transitively,
  // Intersection/Subtract/SymmetricDifference). Same scenarios as DepthFirstUnion; the BFT engine is
  // the more interesting target -- it keeps a merged level-frontier queue and owns a manufacture/
  // suppress revisit cadence, so wide/forest inputs (large frontier) and shape-mismatched inputs
  // (single-sided promotion across the queue) are the cases that stress it most.
  [MemoryDiagnoser]
  [BenchmarkCategory("LINQ", "Merge", "BreadthFirst")]
  public class BreadthFirstUnion
  {
    // Full overlap, bushy: every position exists in both operands, so every MergeNode is HasLeftAndRight.
    [Benchmark]
    public void Union_IdenticalTriangleTrees_1448()
      => new TriangleTree().PruneAfter(nodeContext => nodeContext.Position.Depth == 1448)
        .Union(new TriangleTree().PruneAfter(nodeContext => nodeContext.Position.Depth == 1448))
        .Consume(TreeTraversalStrategy.BreadthFirst);

    // Full overlap, flat forest: a fully-matched million-wide root frontier -- the BFT queue's worst case.
    [Benchmark]
    public void Union_TrivialForests_1M()
      => Enumerable.Range(0, 1_000_000).ToTrivialForest()
        .Union(Enumerable.Range(0, 1_000_000).ToTrivialForest())
        .Consume(TreeTraversalStrategy.BreadthFirst);

    // Full overlap, deep chain: narrow frontier but a million-deep matched spine.
    [Benchmark]
    public void Union_DegenerateTrees_1M()
      => Enumerable.Range(0, 1_000_000).ToDegenerateTree()
        .Union(Enumerable.Range(0, 1_000_000).ToDegenerateTree())
        .Consume(TreeTraversalStrategy.BreadthFirst);

    // Shape mismatch: a wide binary tree vs a deep chain. Mostly single-sided nodes plus a long
    // one-sided tail -- exercises the per-level single-sided merge across the frontier queue.
    [Benchmark]
    public void Union_WideVsDeep_1M()
      => Treenumerables.GetWideTree(20)
        .Union(Enumerable.Range(0, 1_000_000).ToDegenerateTree())
        .Consume(TreeTraversalStrategy.BreadthFirst);

    // Partial overlap: the first half of the larger frontier is matched, the tail is single-sided.
    [Benchmark]
    public void Union_ForestVsHalfForest_1M()
      => Enumerable.Range(0, 1_000_000).ToTrivialForest()
        .Union(Enumerable.Range(0, 500_000).ToTrivialForest())
        .Consume(TreeTraversalStrategy.BreadthFirst);
  }
}
