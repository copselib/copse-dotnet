using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  /// <summary>
  /// The streaming operators over the DAG traversal contract (design-docs/DAG_CONTRACT_DESIGN.md,
  /// phase 2): composable wrappers over <see cref="IDagnumerable{TNode, TEdge}"/>.
  /// Operators preserve their source's ordinals (no relabeling exists to do -- ordinals are
  /// correlation keys, not coordinates), so pruned streams carry gaps, harmlessly.
  /// </summary>
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Maps each node value, forwarding the visit stream otherwise unchanged (structure,
    /// edges, ordinals). Deferred. The selector is evaluated per published visit (a node's
    /// discoveries and entry each see it run; invocation counts are unspecified -- purity
    /// expected, the house contract).
    /// </summary>
    public static IDagnumerable<TResult, TEdge> SelectNodes<TNode, TResult, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, TResult> selector)
    {
      if (selector == null)
        throw new ArgumentNullException(nameof(selector));

      return new SelectNodesDagnumerable<TNode, TResult, TEdge>(source, selector);
    }
  }

  internal sealed class SelectNodesDagnumerable<TNode, TResult, TEdge> : IDagnumerable<TResult, TEdge>
  {
    public SelectNodesDagnumerable(IDagnumerable<TNode, TEdge> source, Func<TNode, TResult> selector)
    {
      _Source = source;
      _Selector = selector;
    }

    private readonly IDagnumerable<TNode, TEdge> _Source;
    private readonly Func<TNode, TResult> _Selector;

    public IDagnumerator<TResult, TEdge> GetDagnumerator() =>
      new SelectNodesDagnumerator<TNode, TResult, TEdge>(_Source.GetDagnumerator(), _Selector);
  }

  internal sealed class SelectNodesDagnumerator<TNode, TResult, TEdge> : IDagnumerator<TResult, TEdge>
  {
    public SelectNodesDagnumerator(IDagnumerator<TNode, TEdge> inner, Func<TNode, TResult> selector)
    {
      _Inner = inner;
      _Selector = selector;

      // The pre-enumeration convention, mirrored (the sentinel's Node stays default -- the
      // selector never sees a value the source never published).
      Mode = DagnumeratorMode.DiscoveringNode;
      Ordinal = -1;
      ParentOrdinal = -1;
      EdgeIndex = 0;
    }

    private readonly IDagnumerator<TNode, TEdge> _Inner;
    private readonly Func<TNode, TResult> _Selector;

    public DagnumeratorMode Mode { get; private set; }
    public TResult Node { get; private set; }
    public int Ordinal { get; private set; }
    public TEdge Edge { get; private set; }
    public int ParentOrdinal { get; private set; }
    public int EdgeIndex { get; private set; }

    public bool MoveNext(DagTraversalStrategies strategies)
    {
      if (!_Inner.MoveNext(strategies))
        return false;

      Mode = _Inner.Mode;
      Node = _Selector(_Inner.Node);
      Ordinal = _Inner.Ordinal;
      Edge = _Inner.Edge;
      ParentOrdinal = _Inner.ParentOrdinal;
      EdgeIndex = _Inner.EdgeIndex;
      return true;
    }

    public void Dispose() => _Inner.Dispose();
  }
}

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The node projection at the EVENT grain: every node relabeled from its whole event --
    /// the arrivals that reached it, its value, the departures it dispatches -- each group
    /// complete, once per node. This is an extend (a relabel from a one-hop vantage), not a
    /// bind; it is what the dispatch tier was being used for whenever a pass needed the group
    /// but moved no value. Destructured seats, return-shaped: the result type is inferred from
    /// the lambda. Capture-shaped: the groups are the buffer's.
    /// </summary>
    public static DagBuffer<TResult, TEdge> SelectNodes<TNode, TEdge, TResult>(
      this IDagnumerable<TNode, TEdge> source,
      Func<IReadOnlyList<DagEdgeContext<TNode, TEdge>>, TNode, IReadOnlyList<DagEdgeContext<TNode, TEdge>>, TResult> selector)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      if (selector == null)
        throw new ArgumentNullException(nameof(selector));

      var buffer = DagBuffer<TNode, TEdge>.From(source);
      DagEventSeats.Build(buffer, out var arrivals, out var departures, out _);

      var values = new TResult[buffer.Count];
      for (var ordinal = 0; ordinal < values.Length; ordinal++)
        values[ordinal] = selector(arrivals[ordinal], buffer[ordinal], departures[ordinal]);

      return buffer.WithValues(values);
    }
  }
}
