using System;
using System.Collections.Generic;
using Copse.Async.Treenumerators;
using Copse.Core.Async;

namespace Copse.Async.Treenumerables
{
  /// <summary>
  /// The async engine-backed treenumerable over a hierarchical source (a roots stream +
  /// child-enumerator factory), and the codegen source of truth for the sync engine base
  /// <c>Copse.Treenumerables.Treenumerable&lt;,,&gt;</c>. A composite
  /// <see cref="IAsyncTreenumerable{TValue}"/> affording BOTH traversal dimensions; each acquisition
  /// re-enumerates the roots source from the start (an <see cref="IAsyncEnumerable{TNode}"/> is cold
  /// like its sync counterpart). A hot/single-pass source should be Memoized (or wrapped in
  /// AsyncTree.Defer) for freshness -- the same contract as the sync base.
  /// </summary>
  public class AsyncTreenumerable<TValue, TNode, TAsyncChildEnumerator>
    : IAsyncTreenumerable<TValue>
    where TAsyncChildEnumerator : IAsyncChildEnumerator<TNode>
  {
    public AsyncTreenumerable(
      Func<NodeContext<TNode>, TAsyncChildEnumerator> childEnumeratorFactory,
      Func<TNode, TValue> nodeToValueMap,
      IAsyncEnumerable<TNode> roots)
    {
      _ChildEnumeratorFactory = childEnumeratorFactory;
      _NodeToValueMap = nodeToValueMap;
      _Roots = roots;
    }

    private readonly IAsyncEnumerable<TNode> _Roots;
    private readonly Func<NodeContext<TNode>, TAsyncChildEnumerator> _ChildEnumeratorFactory;
    private readonly Func<TNode, TValue> _NodeToValueMap;

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator()
    {
      return
        new AsyncBreadthFirstTreenumerator<TValue, TNode, TAsyncChildEnumerator>(
          _Roots,
          _ChildEnumeratorFactory,
          _NodeToValueMap);
    }

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator()
    {
      return
        new AsyncDepthFirstTreenumerator<TValue, TNode, TAsyncChildEnumerator>(
          _Roots,
          _ChildEnumeratorFactory,
          _NodeToValueMap);
    }
  }

  // Convenience base for trees whose node IS its surfaced value (TValue == TNode): the value map is
  // the identity, so callers don't supply one. Trees with a distinct internal handle (e.g.
  // PreorderTree's int index) use the three-parameter base above with an explicit resolution map.
  public class AsyncTreenumerable<TNode, TAsyncChildEnumerator>
    : AsyncTreenumerable<TNode, TNode, TAsyncChildEnumerator>
    where TAsyncChildEnumerator : IAsyncChildEnumerator<TNode>
  {
    public AsyncTreenumerable(
      Func<NodeContext<TNode>, TAsyncChildEnumerator> childEnumeratorFactory,
      IAsyncEnumerable<TNode> roots)
      : base(childEnumeratorFactory, node => node, roots)
    {
    }
  }
}
