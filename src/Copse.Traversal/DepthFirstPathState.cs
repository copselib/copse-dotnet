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
  /// </summary>
  internal struct DepthFirstPathState<TNode, TEnumerator> : IDisposable
    where TEnumerator : IDisposable
  {
    public DepthFirstPathState(Func<NodeContext<TNode>, TEnumerator> childEnumeratorFactory)
    {
      _ChildEnumeratorFactory = childEnumeratorFactory;
      _AcceptedNodes = new RefSemiDeque<DepthFirstNodeState<TNode>>();
      _Enumerators = new RefSemiDeque<TEnumerator>();
      _RootNodesSeen = 0;
      _DepthOfLastVisitedNode = -1;
    }

    private readonly Func<NodeContext<TNode>, TEnumerator> _ChildEnumeratorFactory;
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
    public ref DepthFirstNodeState<TNode> PushChild(TNode node, int siblingIndex)
      => ref PushLevel(node, new NodePosition(siblingIndex, Depth + 1));

    public ref DepthFirstNodeState<TNode> PushRoot(TNode node)
      => ref PushLevel(node, new NodePosition(_RootNodesSeen++, 0));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref DepthFirstNodeState<TNode> PushLevel(TNode node, NodePosition position)
    {
      _AcceptedNodes.AddLast(new DepthFirstNodeState<TNode>(node, position));
      _Enumerators.AddLast(_ChildEnumeratorFactory(new NodeContext<TNode>(node, position)));
      return ref _AcceptedNodes.GetLast();
    }

    public void SkipCurrentNode() => _AcceptedNodes.RemoveLast();

    public void DisposeCurrentEnumerator() => _Enumerators.GetLast().Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref DepthFirstNodeState<TNode> TakeNextVisit()
    {
      ref var node = ref _AcceptedNodes.GetLast();
      node.VisitCount++;
      _DepthOfLastVisitedNode = node.Position.Depth;
      return ref node;
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
