using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    // All(p) == !Any(!p). (Fixed 2026-07-05: the outer negation was missing, so the operator
    // returned the complement of its name -- "at least one node fails" -- with no test coverage
    // to catch it. Regression-pinned in AllNodesTests.)
    public static async ValueTask<bool> AllNodesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default)
    {
      return !await source.AnyNodesAsync(nodeContext => !predicate(nodeContext), treeTraversalStrategy).ConfigureAwait(false);
    }

    public static async ValueTask<bool> AllNodesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => !await source.AnyNodesAsync(nodeContext => !predicate(nodeContext)).ConfigureAwait(false);

    public static async ValueTask<bool> AllNodesAsync<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => !await source.AnyNodesAsync(nodeContext => !predicate(nodeContext)).ConfigureAwait(false);
  }
}
