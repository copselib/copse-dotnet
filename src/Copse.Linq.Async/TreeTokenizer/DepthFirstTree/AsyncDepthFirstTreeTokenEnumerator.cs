using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.TreeTokenizer.DepthFirstTree;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq.Async.TreeTokenizer.DepthFirstTree
{
  // The depth-first token stream -- Node / StartChildGroup / EndChildGroup. Depth increases by
  // one per scheduled child (a StartChildGroup); a decrease of k closes k child groups; the tail
  // closes back to the roots.
  internal sealed class AsyncDepthFirstTreeTokenEnumerator<TNode> : IAsyncEnumerator<DepthFirstTreeToken<TNode>>
  {
    public AsyncDepthFirstTreeTokenEnumerator(IAsyncTreenumerator<TNode> breadthFirstTreenumerator)
    {
      _Treenumerator = breadthFirstTreenumerator;
    }

    private readonly IAsyncTreenumerator<TNode> _Treenumerator;

    public DepthFirstTreeToken<TNode> Current { get; private set; }

    // codegen: begin sync-only
    // object IEnumerator.Current => Current;
    // codegen: end sync-only

    private int _PreviousDepth = 0;
    private bool _HasCachedNode = false;
    private DepthFirstTreeToken<TNode> _CachedNode;
    private int _CachedEndChildGroupTokenCount = 0;

    public async ValueTask<bool> MoveNextAsync()
    {
      if (_CachedEndChildGroupTokenCount > 0)
      {
        _CachedEndChildGroupTokenCount--;
        Current = new DepthFirstTreeToken<TNode>(DepthFirstTreeTokenType.EndChildGroup);
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
        if (_Treenumerator.Mode != TreenumeratorMode.SchedulingNode)
          continue;

        var depth = _Treenumerator.Position.Depth;

        if (depth != _PreviousDepth)
        {
          OnDepthChanged(depth);
          return true;
        }

        Current = new DepthFirstTreeToken<TNode>(_Treenumerator.Node);
        return true;
      }

      return OnTreenumeratorFinished();
    }

    private void OnDepthChanged(int newDepth)
    {
      if (newDepth > _PreviousDepth)
      {
        Current = new DepthFirstTreeToken<TNode>(DepthFirstTreeTokenType.StartChildGroup);
      }
      else
      {
        _CachedEndChildGroupTokenCount = _PreviousDepth - _Treenumerator.Position.Depth - 1;
        Current = new DepthFirstTreeToken<TNode>(DepthFirstTreeTokenType.EndChildGroup);
      }

      _HasCachedNode = true;
      _CachedNode = new DepthFirstTreeToken<TNode>(_Treenumerator.Node);
      _PreviousDepth = newDepth;
    }

    private bool OnTreenumeratorFinished()
    {
      if (_PreviousDepth <= 0)
        return false;

      _CachedEndChildGroupTokenCount = _PreviousDepth - 1;
      Current = new DepthFirstTreeToken<TNode>(DepthFirstTreeTokenType.EndChildGroup);
      _PreviousDepth = 0;
      return true;
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
