using Copse.Core;
using Copse.Linq.Treenumerables;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static void Consume<TNode>(
      this ITreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy = default)
    {
      using (var treenumerator = source.GetTreenumerator(treeTraversalStrategy))
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll));
    }

    // Drive a buffer's capture to completion without naming a dimension: finish whichever
    // dimension's capture is furthest along -- both count toward the same total, so the larger
    // buffered count is the cheaper capture to complete -- with depth-first winning ties (and
    // hence the fresh, nothing-buffered case). A no-op on an already-complete buffer, via the
    // member's invariant. Callers with a layout preference use Consume(TreeTraversalStrategy)
    // directly: declared intent outranks sunk cost. (Overload resolution keeps this and the
    // drain above apart: the buffer receiver is more specific, and the strategy-taking member
    // on ITreenumerableBuffer beats any extension.)
    public static void Consume<TValue>(this ITreenumerableBuffer<TValue> buffer)
      => buffer.Consume(
        buffer.GetBufferedCount(TreeTraversalStrategy.DepthFirst) >= buffer.GetBufferedCount(TreeTraversalStrategy.BreadthFirst)
          ? TreeTraversalStrategy.DepthFirst
          : TreeTraversalStrategy.BreadthFirst);

    public static void Consume<TNode>(this IDepthFirstTreenumerable<TNode> source)
    {
      using (var treenumerator = source.GetDepthFirstTreenumerator())
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) ;
    }

    public static void Consume<TNode>(this IBreadthFirstTreenumerable<TNode> source)
    {
      using (var treenumerator = source.GetBreadthFirstTreenumerator())
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll)) ;
    }
  }
}
