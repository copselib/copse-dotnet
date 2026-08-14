using System.Threading.Tasks;

namespace Copse.Async
{
  /// <summary>
  /// The focused pair, reified: a walkable plus a VALID focus -- the carrier of the
  /// full-context (Store) comonad, the type whose instances are what
  /// docs/CATEGORY_THEORY_SURVEY.md §4 calls "the whole tree, seen from here." Two words of
  /// data, by value, nothing owned: many walkers share one terrain, and stepping never
  /// mutates -- every move returns a NEW walker (the comonad is pure; a stance is a value,
  /// not a machine).
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
  /// <para>The comonad's SPLIT mirrors the monad's (the Core move, 2026-08-14): Core holds
  /// the CARRIER and what the contract alone affords -- <see cref="GetValueAsync"/> is
  /// extract, and the step verbs are the coalgebraic navigation -- while the comonad's
  /// operator algebra (<c>Extend</c>, <c>Duplicate</c>, <c>Subtree</c>, the doors) lives in
  /// the operator tier as extensions, exactly as <c>ITreenumerable</c> lives here while
  /// <c>Select</c>/<c>Where</c> live there: carriers in Core, algebras in Linq, for both
  /// tenants. The vantage is bidirectional (the Store presentation, not the severed cofree
  /// one <c>Subtrees()</c> ships): <see cref="MoveToParentAsync"/> is legal because the
  /// focus keeps its ancestors.</para>
  /// </summary>
  public readonly struct AsyncTreeWalker<TValue, THandle>
  {
    // PUBLIC by design (the Core move, 2026-08-14): construction IS the trust-based door --
    // GetTreeWalkerAt was always just this call -- and the comonad's operator surface (Extend,
    // Duplicate, Subtree, the doors) lives up in the operator tier, which needs to mint
    // walkers. The invariant's content survives untouched: a handle is always supplied;
    // only `default` remains the invalid inhabitant.
    public AsyncTreeWalker(IAsyncTreeTerrain<TValue, THandle> terrain, THandle focus)
    {
      Terrain = terrain;
      Focus = focus;
    }

    /// <summary>The terrain this walker stands on. INTERNAL (the door-only design): consumers
    /// meet exactly one navigation spelling -- the walker's own members -- and never the SPI
    /// behind it; the comonad's operator surface (co-bind, duplicate, the severed view) reads
    /// this half through the family IVT. A vantage is focus × terrain; the focus is public
    /// identity, the terrain is bound physics.</summary>
    internal readonly IAsyncTreeTerrain<TValue, THandle> Terrain;

    /// <summary>The handle this walker stands at. Always an actual node -- see the invariant.</summary>
    public readonly THandle Focus;

    /// <summary>Extract: the value at the focus. Always valid -- a walker cannot be unfocused.
    /// (A probe, hence a method: on a growing source the read is demand.)</summary>
    public ValueTask<TValue> GetValueAsync() => Terrain.GetValueAsync(Focus);

    /// <summary>Single upward step. The STEP can fail (a root has no parent); the stance
    /// cannot -- so the result is a by-value maybe, never an unfocused walker.</summary>
    public async ValueTask<AsyncTreeWalkerResult<TValue, THandle>> MoveToParentAsync()
    {
      var parentResult = await Terrain.TryGetParentAsync(Focus).ConfigureAwait(false);

      return parentResult.HasParent
        ? new AsyncTreeWalkerResult<TValue, THandle>(new AsyncTreeWalker<TValue, THandle>(Terrain, parentResult.Parent))
        : default;
    }

    /// <summary>Single downward step to the child at <paramref name="childIndex"/> in sibling
    /// order, or an empty result past the last child.</summary>
    public async ValueTask<AsyncTreeWalkerResult<TValue, THandle>> MoveToChildAsync(int childIndex)
    {
      var childResult = await Terrain.TryGetChildAtAsync(Focus, childIndex).ConfigureAwait(false);

      return childResult.HasChild
        ? new AsyncTreeWalkerResult<TValue, THandle>(new AsyncTreeWalker<TValue, THandle>(Terrain, childResult.Child.Node))
        : default;
    }

  }
}
