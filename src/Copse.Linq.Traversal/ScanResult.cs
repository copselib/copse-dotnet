namespace Copse.Linq
{
  // The aggregation family's canonical pairing (docs/SCANRESULT_DESIGN.md): a source node with
  // the value the pass assigned to it. WHICH value is tier-shaped, and the asymmetry is forced
  // (the audit's input/output row): FOLDS record their OUTPUT (the node's accumulation --
  // RootfixScan seed⊕...⊕node, leaffix rollups), while the rootfix SURVEY records its INPUT --
  // the node's ARRIVAL -- because a survey is the family's one 1-in-n-out shape: it has no
  // node-grained output, and its n outputs are recorded as its children's arrivals. The
  // pairing comes from the API so callers never smuggle identity through their payloads
  // (the dag family's dispatch-provenance principle, tree-side). What the pure scans and
  // dispatches return, and what the aggregates yield.
  //
  // Deliberately carries NO position: positions are stream facts, not value properties --
  // Where renumbers siblings and promotion compresses depths, so an in-band position would go
  // stale under composition. Callback-context types (DispatchTarget, DispatchSource) carry
  // NodeContext instead: immediate, consumed in place, never stale. Shared by the sync
  // operators and their async analogs.
  public readonly struct ScanResult<TSource, TAccumulate>
  {
    public ScanResult(TSource node, TAccumulate accumulate)
    {
      Node = node;
      Accumulate = accumulate;
    }

    public readonly TSource Node;
    public readonly TAccumulate Accumulate;

    public override string ToString() => $"{Node} <- {Accumulate}";
  }
}
