using System;
using System.Collections.Generic;
using Copse.Async.Treenumerators;
using Copse.Core.Async;

namespace Copse.Async.Treenumerables
{
  // Codegen source of truth for the sync engine base Copse.Treenumerables.Treenumerable<,,>.
  /// <summary>
  /// The engine-backed treenumerable over hierarchical data: give it a roots stream and a
  /// child-enumerator factory, and it affords both traversal orders. Each traversal
  /// re-enumerates the roots source from the start (an <see cref="IAsyncEnumerable{TNode}"/>
  /// is cold, like its sync counterpart); a single-pass source should be memoized first.
  /// <typeparamref name="TNode"/> is the traversed node type and
  /// <typeparamref name="TValue"/> the surfaced value; <paramref name="nodeToValueMap"/> in
  /// the constructor resolves one to the other per visit.
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

  /// <summary>The two-parameter form for trees whose node is its own surfaced value: the value
  /// map is the identity, so callers don't supply one.</summary>
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
