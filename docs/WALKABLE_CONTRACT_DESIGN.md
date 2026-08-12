# Walkable Contract Design: terrain, extent, and the buffer re-parent

**Status:** DESIGN — nothing here is built; the codegen is not touched until every OPEN
below is ruled. Companions: [WALKER_DESIGN.md](WALKER_DESIGN.md) (the findings this design
executes), [CATEGORY_THEORY_SURVEY.md](CATEGORY_THEORY_SURVEY.md) §4 (the comonad the
contracts serve).
**Branch:** `experimental/walker`
**Date:** 2026-08-13

The three ratified decisions this design implements:

1. **Buffer re-parenting** (2026-08-12): `ITreenumerableBuffer` implements the walkable
   contract — *captures are never address-poor*. Two-way collapse vetoed (computed and
   external terrains stay buffer-free comonad hosts).
2. **The GetRootAt finding** (2026-08-12): `GetRootAt` is `GetChildAt` with the parent
   erased because the parent is the unfocused stance — extent, not terrain. The comonadic
   machinery never touches it; its clients are all protocol-side.
3. **The TreeWalker carrier** (2026-08-12): the comonad lives on `TreeWalker` (terrain +
   valid focus); the walkable never carries a focus; entry is partial at the doors, life
   inside is total.

---

## 1. The contract family (sync shapes; async twins in §3)

### 1a. Terrain — the comonad's home (PROPOSED, name OPEN-1)

```csharp
// Copse (core-adjacent, color-generated) — the s → a of the Store comonad
public interface ITreeTerrain<TValue, THandle>
{
  TValue GetValue(THandle handle);
  ParentResult<THandle> GetParent(THandle handle);
  ChildResult<THandle> GetChildAt(THandle handle, int childIndex);
}
```

Everything handle-parameterized; no entry, no stream, no finiteness claim. **Citizens that
need exactly this and nothing more:** `TreeWalker` (all of its members consume only these
three probes — `Subtree()` included, since the severed view is terrain-plus-frontier),
`Extend`'s observers, and the rootless sources the finiteness law protects (Collatz-style
computed adjacency, infinite grids, external structures with no enumerable root list) —
sources that today **cannot implement the walkable contract at all** because they cannot
honestly answer `GetRootAt`. Minting terrain is justified by the capability-lattice
restraint rule: the cell has citizens.

### 1b. The composite — walkable = terrain + extent + stream (PROPOSED)

```csharp
public interface IWalkableTreenumerable<TValue, THandle>
  : ITreeTerrain<TValue, THandle>, ITreenumerable<TValue>
{
  ChildResult<THandle> GetRootAt(int rootIndex);   // the extent
}
```

- **No separate extent interface.** The restraint rule again: no known citizen affords
  extent without stream or stream without extent (a source that can enumerate its roots can
  stream, and vice versa — the stream *starts* at the roots). `GetRootAt` stays a member of
  the composite; the smell is healed by the seam being *visible* (terrain above, extent
  below), not by minting an interface for an empty cell.
- The composite's meaning is unchanged: everything that is an `IWalkableTreenumerable`
  today remains one, with the same four members. The split inserts terrain *above*; no
  implementer changes shape.

### 1c. The buffer re-parent (RATIFIED; handle clause OPEN-2)

```csharp
public interface ITreenumerableBuffer<TValue>
  : IWalkableTreenumerable<TValue, int>          // was : ITreenumerable<TValue>
{
  // existing buffer surface unchanged
}
```

- `THandle` pins to `int` for the buffer family: **handles are layout ordinals** — the
  index of the node in the buffer's flat encoding. Guessable, dense, zero-based.
- **The layout-instability clause (OPEN-2):** a preorder-backed capture's handle k is a
  *preorder* ordinal; a level-order-backed capture's is a *level-order* ordinal. The same
  tree materialized under the two layouts has two different handle spaces. Handles are
  opaque per the walkable contract, so this is consistent — but it must be documented on
  the buffer contract: handles are per-capture coordinates, never portable across captures
  or layouts. (This is also the status quo for `MaterializeWalkable` today.)
- The **long migration** owns handle width. `int` here now; when positions go `long`,
  buffer handles ride the same sweep (coordinate with that branch — buffer handles ARE
  positions).
- `IWalkableTreenumerableBuffer` **dissolves** (its cell becomes `ITreenumerableBuffer`
  itself); `WalkableTreenumerableBuffer` (the internal wrapper) **dies**;
  `MaterializeWalkable` **collapses into `Materialize`** (OPEN-3: breaking rename,
  release-notes flag per house habit).

## 2. Implementer matrix — who answers the probes, and how

| Implementer | Probe strategy |
|---|---|
| `TreenumerableBuffer` / `MaterializeTreenumerable` (completed captures) | The lazy index machinery the walker PoC already built: preorder store → CSR child index + parent index (~2n ints, one pass, built on first probe); level-order store → child arithmetic native, parent index via the stackless two-cursor merge. Zero cost if never probed. |
| Memoize buffers (live feed) | **Pull-through**: a probe is demand — `EnsureBuffered`/`EnsureChildAvailable` force the source exactly as far as the probe reaches (grow-precedes-read already speaks this). A probe past the frontier of an incomplete memo advances the frontier. |
| `AsyncMaterializeTreenumerable` (lazy declared-layout capture) | Probe forces the capture (the completion seam already exists for the stream side; adjacency rides the same `CompleteAsync`). |
| Computed / external terrains | Unchanged — terrain (or walkable) implementers by hand, never buffers. |

