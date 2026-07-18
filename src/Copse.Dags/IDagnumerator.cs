using System;

namespace Copse.Dags
{
  /// <summary>
  /// The DAG visit-stream protocol (docs/DAG_CONTRACT_DESIGN.md): a stateful walk over a dag's
  /// topological presentation, publishing <see cref="DagnumeratorMode.DiscoveringNode"/> once per
  /// in-edge and <see cref="DagnumeratorMode.EnteringNode"/> once per node, entries strictly
  /// after their last discovery. Sources with zero in-edges (the walk's roots) are discovered by
  /// convention at the start of enumeration, in topological order, with
  /// <see cref="ParentOrdinal"/> −1 and <see cref="EdgeIndex"/> counting the sources.
  ///
  /// Nodes are correlated by <see cref="Ordinal"/> — the node's index in the enumeration
  /// dimension's topological order — never by value identity: user values are never compared or
  /// hashed. Pre-enumeration convention (the ForestRoot analog, conformance-checked): mode
  /// DiscoveringNode, Ordinal −1, ParentOrdinal −1, EdgeIndex 0, default Node/Edge.
  /// </summary>
  public interface IDagnumerator<TNode, TEdge> : IDisposable
  {
    /// <summary>
    /// Applies <paramref name="strategies"/> to the CURRENT visit, then advances. Strategies are
    /// mode-checked: <see cref="DagTraversalStrategies.SkipEdge"/> only answers a discovery,
    /// <see cref="DagTraversalStrategies.SkipOutEdges"/> only answers an entry; a wrong-mode
    /// strategy throws.
    /// </summary>
    bool MoveNext(DagTraversalStrategies strategies);

    DagnumeratorMode Mode { get; }

    /// <summary>The discovered/entered node's value.</summary>
    TNode Node { get; }

    /// <summary>The node's index in this dimension's topological order — the correlation key.</summary>
    int Ordinal { get; }

    /// <summary>Discovery only: the in-edge's payload (default for the conventional source discoveries).</summary>
    TEdge Edge { get; }

    /// <summary>Discovery only: the dispatching parent's ordinal; −1 for the conventional source discoveries.</summary>
    int ParentOrdinal { get; }

    /// <summary>Discovery only: the edge's index within the dispatching parent's out-edges (source index for source discoveries).</summary>
    int EdgeIndex { get; }
  }
}
