using Copse.Treenumerables;
using Copse.Core;
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
      Func<NodeAndPosition<TNode>, bool> predicate,
      ITreenumerable<TNode> nodeExpander)
      => ExpandNode(source, predicate, _ => nodeExpander);

    public static ITreenumerable<TNode> ExpandNode<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeAndPosition<TNode>, ITreenumerable<TNode>> nodeExpander)
      => ExpandNode(source, _ => true, nodeExpander);

    public static ITreenumerable<TNode> ExpandNode<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeAndPosition<TNode>, bool> predicate,
      Func<NodeAndPosition<TNode>, ITreenumerable<TNode>> nodeExpander)
      => ExpandNode(source, predicate, nodeExpander, (sourceNodeAndPosition, expandedNodeAndPosition) => expandedNodeAndPosition.Node);

    public static ITreenumerable<TResult> ExpandNode<TSource, TExpandedNode, TResult>(
      this ITreenumerable<TSource> source,
      Func<NodeAndPosition<TSource>, bool> predicate,
      Func<NodeAndPosition<TSource>, ITreenumerable<TExpandedNode>> nodeExpander,
      Func<NodeAndPosition<TSource>, NodeAndPosition<TExpandedNode>, TResult> selector)
      => Tree.Create(
        () => throw new NotImplementedException(),
        () => new ExpandNodesDepthFirstTreenumerator<TSource, TExpandedNode, TResult>(() => source.GetDepthFirstTreenumerator(), predicate, nodeExpander, selector));
  }
}
