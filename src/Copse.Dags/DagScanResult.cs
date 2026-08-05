namespace Copse.Dags
{
  /// <summary>
  /// The fold tier's canonical pairing (the 2026-08-05 constitution alignment; the tree
  /// family's ScanResult, twinned here because this project is self-contained by ruling): a
  /// source node with the accumulation the pass assigned to it. The pairing comes FROM THE
  /// API so callers never smuggle identity through their payloads -- the dispatch-provenance
  /// principle applied to results; the value-replacing shape-isomorphic result (and the
  /// name-carrying it forced) is retired. What the scans' buffers hold; project
  /// <see cref="Accumulate"/> when only values are wanted. Deliberately carries no position
  /// analog: ordinals are stream facts, owned by the enumeration.
  /// </summary>
  public readonly struct DagScanResult<TNode, TAccumulate>
  {
    public DagScanResult(TNode node, TAccumulate accumulate)
    {
      Node = node;
      Accumulate = accumulate;
    }

    public readonly TNode Node;
    public readonly TAccumulate Accumulate;

    public override string ToString() => $"{Node} <- {Accumulate}";
  }
}
