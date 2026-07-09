using Copse.Treenumerables;

namespace Copse.Trees
{
  /// <summary>
  /// Interesting fact about this tree is that the width of the levels are
  /// related to Pascal's triangle.
  ///
  /// For example:
  ///
  /// If the depth is 3, the width of the levels are 1, 2, 1, which corresponds to the
  /// third row of Pascal's triangle.
  ///
  /// If the depth is 5, the width of the levels are 1, 4, 6, 4, 1, which corresponds to the
  /// fifth row of Pascal's triangle.
  /// </summary>
  public class NDecrementTree : Treenumerable<int, NDecrementTreeNodeChildEnumerator>
  {
    public NDecrementTree(int depth)
      : base(
          nodeContext => new NDecrementTreeNodeChildEnumerator(nodeContext.Node),
          new[] { depth - 1 })
    {
    }
  }
}
