using Copse.Core;

namespace Copse.Linq.Extensions
{
  /// <summary>Helpers for reshaping <see cref="NodeVisit{TNode}"/> values.</summary>
  public static class NodeVisitExtensions
  {
    /// <summary>The same visit event carrying <paramref name="node"/> in place of the
    /// original node -- mode, visit count, and position unchanged.</summary>
    public static NodeVisit<TResult> WithNode<TSource, TResult>(
      this NodeVisit<TSource> visit,
      TResult node)
    {
      return
        new NodeVisit<TResult>(
          node,
          visit.VisitCount,
          visit.Position);
    }

    /// <summary>The visit's node and position as a <see cref="NodeAndPosition{TNode}"/>, dropping
    /// the mode and visit count.</summary>
    public static NodeAndPosition<TNode> ToNodeAndPosition<TNode>(this NodeVisit<TNode> visit)
    {
      return
        new NodeAndPosition<TNode>(
          visit.Node,
          visit.Position);
    }
  }
}
