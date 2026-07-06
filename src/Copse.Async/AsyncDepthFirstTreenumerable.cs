using Copse.Core.Async;
using System;
using System.Collections.Generic;

namespace Copse.Async
{
  /// <summary>
  /// A deferred async treenumerable over a hierarchical source: a fresh roots stream + child-enumerator
  /// factory per enumeration, mapped to values. The async analog of the engine-backed treenumerable, so
  /// operators (Copse.Linq.AsyncTreenumerable's Where / Select) have something to wrap.
  /// </summary>
  public sealed class AsyncDepthFirstTreenumerable<TValue, TNode, TAsyncChildEnumerator>
    : IAsyncTreenumerable<TValue>
    where TAsyncChildEnumerator : IAsyncChildEnumerator<TNode>
  {
    public AsyncDepthFirstTreenumerable(
      Func<IAsyncEnumerable<TNode>> rootsFactory,
      Func<NodeContext<TNode>, TAsyncChildEnumerator> childEnumeratorFactory,
      Func<TNode, TValue> map)
    {
      _RootsFactory = rootsFactory;
      _ChildEnumeratorFactory = childEnumeratorFactory;
      _Map = map;
    }

    private readonly Func<IAsyncEnumerable<TNode>> _RootsFactory;
    private readonly Func<NodeContext<TNode>, TAsyncChildEnumerator> _ChildEnumeratorFactory;
    private readonly Func<TNode, TValue> _Map;

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator()
      => new AsyncDepthFirstTreenumerator<TValue, TNode, TAsyncChildEnumerator>(
        _RootsFactory(), _ChildEnumeratorFactory, _Map);
  }
}
