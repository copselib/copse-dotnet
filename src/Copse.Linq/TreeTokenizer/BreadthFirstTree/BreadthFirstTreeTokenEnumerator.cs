using Copse.Core;
using System.Collections;
using System.Collections.Generic;

namespace Copse.Linq.TreeTokenizer.BreadthFirstTree
{
  internal sealed class BreadthFirstTreeTokenEnumerator<TNode> : IEnumerator<BreadthFirstTreeToken<TNode>>
  {
    public BreadthFirstTreeTokenEnumerator(ITreenumerator<TNode> breadthFirstTreenumerator)
    {
      _Treenumerator = breadthFirstTreenumerator;
    }

    private readonly ITreenumerator<TNode> _Treenumerator;
    private readonly Queue<BreadthFirstTreeToken<TNode>> _CachedSeparators = new Queue<BreadthFirstTreeToken<TNode>>();
    private bool _HasCachedNode = false;
    private BreadthFirstTreeToken<TNode> _CachedNode;
    private int _CurrentLevelDepth = -1;
    private bool _EnumerationStarted = false;
    private bool _TreenumeratorEnumerationFinished = false;

    public BreadthFirstTreeToken<TNode> Current { get; private set; }

    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
      if (_TreenumeratorEnumerationFinished)
        return OnTreenumeratorEnumerationFinished();

      if (_CachedSeparators.Count > 0)
      {
        Current = _CachedSeparators.Dequeue();
        return true;
      }

      if (_HasCachedNode)
      {
        _HasCachedNode = false;
        Current = _CachedNode;
        return true;
      }

      while (_Treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
      {
        if (!_EnumerationStarted)
        {
          OnEnumerationStarting();
          return true;
        }

        if (_Treenumerator.Mode == TreenumeratorMode.SchedulingNode)
        {
          OnSchedulingNode();
          return true;
        }

        OnVisitingNode();
      }

      _TreenumeratorEnumerationFinished = true;

      return OnTreenumeratorEnumerationFinished();
    }

    private void OnEnumerationStarting()
    {
      Current = new BreadthFirstTreeToken<TNode>(_Treenumerator.Node);
      _EnumerationStarted = true;
    }

    private void OnSchedulingNode()
    {
      var node = new BreadthFirstTreeToken<TNode>(_Treenumerator.Node);

      if (_CachedSeparators.Count > 0)
      {
        Current = _CachedSeparators.Dequeue();
        _HasCachedNode = true;
        _CachedNode = node;
        return;
      }

      Current = node;
    }

    private void OnVisitingNode()
    {
      if (_Treenumerator.VisitCount != 1)
        return;

      if (_Treenumerator.Position.Depth == _CurrentLevelDepth)
      {
        _CachedSeparators.Enqueue(new BreadthFirstTreeToken<TNode>(BreadthFirstTreeTokenType.FamilySeparator));
      }
      else
      {
        _CachedSeparators.Enqueue(new BreadthFirstTreeToken<TNode>(BreadthFirstTreeTokenType.GenerationSeparator));

        _CurrentLevelDepth++;
      }
    }

    private bool OnTreenumeratorEnumerationFinished()
    {
      if (_CachedSeparators.Count > 0)
      {
        Current = _CachedSeparators.Dequeue();

        if (Current.Type == BreadthFirstTreeTokenType.GenerationSeparator)
          _CachedSeparators.Clear();

        return true;
      }

      return false;
    }

    public void Reset()
    {
      // Do nothing.
    }

    public void Dispose()
    {
      _Treenumerator?.Dispose();
    }
  }
}
