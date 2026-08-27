namespace Copse.Linq
{
  // The INPUT pairing (design-docs/SCANRESULT_DESIGN.md): a downward survey has no
  // node-grained output -- its n outputs land as its children's arrivals -- so the only
  // per-node record is what arrived. Named by payload per the house pairing grammar;
  // deliberately carries no position (see NodeAccumulation).
  /// <summary>
  /// A source node paired with the value a dispatch pass delivered to it -- what
  /// <c>RootfixDispatch</c> returns for every node: the node, and what its family's survey
  /// dispatched to it. Carries no position; positional context is available inside the
  /// survey's own callback types.
  /// </summary>
  public readonly struct NodeArrival<TNode, TDispatch>
  {
    /// <summary>Pairs <paramref name="node"/> with <paramref name="arrival"/>.</summary>
    public NodeArrival(TNode node, TDispatch arrival)
    {
      Node = node;
      Arrival = arrival;
    }

    /// <summary>The source node.</summary>
    public readonly TNode Node;

    /// <summary>The value dispatched to this node by its family's survey.</summary>
    public readonly TDispatch Arrival;

    /// <inheritdoc/>
    public override string ToString() => $"{Node} <- {Arrival}";
  }
}
