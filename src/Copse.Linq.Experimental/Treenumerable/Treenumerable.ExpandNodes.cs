using Copse.Treenumerables;
﻿using Copse.Core;
using Copse.Linq.Experimental.Treenumerators.ExpandNodes;
using System;

namespace Copse.Linq.Experimental
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<TNode> ExpandNode<TNode>(
      this ITreenumerable<TNode> source,
      ITreenumerable<TNode> nodeExpander)
      => ExpandNode(source, _ => true, _ => nodeExpander);

    public static ITreenumerable<TNode> ExpandNode<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      ITreenumerable<TNode> nodeExpander)
      => ExpandNode(source, predicate, _ => nodeExpander);

    public static ITreenumerable<TNode> ExpandNode<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeContext<TNode>, ITreenumerable<TNode>> nodeExpander)
      => ExpandNode(source, _ => true, nodeExpander);

    public static ITreenumerable<TNode> ExpandNode<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      Func<NodeContext<TNode>, ITreenumerable<TNode>> nodeExpander)
      => ExpandNode(source, predicate, nodeExpander, (sourceNodeContext, expandedNodeContext) => expandedNodeContext.Node);

    public static ITreenumerable<TResult> ExpandNode<TSource, TExpandedNode, TResult>(
      this ITreenumerable<TSource> source,
      Func<NodeContext<TSource>, bool> predicate,
      Func<NodeContext<TSource>, ITreenumerable<TExpandedNode>> nodeExpander,
      Func<NodeContext<TSource>, NodeContext<TExpandedNode>, TResult> selector)
      => Tree.Create(
        () => throw new NotImplementedException(),
        () => new ExpandNodesDepthFirstTreenumerator<TSource, TExpandedNode, TResult>(() => source.GetDepthFirstTreenumerator(), predicate, nodeExpander, selector));
  }
}
