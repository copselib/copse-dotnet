# The Walker Factory Design: the door-only walkable, the terrain SPI, and the receipts rollout

**Status:** SPEC (2026-08-14) — ratified in conversation, unbuilt. Supersedes
WALKABLE_CONTRACT_DESIGN.md §1a's contract shape (and resurrects its withdrawn terrain
split in a corrected role); completes §12's two-audience policy; instantiates
CATEGORY_THEORY_SURVEY.md §10's foundation.
**Branch:** `experimental/walker`

> **ITreenumerable is an enumerator factory. IWalkableTreenumerable is a tree walker
> factory.**
> — the charter, verbatim (Jason, 2026-08-14)

## 1. The contract

```csharp
public interface IWalkableTreenumerable<TValue, THandle> : ITreenumerable<TValue>
{
  TreeWalkerResult<TValue, THandle> TryGetTreeWalker();   // the door; miss = empty forest
}
```

One member. The walkable's job is the handshake: manufacture a stance, bound to the best
terrain the source affords, and exit the story. It appears in no runtime call path — like
`IEnumerable` after `GetEnumerator` returns. `THandle` stays on the contract because
handles are public identity (stored, re-entered); the door's product carries them.

The four adjacency probes LEAVE the consumer contract. They do not die — they demote to
the provider SPI (§2). No consumer-facing signature mentions a probe again; the walker is
the entire public navigation API.

## 2. `ITreeTerrain` — the provider SPI, resurrected

```csharp
public interface ITreeTerrain<TValue, THandle>          // Core-hosted; provider-side
{
  TValue GetValue(THandle handle);
  ParentResult<THandle> TryGetParent(THandle handle);
  ChildResult<THandle> TryGetChildAt(THandle handle, int childIndex);
  ChildResult<THandle> TryGetRootAt(int rootIndex);
  // grows ONLY by receipts (§6) — e.g., TryGetNextSibling, TryGetExtent, when a
  // migration demands them; each member lands with its per-terrain price sheet.
}
```

This is §1a's withdrawn `ITreeTerrain`, returned in its correct role. The withdrawal
clause hedged: *"if that citizen ever arrives, inserting it is a compatible change then."*
The citizens arrived — every adjacency index, every lens view, every memo pull-through,
every foreign adapter is one — and the role inverted from the original proposal: not a
public supertype consumers meet, but the SPI standing BEHIND the walker. Public (foreign
providers must implement it; a DOM adapter is the canonical citizen), positioned as
provider surface exactly like the store SPIs, and invisible from consumer code.

**Roots stay on the terrain** (revising §1a's extent finding for the new shape): roots
are the virtual forest-root's child group — the ratified reading — and a walker standing
at a root steps to the next root through the same sibling machinery as any other sibling
group. The contract's door is sugar over `TryGetRootAt(0)`.

## 3. The walker

```csharp
public readonly struct TreeWalker<TValue, THandle>       // Core; two fields, unchanged count
{
  private readonly ITreeTerrain<TValue, THandle> _Terrain;   // was: the walkable
  public readonly THandle Focus;
  // ctor stays the trust door; steps stay result-typed; NO new state, ever --
  // the surface may grow (by receipts), the carrier may not.
}
```

The field re-aims one level down: **walker → terrain → arithmetic**, one dispatch, the
walkable never in the path (Jason's stack-frame ruling). The door binds the terrain at
birth — the concrete capture installs its span-backed index, the memo its pull-through,
the lens its rewriting view; the walker never knows which physics it holds
(terrain-at-birth: probes-at-birth promoted to the design's organizing move).

Later lever, unbuilt until measured: `TreeWalker<TValue, THandle, TTerrain>` with a
struct constraint (the `TChildEnumerator`/`TStore` pattern's third application) for
zero-dispatch navigation. Navigation is consumer-scale; bulk work bypasses walkers
through the span paths — the tiering the receiver-smart measurements established.

## 4. The algebra drops to terrain altitude

Lenses and `Extend` are TERRAIN TRANSFORMERS: terrain in, terrain out.

- `SubtreeTerrain(terrain, root)` — the severed view (two answers rewritten).
- `PruneAfterTerrain(terrain, predicate)` — the restriction lens's adjacency half.
- `ExtendTerrain(terrain, observer)` — the comonad's relabeling; the observer receives
  **the walker** (the vantage as a value — its honest type at last, replacing the
  unbundled `(source, handle)` pair).

The walkable/walker surfaces are door veneers over these. Pair-citizenship (a lens's
stream half = the streaming operator) is unchanged in substance; the seam label moves.

## 5. Staging — three phases, green throughout

- **A (additive):** mint `ITreeTerrain`, retype the walker's field, add the door to the
  existing contract. The old probes remain on the contract, implemented by delegating to
  each citizen's terrain. Suite stays green; nothing consumer-visible breaks.
- **B (migration):** roll consumers onto walker/door spellings one extension at a time —
  the operators' fallback folds first (already walker-shaped; cheapest receipts), then
  `SpanningSubtree`, `Subtrees`, the acquisition scans, the battery, the catalog. Each
  migration keeps its oracle: old buffer spelling ≡ new walker spelling, pinned.
- **C (the cut):** delete the probes from `IWalkableTreenumerable` when nothing speaks
  them. Breaking, release-notes flag, pre-beta.

## 6. The receipts methodology (the surface-discovery engine)

**Every time a migrating operation does something with a buffer that the walker cannot
do, that is a discovered missing walker feature.** No member lands speculatively; each
arrives with a receipt naming the migration that demanded it, its per-terrain price
sheet, and its oracle pin. The restraint rule, automated; consumer-names-the-signature,
applied to the whole surface. Expected early receipts (predicted, not pre-admitted):
per-focus extent, child stepping, root enumeration, descendant ranges (ordinal-borne —
extensions over `int`-handled terrains, never contract members), sibling steps
(`TryGetNextSibling` cheap everywhere; `Previous` honest about its preorder asymmetry).

## 7. Deferred-by-receipts ledger

Door arity and the jump (`walker.At(handle)`? — lean: walker-side, contract stays one
member) · `Terrain` property visibility on the walker · `GetHandles`/`GetHandlesWithValues`
relocation · every capability member. Settled by the first migration that needs each.

## 8. Carrier-neutrality check (the DAG dual)

`IDagnumerable` + `TryGetDagWalker()`; the terrain SPI's dual speaks CSR rows and
arrival groups; Sourcefix/Sinkfix schedules unchanged; the receipts methodology carries
over verbatim. Nothing in this design is tree-shaped except the tree instance.
