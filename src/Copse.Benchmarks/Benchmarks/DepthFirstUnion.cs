using Copse.Core;
using Copse.Linq;
using Copse.Trees;
using BenchmarkDotNet.Attributes;
using System.Linq;

namespace Copse.Benchmarks
{
  // Benchmarks for the depth-first StructuralMerge engine (the engine behind Union and, transitively,
  // Intersection/Subtract/SymmetricDifference). Unlike the single-input operators, merge has TWO inputs,
  // so the cost drivers it adds are the OVERLAP RATIO (positions present in both operands -> matched
  // MergeNode pairs, vs present in one -> single-sided nodes driven one side at a time) and SHAPE
  // MISMATCH, on top of size/depth/width. Mirrors the DepthFirstWhere benchmark style.
  [MemoryDiagnoser]
  [BenchmarkCategory("LINQ", "Merge", "DepthFirst")]
  public class DepthFirstUnion
  {
    // Full overlap, bushy: every position exists in both operands, so every MergeNode is
    // HasLeftAndRight (the matched-pair / lockstep-in-step path).
    [Benchmark]
    public void Union_IdenticalTriangleTrees_1448()
      => new TriangleTree().PruneAfter(nodeContext => nodeContext.Position.Depth == 1448)
        .Union(new TriangleTree().PruneAfter(nodeContext => nodeContext.Position.Depth == 1448))
        .Consume(TreeTraversalStrategy.DepthFirst);

    // Full overlap, flat forest: stresses root-frontier handling with a fully-matched million roots.
    [Benchmark]
    public void Union_TrivialForests_1M()
      => Enumerable.Range(0, 1_000_000).ToTrivialForest()
        .Union(Enumerable.Range(0, 1_000_000).ToTrivialForest())
        .Consume(TreeTraversalStrategy.DepthFirst);

    // Full overlap, deep chain: stresses the deep path state with a fully-matched million-deep spine.
    [Benchmark]
    public void Union_DegenerateTrees_1M()
      => Enumerable.Range(0, 1_000_000).ToDegenerateTree()
        .Union(Enumerable.Range(0, 1_000_000).ToDegenerateTree())
        .Consume(TreeTraversalStrategy.DepthFirst);

    // Shape mismatch: a wide binary tree vs a deep chain. They overlap only along the (0, depth) spine;
    // most nodes are single-sided and there is a long one-sided tail -- the "advance the side that is
    // behind" path that an in-step full-overlap merge never exercises.
    [Benchmark]
    public void Union_WideVsDeep_1M()
      => Treenumerables.GetWideTree(20)
        .Union(Enumerable.Range(0, 1_000_000).ToDegenerateTree())
        .Consume(TreeTraversalStrategy.DepthFirst);

    // Partial overlap: the first half of the larger forest is matched, the tail is single-sided.
    [Benchmark]
    public void Union_ForestVsHalfForest_1M()
      => Enumerable.Range(0, 1_000_000).ToTrivialForest()
        .Union(Enumerable.Range(0, 500_000).ToTrivialForest())
        .Consume(TreeTraversalStrategy.DepthFirst);
  }
}
