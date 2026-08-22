using System;

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