The PoC classes `WalkablePreorderTreenumerable` / `WalkableLevelOrderTreenumerable`
(OPEN-4): their index machinery is absorbed into the buffer implementations as shared
internal builders; the classes themselves either retire to `internal` (store-in-hand
walkables for tests/machinery) or die into the buffers. Lean: internal, since
`Materialize` will hand out walkable buffers directly and no consumer needs to name them.

## 3. The async twins (PROPOSED; shapes OPEN-5)

The color rule: contracts are single-sourced async → generated sync. The sync PoC files
(`IWalkableTreenumerable.cs`, `ParentResult.cs`, the walkable classes) were hand-written
for speed; crossing colors means authoring the async sources and demoting the sync files
to `.g.cs` twins.

```csharp
// Copse.Async — the async terrain (transcribes to ITreeTerrain by await-strip)
public interface IAsyncTreeTerrain<TValue, THandle>
{
  ValueTask<TValue> GetValueAsync(THandle handle);
  ValueTask<ParentResult<THandle>> GetParentAsync(THandle handle);
  ValueTask<ChildResult<THandle>> GetChildAtAsync(THandle handle, int childIndex);
}

public interface IAsyncWalkableTreenumerable<TValue, THandle>
  : IAsyncTreeTerrain<TValue, THandle>, IAsyncTreenumerable<TValue>
{
  ValueTask<ChildResult<THandle>> GetRootAtAsync(int rootIndex);
}

public interface IAsyncTreenumerableBuffer<TValue>
  : IAsyncWalkableTreenumerable<TValue, int>
{ /* existing surface unchanged */ }
```

- `ValueTask` because completed captures answer synchronously (the common case after the
  first probe) while memo/lazy buffers must be able to await a pull — exactly `ValueTask`'s
  design case.
- **No CancellationToken on probes** — the edges-only ruling from the cancellation pass:
  tokens live at acquisition edges, and the sync transcription elides all CT plumbing.
  A probe is a pull; pulls don't take tokens.
- `ParentResult` moves to an async-side source (namespace `Copse.Async`) with a sync
  `.g.cs` twin — the shape was designed await-strip-legal from day one (its own doc says
  so); `ChildResult` is already dual.
- The probe-struct and index-machinery sources are authored async and generated sync,
  same as the store family — meaningful names preserved per the codegen convention.

## 4. Conformance plan

- **Adjacency oracle battery**: for every buffer type × the conformance corpus, the four
  probes must agree with the engine-derived adjacency (parent/child/root relations read
  off the oracle's visit stream). One battery, all buffer implementers ride it — the
  `VisitStreamConformance` tradition, extended to the adjacency surface.
- **The walker law suites re-run unchanged** — `TreeWalkerLawTests`, `SubtreesLawTests`,
  `WalkerComonadLawTests` — with `MaterializeWalkable` call sites mechanically renamed to
  `Materialize`. The laws themselves must not notice the re-parent.
- **Pull-through pins** for memo buffers: a probe past the frontier advances the frontier
  exactly as far as the probe (no over-materialization); a probe behind it reads without
  touching the source.
- Lattice pins updated: the walkable-only cell keeps its citizen (a raw store walkable or
  computed terrain); the "wears both" pin retargets `Materialize`'s result.

## 5. Execution order (once the OPENs are ruled)

1. Terrain interface + async sources + manifest entries; sync PoC files demoted to twins.
   Full suite green (pure insertion — no behavior change).
2. Buffer re-parent in the async sources; probe implementations on the concrete buffers;
   regeneration; adjacency battery lands. Full suite green.
3. Dissolutions: intersection interface, wrapper, `MaterializeWalkable` → `Materialize`;
   docs and survey rows updated. Full suite green.
4. Coordination notes to the long-migration branch (handle width) recorded in memory and
   the migration's own docs when it lands.

## 6. The OPEN ledger — rulings owed before code

- **OPEN-1 (naming):** the terrain interface — `ITreeTerrain` (the dialogue's own word;
  honest about NOT being a treenumerable) vs `ITreeAdjacency` vs keeping the walker-family
  grammar some other way. Proposal: `ITreeTerrain`.
- **OPEN-2 (handle clause):** ratify the layout-instability clause — buffer handles are
  per-capture layout ordinals, never portable across captures/layouts.
- **OPEN-3 (the collapse):** `MaterializeWalkable` → `Materialize` is a breaking rename
  (alpha; release-notes flag). Confirm.
- **OPEN-4 (PoC classes):** `WalkablePreorderTreenumerable`/`WalkableLevelOrderTreenumerable`
  retire to internal vs stay public. Proposal: internal.
- **OPEN-5 (async shapes):** `ValueTask` probes, `Async` suffix, no CT (edges-only).
  Confirm the member shapes in §3.
- **OPEN-6 (TreeWalker retype):** `TreeWalker` and `Extend`'s observer signatures retype
  from `IWalkableTreenumerable` to the terrain interface (the comonad types against
  terrain alone; rootless sources become carriers). Note: `Extend`-on-a-walkable keeps
  returning a walkable (its stream half needs the extent); a future terrain-level extend
  would return terrain. Proposal: retype in step 1.
