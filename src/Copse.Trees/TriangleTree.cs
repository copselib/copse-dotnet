using Copse.Treenumerables;

namespace Copse.Trees
{
  public class TriangleTree : Treenumerable<int, TriangleTreeNodeChildEnumerator>
  {
    public TriangleTree()
      : base(
          nodeContext => new TriangleTreeNodeChildEnumerator(nodeContext.Node == 0 ? nodeContext.Position.Depth + 2 : 0),
          new[] { 0 })
    {
    }
  }
}
