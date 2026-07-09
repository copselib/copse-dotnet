using Copse.Core;

namespace Copse.Linq.Extensions
{
  public static class NodeVisitExtensions
  {
    public static NodeVisit<TResult> WithNode<TSource, TResult>(
      this NodeVisit<TSource> visit,
      TResult node)
    {
      return
        new NodeVisit<TResult>(
          visit.Mode,
          node,
          visit.VisitCount,
          visit.Position);
    }

    public static NodeContext<TNode> ToNodeContext<TNode>(this NodeVisit<TNode> visit)
    {
      return
        new NodeContext<TNode>(
          visit.Node,
          visit.Position);
    }
  }
}
