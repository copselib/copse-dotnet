namespace Copse.Linq
{
  // The OUTPUT pairing (design-docs/SCANRESULT_DESIGN.md): every operator whose per-node
  // record is an output returns this; RootfixDispatch is the family's one input-recorder and
  // returns NodeArrival instead -- one field, one meaning, per tier. Deliberately carries NO
  // position: positions are stream facts, not value properties -- Where renumbers siblings
  // and promotion compresses depths, so an in-band position would go stale under
  // composition. Callback-context types (DispatchTarget, DispatchSource) carry NodeContext
  // instead: immediate, consumed in place, never stale.
  /// <summary>
  /// A source node paired with the accumulation a pass computed for it -- what the scans,
  /// the aggregates, and <c>LeaffixDispatch</c> return for every node. Carries no position:
  /// a stored position would go stale as soon as a later operator reshaped the tree, so
  /// positional context is only ever handed to callbacks, never recorded in results.
  /// </summary>
  public readonly struct NodeAccumulation<TNode, TAccumulate>
  {
    /// <summary>Pairs <paramref name="node"/> with <paramref name="accumulate"/>.</summary>
    public NodeAccumulation(TNode node, TAccumulate accumulate)
    {
      Node = node;
      Accumulate = accumulate;
    }

    /// <summary>The source node.</summary>
    public readonly TNode Node;

    /// <summary>The accumulation the pass computed for this node.</summary>
    public readonly TAccumulate Accumulate;

    /// <inheritdoc/>
    public override string ToString() => $"{Node} <- {Accumulate}";
  }
}
