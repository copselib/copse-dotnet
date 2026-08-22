using Copse.Core.Async;

namespace Copse.Linq
{
  /// <summary>
  /// One node's expansion under <c>SelectMany</c>: a forest that replaces the node, plus the
  /// placement of the SLOT -- where the node's own children, each already rewritten, re-hang.
  /// A forest with no slot stands alone and the children are dropped. The four special values
  /// are the library's own reshapings: <c>Return</c> (the single node, children under it --
  /// Select's unit), <c>Promote</c> (no node, children promoted -- Where's drop arm),
  /// <c>Drop</c> (nothing, children gone -- PruneBefore's arm), <c>Leaf</c> (the node kept,
  /// children gone -- PruneAfter's arm). The general forms take any forest.
  /// (design-docs/SELECTMANY_DESIGN.md records the semantics and their laws.)
  ///
  /// <para><c>default</c> is <c>Drop</c>: no forest, no slot.</para>
  /// </summary>
  public readonly struct AsyncExpansion<TResult>
  {
    internal AsyncExpansion(IAsyncDepthFirstTreenumerable<TResult> forest, SlotPlacement placement)
    {
      Forest = forest;
      Placement = placement;
      HasSingleValue = false;
      SingleValue = default;
    }

    // The one-node forest, structural: Return and Leaf are the bind's hot path, and a one-node
    // treenumerable per node costs three allocations the operator can replace with two
    // emissions (measured: the theorem rows of the SelectMany benchmarks).
    internal AsyncExpansion(TResult singleValue, SlotPlacement placement)
    {
      Forest = null;
      Placement = placement;
      HasSingleValue = true;
      SingleValue = singleValue;
    }

    // The Leaf row of the quartet's composition table (SELECTMANY_DESIGN.md Addendum V): a
    // prune-after ahead of this expansion drops the slot, keeping the forest.
    internal AsyncExpansion<TResult> WithoutSlot()
      => HasSingleValue
        ? new AsyncExpansion<TResult>(SingleValue, SlotPlacement.None)
        : new AsyncExpansion<TResult>(Forest, SlotPlacement.None);

    internal bool HasSingleValue { get; }

    internal TResult SingleValue { get; }

    /// <summary>The replacing forest, or <c>null</c> for none.</summary>
    public IAsyncDepthFirstTreenumerable<TResult> Forest { get; }

    /// <summary>Where the node's children re-hang.</summary>
    public SlotPlacement Placement { get; }
  }

  /// <summary>The expansion vocabulary's factories (non-generic home, for inference).</summary>
  public static class AsyncExpansion
  {
    /// <summary>The single node <paramref name="value"/>, children under it.</summary>
    public static AsyncExpansion<TResult> Return<TResult>(TResult value)
      => new AsyncExpansion<TResult>(value, SlotPlacement.UnderLastRoot);

    /// <summary>No node; the children promoted into the vacated position.</summary>
    public static AsyncExpansion<TResult> Promote<TResult>()
      => new AsyncExpansion<TResult>(null, SlotPlacement.AfterRoots);

    /// <summary>Nothing: the node and its descendants vanish.</summary>
    public static AsyncExpansion<TResult> Drop<TResult>()
      => default;

    /// <summary>The single node <paramref name="value"/> as a leaf: its descendants vanish.</summary>
    public static AsyncExpansion<TResult> Leaf<TResult>(TResult value)
      => new AsyncExpansion<TResult>(value, SlotPlacement.None);

    /// <summary>The general form: <paramref name="forest"/> with the slot at
    /// <paramref name="placement"/>.</summary>
    public static AsyncExpansion<TResult> Of<TResult>(IAsyncDepthFirstTreenumerable<TResult> forest, SlotPlacement placement)
      => new AsyncExpansion<TResult>(forest, placement);
  }
}
