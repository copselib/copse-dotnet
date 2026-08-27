using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    // All(p) == !Any(!p). (Fixed 2026-07-05: the outer negation was missing, so the operator
    // returned the complement of its name -- "at least one node fails" -- with no test coverage
    // to catch it. Regression-pinned in AllNodesTests.) Value flavor primary; positional is
    // the arity-split (the Select/Where grammar, swept family-wide 2026-08-05).
    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static async ValueTask<bool> AllNodesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default,
      CancellationToken cancellationToken = default)
      => !await source.AnyNodesAsync(node => !predicate(node), treeTraversalStrategy, cancellationToken).ConfigureAwait(false);

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static async ValueTask<bool> AllNodesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default,
      CancellationToken cancellationToken = default)
      => !await source.AnyNodesAsync((node, position) => !predicate(node, position), treeTraversalStrategy, cancellationToken).ConfigureAwait(false);

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static async ValueTask<bool> AllNodesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      CancellationToken cancellationToken = default)
      => !await source.AnyNodesAsync(node => !predicate(node), cancellationToken).ConfigureAwait(false);

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static async ValueTask<bool> AllNodesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      CancellationToken cancellationToken = default)
      => !await source.AnyNodesAsync((node, position) => !predicate(node, position), cancellationToken).ConfigureAwait(false);

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static async ValueTask<bool> AllNodesAsync<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      CancellationToken cancellationToken = default)
      => !await source.AnyNodesAsync(node => !predicate(node), cancellationToken).ConfigureAwait(false);

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static async ValueTask<bool> AllNodesAsync<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      CancellationToken cancellationToken = default)
      => !await source.AnyNodesAsync((node, position) => !predicate(node, position), cancellationToken).ConfigureAwait(false);
  }
}
