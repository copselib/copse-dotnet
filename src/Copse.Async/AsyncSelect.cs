using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Async
{
  /// <summary>Async LINQ operators over <see cref="IAsyncTreenumerable{TNode}"/>. Prototype: Select only.</summary>
  public static class AsyncTreenumerableExtensions
  {
    /// <summary>
    /// Async <c>Select</c>. Structurally a pure passthrough -- it maps each node's value and forwards
    /// the visit stream unchanged -- so it is the smallest operator that exercises the async
    /// composition seam: its one source touch is <c>await inner.MoveNextAsync(...)</c>. A future
    /// structural operator (Where, prune) extracts its own sans-I/O cadence the same way the engine
    /// does, with the inner treenumerator's advance as its seam.
    /// </summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
      => new AsyncSelectTreenumerable<TSource, TResult>(source, selector);

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
        // THE SEAM: the operator's only source touch is the inner treenumerator's awaited advance.
        if (!await _Inner.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
          return false;

        Node = _Selector(_Inner.Node);
        return true;
      }

      public ValueTask DisposeAsync() => _Inner.DisposeAsync();
    }
  }
}
