using Copse.Linq;
using Copse.Trees;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  [MemoryDiagnoser]
  [BenchmarkCategory("Traversal", "Preorder")]
  public class PreorderTraversal
  {
    [Benchmark]
    public void DeepTree() =>
      Treenumerables
      .GetDeepTree(20)
      .PreorderTraversal()
      .Consume();

    [Benchmark]
    public void TriangleTree_PruneAfter_1447() =>
      new TriangleTree()
      .PruneAfter(nodeContext => nodeContext.Position.Depth == 1447)
      .PreorderTraversal()
      .Consume();

    [Benchmark]
    public void CompleteBinaryTree_PruneBefore_20() =>
      new CompleteBinaryTree()
      .PruneBefore(nodeContext => nodeContext.Position.Depth == 20)
      .PreorderTraversal()
      .Consume();

    [Benchmark]
    public void CompleteBinaryTree_PruneAfterDepth_19() =>
      new CompleteBinaryTree()
      .PruneAfter(nodeContext => nodeContext.Position.Depth == 19)
      .PreorderTraversal()
      .Consume();
  }
}
