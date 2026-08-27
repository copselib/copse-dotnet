using Copse.Treenumerables;

namespace Copse.Trees
{
  public class CompleteBinaryTree : HierarchicalTreenumerable<int, CompleteBinaryTreeNodeChildEnumerator>
  {
    public CompleteBinaryTree()
      : base(
          nodeContext => new CompleteBinaryTreeNodeChildEnumerator(nodeContext.Node),
          new int[] { 0 })
    {
    }
  }
}
