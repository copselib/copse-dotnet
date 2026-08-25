using System;
using System.Collections.Generic;
using Copse.Async.Treenumerators;
using Copse.Core.Async;

namespace Copse.Async.Treenumerables
{
  // Codegen source of truth for the sync engine base Copse.Treenumerables.Treenumerable<,,>.
  //
  // The engine is a pull-shaped topology: resolve (handleToNodeMap, the GetValue arrow),
  // children (the child-enumerator factory), roots (the roots stream). The walkable tier's
  // IAsyncTreeTopology is the same three ingredients with the child axis indexed instead of
  // pulled, plus the parent probe that indexing makes affordable.
  /// <summary>
  /// The engine-backed treenumerable over hierarchical data. The engine walks HANDLES -- each
  /// node's navigable identity, whatever can produce a child enumerator (an index into a
  /// store, an object reference, a handle-and-payload pair) -- and surfaces NODES, resolving
  /// each handle through <c>handleToNodeMap</c> as it publishes. Where the node is its own
  /// handle, use the two-parameter form. Each traversal re-enumerates the roots source from
  /// the start (an <see cref="IAsyncEnumerable{THandle}"/> is cold, like its sync
  /// counterpart); a single-pass source should be memoized first.
  /// </summary>
  public class AsyncTreenumerable<TNode, THandle, TAsyncChildEnumerator>
    : IAsyncTreenumerable<TNode>
    where TAsyncChildEnumerator : IAsyncChildEnumerator<THandle>
  {
    public AsyncTreenumerable(
      Func<NodeContext<THandle>, TAsyncChildEnumerator> childEnumeratorFactory,
      Func<THandle, TNode> handleToNodeMap,
      IAsyncEnumerable<THandle> roots)
    {
      _ChildEnumeratorFactory = childEnumeratorFactory;
      _HandleToNodeMap = handleToNodeMap;
      _Roots = roots;
    }

    private readonly IAsyncEnumerable<THandle> _Roots;
    private readonly Func<NodeContext<THandle>, TAsyncChildEnumerator> _ChildEnumeratorFactory;
    private readonly Func<THandle, TNode> _HandleToNodeMap;

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
    {
      return
        new AsyncBreadthFirstTreenumerator<TNode, THandle, TAsyncChildEnumerator>(
          _Roots,
          _ChildEnumeratorFactory,
          _HandleToNodeMap);
    }

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
    {
      return
        new AsyncDepthFirstTreenumerator<TNode, THandle, TAsyncChildEnumerator>(
          _Roots,
          _ChildEnumeratorFactory,
          _HandleToNodeMap);
    }
  }

  /// <summary>The two-parameter form for trees whose node is its own handle: the map is the
  /// identity, so callers don't supply one.</summary>
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
