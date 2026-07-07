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
  /// <para>Deferred: <c>Where</c> / <c>Select</c> / <c>Do</c> / <c>Hide</c> (both traversal dimensions);
  /// terminals <c>CountNodesAsync</c> / <c>ToListAsync</c>.</para>
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
    /// Async <c>Do</c>: runs a side effect on every emitted visit, forwarding the visit stream
    /// unchanged. Deferred (the effect runs during enumeration, once per <c>MoveNextAsync</c>).
    /// </summary>
    public static IAsyncTreenumerable<TNode> Do<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Action<NodeVisit<TNode>> onNext)
      => new AsyncDoTreenumerable<TNode>(source, onNext);

    /// <summary>
    /// Async <c>Hide</c>: forwards the visit stream unchanged behind the plain
    /// <see cref="IAsyncTreenumerable{TNode}"/> contract, so callers can't downcast to (or feature-test
    /// for) the concrete source type. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> Hide<TNode>(
      this IAsyncTreenumerable<TNode> source)
      => new AsyncHideTreenumerable<TNode>(source);

    /// <summary>
    /// Async <c>PruneAfter</c>: keeps each node that matches the predicate but sheds its subtree (the
    /// matched node is the deepest of its lineage kept). Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneAfter<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => new AsyncPruneAfterTreenumerable<TNode>(source, predicate);

    /// <summary>
    /// Async <c>PruneBefore</c>: prunes each subtree at (and including) the first node matching the
    /// predicate -- no child promotion (SkipNodeAndDescendants). "Prune when true", so the removal
    /// polarity inverts here at the operator, over the Where machinery (keep when true). Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneBefore<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => predicate == null ? source : new AsyncPruneBeforeTreenumerable<TNode>(source, predicate);

    /// <summary>
    /// Async <c>TakeNodesUntil</c>: forwards nodes until one matches the predicate, then stops
    /// scheduling (pruning that node's subtree and later siblings), keeping the matched node itself
    /// iff <paramref name="keepFinalNode"/>. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => new AsyncTakeNodesUntilTreenumerable<TNode>(source, predicate, keepFinalNode);

    /// <summary>
    /// Async <c>TakeNodesWhile</c>: forwards nodes while they match the predicate -- TakeNodesUntil
    /// with an inverted predicate. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => source.TakeNodesUntil(nodeContext => !predicate(nodeContext), keepFinalNode);

    /// <summary>
    /// Async <c>RootfixScan</c>: a cumulative scan from the root -- each node's value becomes the
    /// accumulator applied to its parent's accumulated value and the node (a prefix-fold down each
    /// root-to-node path). Transforms the <c>TNode</c> tree into a <c>TAccumulate</c> tree. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => new AsyncRootfixScanTreenumerable<TNode, TAccumulate>(source, accumulator, seed);

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

    private sealed class AsyncDoTreenumerable<TNode> : IAsyncTreenumerable<TNode>
    {
      public AsyncDoTreenumerable(IAsyncTreenumerable<TNode> source, Action<NodeVisit<TNode>> onNext)
      {
        _Source = source;
        _OnNext = onNext;
      }

      private readonly IAsyncTreenumerable<TNode> _Source;
      private readonly Action<NodeVisit<TNode>> _OnNext;

      public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
        => new AsyncDoTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator(), _OnNext);

      public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
        => new AsyncDoTreenumerator<TNode>(_Source.GetAsyncBreadthFirstTreenumerator(), _OnNext);
    }

    private sealed class AsyncDoTreenumerator<TNode> : IAsyncTreenumerator<TNode>
    {
      public AsyncDoTreenumerator(IAsyncTreenumerator<TNode> inner, Action<NodeVisit<TNode>> onNext)
      {
        _Inner = inner;
        _OnNext = onNext;
      }

      private readonly IAsyncTreenumerator<TNode> _Inner;
      private readonly Action<NodeVisit<TNode>> _OnNext;

      public TNode Node => _Inner.Node;
      public int VisitCount => _Inner.VisitCount;
      public TreenumeratorMode Mode => _Inner.Mode;
      public NodePosition Position => _Inner.Position;

      public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
      {
        if (!await _Inner.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
          return false;

        _OnNext?.Invoke(new NodeVisit<TNode>(_Inner.Mode, _Inner.Node, _Inner.VisitCount, _Inner.Position));
        return true;
      }

      public ValueTask DisposeAsync() => _Inner.DisposeAsync();
    }

    private sealed class AsyncHideTreenumerable<TNode> : IAsyncTreenumerable<TNode>
    {
      public AsyncHideTreenumerable(IAsyncTreenumerable<TNode> source)
      {
        _Source = source;
      }

      private readonly IAsyncTreenumerable<TNode> _Source;

      public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
        => new AsyncHideTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator());

      public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
        => new AsyncHideTreenumerator<TNode>(_Source.GetAsyncBreadthFirstTreenumerator());
    }

    private sealed class AsyncHideTreenumerator<TNode> : IAsyncTreenumerator<TNode>
    {
      public AsyncHideTreenumerator(IAsyncTreenumerator<TNode> inner)
      {
        _Inner = inner;
      }

      private readonly IAsyncTreenumerator<TNode> _Inner;

      public TNode Node => _Inner.Node;
      public int VisitCount => _Inner.VisitCount;
      public TreenumeratorMode Mode => _Inner.Mode;
      public NodePosition Position => _Inner.Position;

      public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
        => _Inner.MoveNextAsync(nodeTraversalStrategies);

      public ValueTask DisposeAsync() => _Inner.DisposeAsync();
    }

    private sealed class AsyncPruneAfterTreenumerable<TNode> : IAsyncTreenumerable<TNode>
    {
      public AsyncPruneAfterTreenumerable(IAsyncTreenumerable<TNode> source, Func<NodeContext<TNode>, bool> predicate)
      {
        _Source = source;
        _Predicate = predicate;
      }

      private readonly IAsyncTreenumerable<TNode> _Source;
      private readonly Func<NodeContext<TNode>, bool> _Predicate;

      public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
        => new AsyncPruneAfterTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator, _Predicate);

      public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
        => new AsyncPruneAfterTreenumerator<TNode>(_Source.GetAsyncBreadthFirstTreenumerator, _Predicate);
    }

    private sealed class AsyncPruneBeforeTreenumerable<TNode> : IAsyncTreenumerable<TNode>
    {
      public AsyncPruneBeforeTreenumerable(IAsyncTreenumerable<TNode> source, Func<NodeContext<TNode>, bool> predicate)
      {
        _Source = source;
        _Predicate = predicate;
      }

      private readonly IAsyncTreenumerable<TNode> _Source;
      private readonly Func<NodeContext<TNode>, bool> _Predicate;

      // PruneBefore is Where over the INVERTED predicate with SkipNodeAndDescendants (prune the
      // subtree, no promotion) instead of Where's SkipNode (promote children).
      public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
        => new AsyncWhereDepthFirstTreenumerator<TNode>(
          _Source.GetAsyncDepthFirstTreenumerator,
          nodeContext => !_Predicate(nodeContext),
          NodeTraversalStrategies.SkipNodeAndDescendants);

      public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
        => new AsyncWhereBreadthFirstTreenumerator<TNode>(
          _Source.GetAsyncBreadthFirstTreenumerator,
          nodeContext => !_Predicate(nodeContext),
          NodeTraversalStrategies.SkipNodeAndDescendants);
    }

    private sealed class AsyncTakeNodesUntilTreenumerable<TNode> : IAsyncTreenumerable<TNode>
    {
      public AsyncTakeNodesUntilTreenumerable(IAsyncTreenumerable<TNode> source, Func<NodeContext<TNode>, bool> predicate, bool keepFinalNode)
      {
        _Source = source;
        _Predicate = predicate;
        _KeepFinalNode = keepFinalNode;
      }

      private readonly IAsyncTreenumerable<TNode> _Source;
      private readonly Func<NodeContext<TNode>, bool> _Predicate;
      private readonly bool _KeepFinalNode;

      public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
        => new AsyncTakeNodesUntilTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator, _Predicate, _KeepFinalNode);

      public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
        => new AsyncTakeNodesUntilTreenumerator<TNode>(_Source.GetAsyncBreadthFirstTreenumerator, _Predicate, _KeepFinalNode);
    }

    private sealed class AsyncRootfixScanTreenumerable<TNode, TAccumulate> : IAsyncTreenumerable<TAccumulate>
    {
      public AsyncRootfixScanTreenumerable(
        IAsyncTreenumerable<TNode> source,
        Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
        TAccumulate seed)
      {
        _Source = source;
        _Accumulator = accumulator;
        _Seed = seed;
      }

      private readonly IAsyncTreenumerable<TNode> _Source;
      private readonly Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> _Accumulator;
      private readonly TAccumulate _Seed;

      public IAsyncTreenumerator<TAccumulate> GetAsyncDepthFirstTreenumerator()
        => new AsyncRootfixScanDepthFirstTreenumerator<TNode, TAccumulate>(_Source.GetAsyncDepthFirstTreenumerator, _Accumulator, _Seed);

      public IAsyncTreenumerator<TAccumulate> GetAsyncBreadthFirstTreenumerator()
        => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, TAccumulate>(_Source.GetAsyncBreadthFirstTreenumerator, _Accumulator, _Seed);
    }
  }
}
