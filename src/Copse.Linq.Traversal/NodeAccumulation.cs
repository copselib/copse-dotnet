namespace Copse.Linq
{
  // The OUTPUT pairing (design-docs/SCANRESULT_DESIGN.md, the recording rule made type-level
  // 2026-08-06): a source node with the accumulation the pass computed FOR it. Returned by
  // every operator whose per-node record is an output -- the scans, the aggregates, and
  // LeaffixDispatch (n-in-1-out: its survey has a node-grained output). RootfixDispatch is
  // the family's one input-recorder and returns NodeArrival instead -- one field, one
  // meaning, per tier. Named by PAYLOAD, not operator (the house pairing grammar:
  // NodeContext, NodeVisit, NodePosition), because the operator axis lies here: a dispatch
  // (leaffix) records accumulations. Was ScanResult until 2026-08-06.
  //
  // The pairing comes from the API so callers never smuggle identity through their payloads
  // (the dag family's dispatch-provenance principle, tree-side). Deliberately carries NO
  // position: positions are stream facts, not value properties -- Where renumbers siblings
  // and promotion compresses depths, so an in-band position would go stale under
  // composition. Callback-context types (DispatchTarget, DispatchSource) carry NodeContext
  // instead: immediate, consumed in place, never stale. Shared by the sync operators and
  // their async analogs.
  public readonly struct NodeAccumulation<TSource, TAccumulate>
  {
    public NodeAccumulation(TSource node, TAccumulate accumulate)
    {
      Node = node;
      Accumulate = accumulate;
    }

    public readonly TSource Node;
    public readonly TAccumulate Accumulate;

    public override string ToString() => $"{Node} <- {Accumulate}";
  }
}
