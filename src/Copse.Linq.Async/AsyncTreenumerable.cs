using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async; // AsyncWhereDepthFirstTreenumerator
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  /// <summary>
  /// Async LINQ-style tree operators over <see cref="IAsyncTreenumerable{TNode}"/>. Sits in the
  /// <c>Copse.Linq</c> namespace alongside the synchronous <see cref="Treenumerable"/>, exactly as
  /// <c>System.Linq.AsyncEnumerable</c> sits alongside <c>Enumerable</c>: deferred operators keep their
  /// sync names (no <c>Async</c> suffix) and are overload-resolved by the async receiver type; terminal
  /// operators carry the <c>Async</c> suffix (they return an awaitable).
  ///
  /// <para>Prototype: deferred <c>Where</c> / <c>Select</c> and terminal <c>CountNodesAsync</c> /
  /// <c>ToListAsync</c>, depth-first dimension only.</para>
  /// </summary>
  public static class AsyncTreenumerable
  {
    /// <summary>Async <c>Where</c> (LINQ polarity: true = keep). Deferred; returns the filtered async tree.</summary>
    public static IAsyncTreenumerable<TNode> Where<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return new AsyncWhereTreenumerable<TNode>(source, predicate);
    }

    /// <summary>Async <c>Select</c>: maps each node's value, forwarding the visit stream unchanged. Deferred.</summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
      => new AsyncSelectTreenumerable<TSource, TResult>(source, selector);

    /// <summary>
    /// Terminal: the number of nodes in the (filtered) tree. Each node is scheduled exactly once, so
    /// this counts scheduling visits. Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask<int> CountNodesAsync<TNode>(this IAsyncTreenumerable<TNode> source)
    {
      var count = 0;
      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          if (t.Mode == TreenumeratorMode.SchedulingNode)
            count++;
      return count;
    }

    /// <summary>
    /// Terminal: the node values of the (filtered) tree, in depth-first schedule order (each node
    /// once). Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask<List<TNode>> ToListAsync<TNode>(this IAsyncTreenumerable<TNode> source)
    {
      var list = new List<TNode>();
      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          if (t.Mode == TreenumeratorMode.SchedulingNode)
            list.Add(t.Node);
      return list;
    }

    private sealed class AsyncWhereTreenumerable<TNode> : IAsyncTreenumerable<TNode>
    {
      public AsyncWhereTreenumerable(IAsyncTreenumerable<TNode> source, Func<NodeContext<TNode>, bool> predicate)
      {
        _Source = source;
        _Predicate = predicate;
      }

      private readonly IAsyncTreenumerable<TNode> _Source;
      private readonly Func<NodeContext<TNode>, bool> _Predicate;

      public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
        => new AsyncWhereDepthFirstTreenumerator<TNode>(
          _Source.GetAsyncDepthFirstTreenumerator,
          _Predicate,
          NodeTraversalStrategies.SkipNode);

      public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
        => new AsyncWhereBreadthFirstTreenumerator<TNode>(
          _Source.GetAsyncBreadthFirstTreenumerator,
          _Predicate,
          NodeTraversalStrategies.SkipNode);
    }

    private sealed class AsyncSelectTreenumerable<TSource, TResult> : IAsyncTreenumerable<TResult>
    {
      public AsyncSelectTreenumerable(IAsyncTreenumerable<TSource> source, Func<TSource, TResult> selector)
      {
        _Source = source;
        _Selector = selector;
      }

      private readonly IAsyncTreenumerable<TSource> _Source;
      private readonly Func<TSource, TResult> _Selector;

      public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator()
        => new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator(), _Selector);

      public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator()
        => new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator(), _Selector);
    }

    private sealed class AsyncSelectTreenumerator<TSource, TResult> : IAsyncTreenumerator<TResult>
    {
      public AsyncSelectTreenumerator(IAsyncTreenumerator<TSource> inner, Func<TSource, TResult> selector)
      {
        _Inner = inner;
        _Selector = selector;
      }

      private readonly IAsyncTreenumerator<TSource> _Inner;
      private readonly Func<TSource, TResult> _Selector;

      public TResult Node { get; private set; } = default;
      public int VisitCount => _Inner.VisitCount;
      public TreenumeratorMode Mode => _Inner.Mode;
      public NodePosition Position => _Inner.Position;

      public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
      {
        if (!await _Inner.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
          return false;

        Node = _Selector(_Inner.Node);
        return true;
      }

      public ValueTask DisposeAsync() => _Inner.DisposeAsync();
    }
  }
}
