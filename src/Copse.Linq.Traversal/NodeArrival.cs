namespace Copse.Linq
{
  // The INPUT pairing (docs/SCANRESULT_DESIGN.md, the recording rule made type-level
  // 2026-08-06): a source node with the value the pass delivered TO it. RootfixDispatch's
  // return, and only its -- the family's one 1-in-n-out shape: a downward survey has no
  // node-grained output (its n outputs land as its children's arrivals), so the only
  // per-node record is what arrived. Until 2026-08-06 this rode ScanResult's Accumulate
  // field, one name for two tier-shaped meanings -- the split follows the dag family
  // (DagDispatchResult), where the principle was ratified from birth: the two tiers never
  // overload one field with two meanings. The arrival is SINGULAR because a tree node has
  // one parent; the dag twin's is a group (DagArrivals) because a dag node has n -- the
  // field shape itself records the structural difference between the families.
  //
  // Named by PAYLOAD per the house pairing grammar (NodeContext, NodeVisit,
  // NodeAccumulation). Deliberately carries NO position (see NodeAccumulation). Shared by
  // the sync operators and their async analogs.
  public readonly struct NodeArrival<TSource, TDispatch>
  {
    public NodeArrival(TSource node, TDispatch arrival)
    {
      Node = node;
      Arrival = arrival;
    }

    public readonly TSource Node;
    public readonly TDispatch Arrival;

    public override string ToString() => $"{Node} <- {Arrival}";
  }
}
