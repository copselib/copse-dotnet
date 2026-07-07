using Copse.Core.Async;
using System;
using System.Collections.Generic;

namespace Copse.Async
{
  /// <summary>
  /// The async engine-backed treenumerable over a hierarchical source (a fresh roots stream +
  /// child-enumerator factory per enumeration, mapped to values). The async analog of the sync
  /// engine base <c>Copse.Treenumerables.Treenumerable&lt;,,&gt;</c>: a composite
  /// <see cref="IAsyncTreenumerable{TValue}"/> that affords BOTH traversal dimensions.
  /// </summary>
  public sealed class AsyncTreenumerable<TValue, TNode, TAsyncChildEnumerator>
    : IAsyncTreenumerable<TValue>
    where TAsyncChildEnumerator : IAsyncChildEnumerator<TNode>
  {
    public AsyncTreenumerable(
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

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator()
      => new AsyncBreadthFirstTreenumerator<TValue, TNode, TAsyncChildEnumerator>(
        _RootsFactory(), _ChildEnumeratorFactory, _Map);
  }
}
