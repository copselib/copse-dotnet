using Copse.Core;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static bool AllNodes<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default)
    {
      return source.AnyNodes(nodeContext => !predicate(nodeContext), treeTraversalStrategy);
    }

    // NOTE: mirrors the ITreenumerable overload's composition verbatim (including its missing
    // outer negation -- flagged for review 2026-07-04); fix all three together.
    public static bool AllNodes<TNode>(
      this IDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => source.AnyNodes(nodeContext => !predicate(nodeContext));

    public static bool AllNodes<TNode>(
      this IBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => source.AnyNodes(nodeContext => !predicate(nodeContext));
  }
}
