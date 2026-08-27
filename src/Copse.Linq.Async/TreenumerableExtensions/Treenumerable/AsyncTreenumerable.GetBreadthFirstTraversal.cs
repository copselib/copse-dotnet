using Copse.Core;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The full breadth-first visit stream, with a per-node strategy selector.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncBreadthFirstTreenumerator, nodeContext => nodeTraversalStrategiesSelector(nodeContext.Node), cancellationToken);
    }

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncBreadthFirstTreenumerator, nodeContext => nodeTraversalStrategiesSelector(nodeContext.Node, nodeContext.Position), cancellationToken);
    }

    /// <summary>The full breadth-first visit stream (TraverseAll).</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncBreadthFirstTreenumerator, cancellationToken);
    }
  }
}
