using Copse;
using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.Linq.Extensions;
using Copse.Linq.Treenumerators; // WhereDepthFirstPath (internal, via InternalsVisibleTo)
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq.Treenumerators
{
  /// <summary>
  /// The FOURTH CELL's depth-first machine (the ancestor composer, SCAN_TIER_DESIGN.md) and
  /// the codegen source of truth for its sync twin: the filter driver with an inherited-fold
  /// stage. Identical to <see cref="AsyncWhereDepthFirstTreenumerator{TSource, TResult, TResultSelector}"/>
  /// except that scheduling runs the rootfix fold FIRST -- once per scheduled node, accepted
  /// or rejected (composition is data flow: a rejected node's descendants still fold through
  /// its accumulate) -- and the composed selector chain sees the PAIR <c>(node, accumulate)</c>,
  /// minted transiently at the decision site (the emission mint: never stored).
  ///
  /// <para>THE ACCUMULATE TRAIL: a depth-indexed live array -- <c>_AccumulateTrail[d]</c> is
  /// the accumulate context of the CURRENT PATH's inner-depth-d node. One overwrite per
  /// scheduled node, one read for its parent. Trivially coherent under depth-first order (a
  /// node's subtree completes before its next sibling schedules, so slots at or above a
  /// scheduled node's parent depth are always the current path's) -- the DFT analog of the
  /// BFT machinery's skip-prefix carry, carrying a TAccumulate instead of a count. This is
  /// the O(depth) information floor, the same width the scan engine carries.</para>
  ///
  /// <para>Plain Where pays nothing for this machine's existence: it is a TWIN family
  /// (the hard gate, SCAN_TIER_DESIGN.md section 3); the plain driver is byte-identical.</para>
  /// </summary>
  internal sealed class AsyncScanWhereDepthFirstTreenumerator<TSource, TAccumulate, TResult, TResultSelector>
    : AsyncTreenumeratorWrapper<TSource, TResult>
    where TResultSelector : struct, IAsyncResultSelector<NodeAccumulation<TSource, TAccumulate>, TResult>
  {
    public AsyncScanWhereDepthFirstTreenumerator(
      Func<IAsyncTreenumerator<TSource>> innerTreenumeratorFactory,
      Func<NodeContext<TAccumulate>, NodeContext<TSource>, TAccumulate> accumulator,
      TAccumulate seed,
      TResultSelector resultSelector)
      : base(innerTreenumeratorFactory)
    {
      _Accumulator = accumulator;
      _SeedContext = new NodeContext<TAccumulate>(seed, NodePosition.ForestRoot);
      _ResultSelector = resultSelector;

      _Path = new WhereDepthFirstPath<TResult>(default, NodePosition.ForestRoot);
    }

    private readonly Func<NodeContext<TAccumulate>, NodeContext<TSource>, TAccumulate> _Accumulator;
    private readonly NodeContext<TAccumulate> _SeedContext;
    private readonly TResultSelector _ResultSelector;

    // The accumulate trail (see the class doc). The seed context stands in for depth -1 (the
    // virtual forest root), so roots fold from it -- the scan engines' exact boundary.
    private readonly List<NodeContext<TAccumulate>> _AccumulateTrail = new List<NodeContext<TAccumulate>>();

    private WhereDepthFirstPath<TResult> _Path;
    private bool _HasCachedChild = false;

    private NodeTraversalStrategies _PendingResultStrategies = NodeTraversalStrategies.TraverseAll;

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_HasCachedChild)
      {
        _HasCachedChild = false;
        Publish(ref _Path.AcceptedTop());
        return true;
      }

      nodeTraversalStrategies |= _PendingResultStrategies;
      _PendingResultStrategies = NodeTraversalStrategies.TraverseAll;

      if (InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode
        && _Path.AcceptedCount > 1
        && nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Path.MoveLastAcceptedToSkipped();
      }

      if (Mode == TreenumeratorMode.VisitingNode)
        nodeTraversalStrategies = NodeTraversalStrategies.TraverseAll;

      if (InnerTreenumerator.Position.IsForestRoot)
        nodeTraversalStrategies = NodeTraversalStrategies.TraverseAll;

      while (await InnerTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
      {
        nodeTraversalStrategies = NodeTraversalStrategies.TraverseAll;

        if (InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode)
        {
          if (!OnScheduling(out var rejectedStrategies))
          {
            nodeTraversalStrategies = rejectedStrategies;
            continue;
          }

          return true;
        }

        if (OnVisiting())
          return true;
      }

      return false;
    }

    private bool OnScheduling(out NodeTraversalStrategies rejectedStrategies)
    {
      _Path.PopDeeperThanForScheduling(InnerTreenumerator.Position.Depth);

      // THE FOLD STAGE, before the accept/reject decision: every scheduled node folds from
      // its inner parent's accumulate -- rejection is downstream of the fold (data flow),
      // so a rejected node's descendants inherit through it exactly as they would in the
      // two-machine spelling.
      var innerDepth = InnerTreenumerator.Position.Depth;
      var parentContext = innerDepth == 0 ? _SeedContext : _AccumulateTrail[innerDepth - 1];
      var accumulate = _Accumulator(parentContext, InnerTreenumerator.ToNodeContext());

      var accumulateContext = new NodeContext<TAccumulate>(accumulate, InnerTreenumerator.Position);
      if (innerDepth < _AccumulateTrail.Count)
        _AccumulateTrail[innerDepth] = accumulateContext;
      else
        _AccumulateTrail.Add(accumulateContext);

      // ONE evaluation of the composed selector chain, against the PAIR context: the pair
      // is minted here, on the stack, and never stored.
      var result = _ResultSelector.GetResult(
        new NodeContext<NodeAccumulation<TSource, TAccumulate>>(
          new NodeAccumulation<TSource, TAccumulate>(InnerTreenumerator.Node, accumulate),
          InnerTreenumerator.Position));

      if (result.Strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        rejectedStrategies = result.Strategies;
        return false;
      }

      rejectedStrategies = NodeTraversalStrategies.TraverseAll;
      _PendingResultStrategies = result.Strategies;

      var cacheChild = _Path.ShouldCacheChild();

      _Path.PushAcceptedChild(result.Node, InnerTreenumerator.Position);

      if (cacheChild)
      {
        _HasCachedChild = true;
        Publish(ref _Path.TakeParentReturnVisit());
      }
      else
      {
        Publish(ref _Path.AcceptedTop());
      }

      return true;
    }

    private bool OnVisiting()
    {
      _Path.PopDeeperThanForVisiting(
        InnerTreenumerator.Position.Depth,
        out var removedVisitedNodes,
        out var removedSkippedNodes);

      if (_Path.ShouldSuppressVisit(InnerTreenumerator.Position, removedVisitedNodes, removedSkippedNodes))
        return false;

      Publish(ref _Path.TakeCurrentVisit());

      return true;
    }

    private void Publish(ref WhereDepthFirstPath<TResult>.InternalNodeVisit frame)
    {
      Mode = TreenumeratorModes.FromVisitCount(frame.VisitCount);

      _Path.RecordPublished(frame.OriginalPosition.Depth, Mode == TreenumeratorMode.VisitingNode);

      Node = frame.Node;
      VisitCount = frame.VisitCount;
      Position = frame.Position;
    }
  }
}
