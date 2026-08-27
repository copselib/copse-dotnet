using Copse.Collections;
using Copse.Core;
using System;
using System.Runtime.CompilerServices;

namespace Copse.Traversal
{
  /// <summary>
  /// The depth-first path bookkeeping as a color-agnostic, shared struct: sans-I/O path state
  /// constrained only on <see cref="IDisposable"/>, so a <b>direct-style</b> driver (natural inlined
  /// control flow, with a synchronous or awaited pull at the seam) can share it verbatim.
  ///
  /// <para>This is the single shared piece of the codegen approach: the sync
  /// <c>DepthFirstDirectTreenumerator</c>, the async <c>AsyncDepthFirstTreenumerator</c>, and the
  /// generated sync twin all drive THIS. The push/pop/backtrack ops are ported from the original
  /// <c>DepthFirstPath</c>. See <see cref="DepthFirstNodeState{TNode}"/> / <see cref="DepthFirstBacktrackStep"/>.</para>
  ///
  /// <para>The path holds the pull-shaped topology's two arrows -- children (the enumerator
  /// factory) and resolve (the handle-to-node map) -- and both fire at push: a pushed handle
  /// makes its child enumerator and resolves to its node, then is not retained. The map runs
  /// once per node, not once per visit.</para>
  /// </summary>
  internal struct DepthFirstPathState<TNode, THandle, TEnumerator> : IDisposable
    where TEnumerator : IDisposable
  {
    public DepthFirstPathState(
      Func<NodeContext<THandle>, TEnumerator> childEnumeratorFactory,
      Func<THandle, TNode> handleToNodeMap)
    {
      _ChildEnumeratorFactory = childEnumeratorFactory;
      _HandleToNodeMap = handleToNodeMap;
      _AcceptedNodes = new RefSemiDeque<DepthFirstNodeState<TNode>>();
      _Enumerators = new RefSemiDeque<TEnumerator>();
      _RootNodesSeen = 0;
      _DepthOfLastVisitedNode = -1;
    }

    private readonly Func<NodeContext<THandle>, TEnumerator> _ChildEnumeratorFactory;
    private readonly Func<THandle, TNode> _HandleToNodeMap;
    private readonly RefSemiDeque<DepthFirstNodeState<TNode>> _AcceptedNodes;
    private readonly RefSemiDeque<TEnumerator> _Enumerators;
    private int _RootNodesSeen;
    private int _DepthOfLastVisitedNode;

    public int Depth => _Enumerators.Count - 1;
    public bool IsEmpty => _Enumerators.Count == 0;

    public ref TEnumerator TopEnumerator
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref _Enumerators.GetLast();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref DepthFirstNodeState<TNode> PushChild(THandle handle, int siblingIndex)
      => ref PushLevel(handle, new NodePosition(siblingIndex, Depth + 1));

    public ref DepthFirstNodeState<TNode> PushRoot(THandle handle)
      => ref PushLevel(handle, new NodePosition(_RootNodesSeen++, 0));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref DepthFirstNodeState<TNode> PushLevel(THandle handle, NodePosition position)
    {
      _AcceptedNodes.AddLast(new DepthFirstNodeState<TNode>(_HandleToNodeMap(handle), position));
      _Enumerators.AddLast(_ChildEnumeratorFactory(new NodeContext<THandle>(handle, position)));
      return ref _AcceptedNodes.GetLast();
    }

    public void SkipCurrentNode() => _AcceptedNodes.RemoveLast();

    public void DisposeCurrentEnumerator() => _Enumerators.GetLast().Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref DepthFirstNodeState<TNode> TakeNextVisit()
    {
      ref var nodeState = ref _AcceptedNodes.GetLast();
      nodeState.VisitCount++;
      _DepthOfLastVisitedNode = nodeState.Position.Depth;
      return ref nodeState;
    }

    public DepthFirstBacktrackStep PopFinishedLevelAndClassify()
    {
      if (_AcceptedNodes.Count > 0 && _AcceptedNodes.GetLast().Position.Depth == _Enumerators.Count - 1)
        _AcceptedNodes.RemoveLast();

      _Enumerators.RemoveLast().Dispose();

      var depth = _Enumerators.Count - 1;

      if (depth < 0)
        return DepthFirstBacktrackStep.GoToRoot;

      if (depth == _DepthOfLastVisitedNode
        || _AcceptedNodes.Count == 0
        || _AcceptedNodes.GetLast().Position.Depth < depth)
        return DepthFirstBacktrackStep.PromoteNextChild;

      return DepthFirstBacktrackStep.EmitReturnVisit;
    }

    public bool SkipRemainingSiblings()
    {
      var wasEffectiveRoot = _AcceptedNodes.Count == 1;

      var parentDepth = wasEffectiveRoot ? 0 : _AcceptedNodes.GetFromBack(1).Position.Depth;
      var depthDelta = _Enumerators.Count - parentDepth;

      for (int i = 1; i < depthDelta; i++)
        _Enumerators.GetFromBack(i).Dispose();

      return wasEffectiveRoot;
    }

    public void Dispose()
    {
      if (_Enumerators == null)
        return;

      while (_Enumerators.Count > 0)
        _Enumerators.RemoveLast().Dispose();
    }
  }
}
