using Copse.Core;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<TNode> TakeTrees<TNode>(
      this ITreenumerable<TNode> source,
      int count)
      => source.TakeNodesUntil(
        visit => visit.Position.Depth == 0 && visit.Position.SiblingIndex == count,
        false);

    public static IDepthFirstTreenumerable<TNode> TakeTrees<TNode>(
      this IDepthFirstTreenumerable<TNode> source,
      int count)
      => source.TakeNodesUntil(
        visit => visit.Position.Depth == 0 && visit.Position.SiblingIndex == count,
        false);

    public static IBreadthFirstTreenumerable<TNode> TakeTrees<TNode>(
      this IBreadthFirstTreenumerable<TNode> source,
      int count)
      => source.TakeNodesUntil(
        visit => visit.Position.Depth == 0 && visit.Position.SiblingIndex == count,
        false);
  }
}
