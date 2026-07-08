using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.TreeTokenizer.BreadthFirstTree;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq.Async.TreeTokenizer.BreadthFirstTree
{
  // The breadth-first token stream -- Node / FamilySeparator (between siblings of one parent) /
  // GenerationSeparator (between levels). Separators are produced on the first visit of a node
  // and flushed just before the next scheduled node; a trailing generation separator terminates
  // the stream (its level never gets nodes).
  internal sealed class AsyncBreadthFirstTreeTokenEnumerator<TNode> : IAsyncEnumerator<BreadthFirstTreeToken<TNode>>
  {
    public AsyncBreadthFirstTreeTokenEnumerator(IAsyncTreenumerator<TNode> breadthFirstTreenumerator, CancellationToken cancellationToken)
    {
      _Treenumerator = breadthFirstTreenumerator;
      _CancellationToken = cancellationToken;
    }

    private readonly IAsyncTreenumerator<TNode> _Treenumerator;
    private readonly CancellationToken _CancellationToken;
    private readonly Queue<BreadthFirstTreeToken<TNode>> _CachedSeparators = new Queue<BreadthFirstTreeToken<TNode>>();
    private bool _HasCachedNode = false;
    private BreadthFirstTreeToken<TNode> _CachedNode;
    private int _CurrentLevelDepth = -1;
    private bool _EnumerationStarted = false;
    private bool _TreenumeratorEnumerationFinished = false;

    public BreadthFirstTreeToken<TNode> Current { get; private set; }

    // codegen: begin sync-only
    // object IEnumerator.Current => Current;
    // codegen: end sync-only

    public async ValueTask<bool> MoveNextAsync()
    {
      _CancellationToken.ThrowIfCancellationRequested();
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

      while (await _Treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
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

    // codegen: begin sync-only
    // public void Reset()
    // {
    //   // Do nothing.
    // }
    // codegen: end sync-only

    public async ValueTask DisposeAsync()
    {
      if (_Treenumerator != null)
        await _Treenumerator.DisposeAsync().ConfigureAwait(false);
    }
  }
}
