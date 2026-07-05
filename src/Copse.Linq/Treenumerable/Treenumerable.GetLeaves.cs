using Copse.Core;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static IEnumerable<TNode> GetLeaves<TNode>(this IDepthFirstTreenumerable<TNode> source)
    {
      using (var treenumerator = source.GetDepthFirstTreenumerator())
      {
        if (!treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          yield break;

        TNode previousNode = treenumerator.Node;
        int previousDepth = treenumerator.Position.Depth;

        while (treenumerator.MoveNext(NodeTraversalStrategies.SkipNode))
        {
          if (previousDepth >= treenumerator.Position.Depth)
            yield return previousNode;

          previousNode = treenumerator.Node;
          previousDepth = treenumerator.Position.Depth;
        }

        yield return previousNode;
      }
    }

    // The breadth-first dual, leaves in LEVEL order (the depth-first overload yields them in
    // preorder -- the same per-dimension order difference GetTraversal has). No parent
    // bookkeeping is needed: the visit contract schedules a node's children between that
    // node's own visits while it holds the front, so "leaf" is exactly "first visit not
    // followed by a child schedule" -- one pending slot of state.
    public static IEnumerable<TNode> GetLeaves<TNode>(this IBreadthFirstTreenumerable<TNode> source)
    {
      using (var treenumerator = source.GetBreadthFirstTreenumerator())
      {
        var hasPending = false;
        var pending = default(TNode);

        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
          {
            // The pending first-visited node just scheduled a child: not a leaf.
            hasPending = false;
          }
          else if (treenumerator.VisitCount == 1)
          {
            if (hasPending)
              yield return pending;

            pending = treenumerator.Node;
            hasPending = true;
          }
        }

        if (hasPending)
          yield return pending;
      }
    }

    // With both narrow overloads present, a full ITreenumerable needs its own overload to
    // resolve (neither narrow one is better); it keeps the historical depth-first behavior.
    public static IEnumerable<TNode> GetLeaves<TNode>(this ITreenumerable<TNode> source)
      => GetLeaves((IDepthFirstTreenumerable<TNode>)source);
  }
}
