using System.Threading.Tasks;

namespace Copse.Async
{
  /// <summary>
  /// A walkable plus a focus: one node of a tree, together with everything needed to navigate
  /// away from it. Two words of data, held by value and owning nothing -- many walkers share
  /// one topology, and stepping never mutates: every move returns a NEW walker, so a walker is
  /// a value, not a cursor. (It is the carrier of the full-context Store comonad;
  /// design-docs/CATEGORY_THEORY_SURVEY.md §4 has the theory, which you do not need in order to
  /// use it.)
  ///
  /// <para>THE INVARIANT: a walker is always focused on an actual node. "Not yet positioned"
  /// is traversal-protocol state (the forest-root convention, the treenumerator's
  /// before-first stance) and deliberately has no walker spelling -- extract must always
  /// have a value to return, so the unfocused state is not a member of the carrier. Every
  /// creation path (the <c>GetTreeWalkerAt</c>/<c>TryGetTreeWalkerAtRootIndex</c> doors, the step results,
  /// <c>Duplicate</c>'s labels) supplies a real handle. The runtime manufactures
  /// <c>default</c> instances anyway; per the <see cref="ChildResult{TNode}"/> convention,
  /// that value is invalid and must not be used.</para>
  ///
  /// <para>This type carries the CARRIER and the navigation the contract alone affords:
  /// <see cref="GetValueAsync"/> reads the focused value, and the step members move the focus.
  /// The operator algebra over walkers -- <c>Extend</c>, <c>Duplicate</c>, <c>Subtree</c>, and
  /// the acquisition doors -- lives in the Linq tier as extension methods, the same way
  /// <c>ITreenumerable</c> lives here while <c>Select</c> and <c>Where</c> live there.</para>
  ///
  /// <para>Navigation is bidirectional: <see cref="MoveToParentAsync"/> is legal because the
  /// focus keeps its ancestors, which is what separates this presentation from the severed
  /// subtree view <c>Subtrees()</c> ships.</para>
  /// </summary>
  public readonly struct AsyncTreeWalker<TValue, THandle>
  {
    /// <summary>The provider's mint: construction is pure pairing -- the topology flows in,
    /// the walker flows out -- and it exposes the topology to nobody who did not already hold
    /// it. Two audiences, two mints: consumers get walkers from the acquisition doors and
    /// from <see cref="At"/>, while a provider implementing
    /// <see cref="IAsyncWalkableTreenumerable{TValue, THandle}"/> mints here to answer its own
    /// door. Validity is the caller's oath, exactly as at the jump: <paramref name="focus"/>
    /// is presumed a real node of <paramref name="topology"/>, a forged one detonates at the
    /// first probe, and <c>default</c> remains the one invalid inhabitant.
    /// (design-docs/WALKER_FACTORY_DESIGN.md §10 records why the mint is public.)</summary>
    public AsyncTreeWalker(IAsyncTreeTopology<TValue, THandle> topology, THandle focus)
    {
      Topology = topology;
      Focus = focus;
    }

    /// <summary>The topology this walker stands on, exposed so builders and providers can
    /// navigate without a focus. The two surfaces differ by SIGNATURE, which is how you choose
    /// between them: a topology navigates relative to ANY handle, so its methods take one; a
    /// walker navigates relative to its own focus, so its methods do not. Same physics, two
    /// frames. Holding a topology grants no extra power -- probes are read-only and wholesale
    /// views are read-only.</summary>
    public readonly IAsyncTreeTopology<TValue, THandle> Topology;

    /// <summary>The handle this walker stands at. Always an actual node -- see the invariant.</summary>
    public readonly THandle Focus;

    /// <summary>Extract: the value at the focus. Always valid -- a walker cannot be unfocused.
    /// (A probe, hence a method: on a growing source the read is demand.)</summary>
    public ValueTask<TValue> GetValueAsync() => Topology.GetValueAsync(Focus);

    /// <summary>The jump: a sibling stance on the SAME topology, standing at
    /// <paramref name="handle"/>. This is how a stored handle becomes a stance again. No probe
    /// fires and nothing is validated: <paramref name="handle"/> is presumed to be this
    /// topology's own, and the foreign-handle clause applies.</summary>
    public AsyncTreeWalker<TValue, THandle> At(THandle handle)
      => new AsyncTreeWalker<TValue, THandle>(Topology, handle);

    /// <summary>Single upward step. The STEP can fail (a root has no parent); the stance
    /// cannot -- so the result is a by-value maybe, never an unfocused walker.</summary>
    public async ValueTask<AsyncTreeWalkerResult<TValue, THandle>> MoveToParentAsync()
    {
      var parentResult = await Topology.TryGetParentAsync(Focus).ConfigureAwait(false);

      return parentResult.HasParent
        ? new AsyncTreeWalkerResult<TValue, THandle>(new AsyncTreeWalker<TValue, THandle>(Topology, parentResult.Parent))
        : default;
    }

    /// <summary>Single downward step to the child at <paramref name="childIndex"/> in sibling
    /// order, or an empty result past the last child.</summary>
    public async ValueTask<AsyncTreeWalkerResult<TValue, THandle>> MoveToChildAsync(int childIndex)
    {
      var childResult = await Topology.TryGetChildAtAsync(Focus, childIndex).ConfigureAwait(false);

      return childResult.HasChild
        ? new AsyncTreeWalkerResult<TValue, THandle>(new AsyncTreeWalker<TValue, THandle>(Topology, childResult.Child.Node))
        : default;
    }

    /// <summary>The third step: a stance at the root at <paramref name="rootIndex"/> of the
    /// SAME topology. Roots share no parent/child edge, so this is the one adjacency the other
    /// steps cannot reach -- it walks the virtual forest root's child group exactly as
    /// <see cref="MoveToChildAsync"/> walks a node's. Empty result past the last root. With it,
    /// the step set covers the topology's whole probe surface, so a walker never has to be
    /// opened up for its topology.</summary>
    public async ValueTask<AsyncTreeWalkerResult<TValue, THandle>> MoveToRootAsync(int rootIndex)
    {
      var rootResult = await Topology.TryGetRootAtAsync(rootIndex).ConfigureAwait(false);

      return rootResult.HasChild
        ? new AsyncTreeWalkerResult<TValue, THandle>(new AsyncTreeWalker<TValue, THandle>(Topology, rootResult.Child.Node))
        : default;
    }

  }
}
