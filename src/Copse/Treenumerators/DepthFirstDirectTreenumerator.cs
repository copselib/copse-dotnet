using Copse.Core;
using Copse.Engine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
// Disambiguate from Copse.Treenumerators' own copy (in DepthFirstPath.cs) of this name.
using EngineBacktrackStep = Copse.Engine.DepthFirstBacktrackStep;

namespace Copse.Treenumerators
{
  /// <summary>
  /// Depth-first sync driver in the <b>direct style</b>: the same natural, inlined control flow as
  /// <see cref="DepthFirstTreenumerator{TValue, TNode, TChildEnumerator}"/> (OnScheduling / OnVisiting /
  /// Backtrack / TryPushNextChild), NOT the inverted cadence -- but over the shared cross-assembly
  /// <see cref="DepthFirstPathState{TNode, TEnumerator}"/> in Copse.Engine.
  ///
  /// <para>This is the shape a codegen'd sync twin would take (a direct-async driver with <c>await</c>
  /// stripped). It exists to price the direct style against the inverted cadence with the assembly
  /// split held constant: Engine-vs-Direct isolates cross-assembly cost; Direct-vs-Cadence isolates the
  /// seam-inversion cost.</para>
  /// </summary>
  public sealed class DepthFirstDirectTreenumerator<TValue, TNode, TChildEnumerator>
    : TreenumeratorBase<TValue>
    where TChildEnumerator : IChildEnumerator<TNode>
  {
    public DepthFirstDirectTreenumerator(
      IEnumerable<TNode> rootNodes,
      Func<NodeContext<TNode>, TChildEnumerator> childEnumeratorFactory,
      Func<TNode, TValue> map)
    {
      _RootsEnumerator = rootNodes.GetEnumerator();
      _Path = new DepthFirstPathState<TNode, TChildEnumerator>(childEnumeratorFactory);
      _Map = map;
    }

    private readonly IEnumerator<TNode> _RootsEnumerator;
    private DepthFirstPathState<TNode, TChildEnumerator> _Path;
    private readonly Func<TNode, TValue> _Map;

    private bool _RootsEnumeratorFinished = false;

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Path.IsEmpty)
        return MoveToNextRootNode();

      if (Mode == TreenumeratorMode.SchedulingNode)
        return OnScheduling(nodeTraversalStrategies);

      return OnVisiting();
    }

    private bool MoveToNextRootNode()
    {
      if (_RootsEnumeratorFinished || !_RootsEnumerator.MoveNext())
        return false;

      Publish(ref _Path.PushRoot(_RootsEnumerator.Current));
      return true;
    }

    private bool OnScheduling(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        if (_Path.SkipRemainingSiblings())
          _RootsEnumeratorFinished = true;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
        return Backtrack();

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Path.SkipCurrentNode();

        if (TryPushNextChild())
          return true;

        return Backtrack();
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        _Path.DisposeCurrentEnumerator();

      Publish(ref _Path.TakeNextVisit());
      return true;
    }

    private bool OnVisiting()
    {
      if (TryPushNextChild())
        return true;

      return Backtrack();
    }

    private bool Backtrack()
    {
      while (true)
      {
        switch (_Path.PopFinishedLevelAndClassify())
        {
          case EngineBacktrackStep.GoToRoot:
            return MoveToNextRootNode();

          case EngineBacktrackStep.PromoteNextChild:
            if (TryPushNextChild())
              return true;
            continue;

          default: // EmitReturnVisit
            Publish(ref _Path.TakeNextVisit());
            return true;
        }
      }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryPushNextChild()
    {
      if (!_Path.TopEnumerator.MoveNext(out var child))
        return false;

      Publish(ref _Path.PushChild(child.Node, child.SiblingIndex));
      return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Publish(ref Copse.Engine.DepthFirstNodeState<TNode> node)
    {
      Mode = node.VisitCount == 0 ? TreenumeratorMode.SchedulingNode : TreenumeratorMode.VisitingNode;
      Node = _Map(node.Node);
      VisitCount = node.VisitCount;
      Position = node.Position;
    }

    protected override void OnDisposing()
    {
      base.OnDisposing();

      _RootsEnumerator?.Dispose();
      _Path.Dispose();
    }
  }
}
