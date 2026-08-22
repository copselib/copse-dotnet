namespace Copse.Dags
{
  /// <summary>
  /// The provider SPI behind every dag walker (the tree family's <c>ITreeTopology</c>, dualized;
  /// WALKER_FACTORY_DESIGN.md §2 and §8): structure as questions, answered per handle. Three
  /// indexed probes and the labeling. The child axis is the OUT-EDGE GROUP (downstream, toward
  /// the sinks) and the parent axis is the IN-EDGE GROUP (upstream, toward the sources) -- both
  /// indexed, never counted, because fan-out and fan-in are both unbounded on a dag and the
  /// tree's single parent is the arity-one collapse of the in-edge group. The sources are the
  /// VIRTUAL SOURCE's child group, which is why <see cref="TryGetSourceAt"/> exists: the
  /// unfocused walker stands on the virtual source and its first step down is a source.
  ///
  /// Group order is a structural fact the provider owes: out-edges in the dispatch order the
  /// visit protocol presents them, in-edges in discovery order (the buffer's in-adjacency), and
  /// every edge appears once in each group it belongs to (transpose consistency -- the dag
  /// skeleton's validity predicate has this as its third leg). Handles are compared by
  /// <c>EqualityComparer&lt;THandle&gt;.Default</c> wherever a walk must dedup -- sharing is
  /// representable, so set semantics need handle equality; the pledge against VALUE comparison
  /// is untouched. Topology is the comonad's invariant subject: mutate it and you fall out.
  /// </summary>
  public interface IDagTopology<TValue, THandle, TEdge>
  {
    /// <summary>Resolves a handle to the value it labels (extract's raw material).</summary>
    TValue GetValue(THandle handle);

    /// <summary>
    /// The in-edge group, indexed: in-edge <paramref name="inEdgeIndex"/> of the node, as the
    /// parent it arrives from plus the payload; the miss past the last in-edge. A source's
    /// group is empty -- its parent is the virtual source, which the WALKER answers (the
    /// unfocused stance), never the topology.
    /// </summary>
    DagStep<THandle, TEdge> TryGetParentAt(THandle handle, int inEdgeIndex);

    /// <summary>The out-edge group, indexed: out-edge <paramref name="outEdgeIndex"/> of the node; the miss past the last out-edge.</summary>
    DagStep<THandle, TEdge> TryGetChildAt(THandle handle, int outEdgeIndex);

    /// <summary>The virtual source's child group: source <paramref name="sourceIndex"/> in source order, on the seed edge (<c>default</c> payload); the miss past the last source.</summary>
    DagStep<THandle, TEdge> TryGetSourceAt(int sourceIndex);
  }
}
