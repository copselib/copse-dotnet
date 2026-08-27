using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The full depth-first visit stream (every scheduling/visiting visit), with a per-node
    /// strategy selector. Value flavor primary; the positional flavor is the arity split (the
    /// Select/Where grammar).
    /// </summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetDepthFirstTraversal<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncDepthFirstTreenumerator, nodeContext => nodeTraversalStrategiesSelector(nodeContext.Node), cancellationToken);
    }

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetDepthFirstTraversal<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncDepthFirstTreenumerator, nodeContext => nodeTraversalStrategiesSelector(nodeContext.Node, nodeContext.Position), cancellationToken);
    }

    /// <summary>The full depth-first visit stream (TraverseAll).</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetDepthFirstTraversal<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncDepthFirstTreenumerator, cancellationToken);
    }
  }
}
