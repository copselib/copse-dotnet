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

### 1a. The walkable stays whole — the terrain split WITHDRAWN (Jason, 2026-08-13)

The first draft minted a terrain supertype (`ITreeTerrain`: the three
handle-parameterized probes) above the composite. **Withdrawn on review**, on two grounds:

1. **The motivating smell dissolved with the carrier.** "`GetRootAt` compensates for the
   walkable having no focus" was diagnosed while the walkable was auditioning as the
   comonad carrier — the frozen-`∅` reading offended because the type was half-pretending
   to be a comonad value. Once `TreeWalker` took the carrier role, the walkable is
   unapologetically a SOURCE — terrain, entry, stream — and a source having an entry probe
   is the job description, not a smell. The "children of the virtual forest-root" reading
   survives as understanding (`GetRootAt` is the protocol door), not as an indictment.
2. **The restraint rule forbids the mint.** The advertised citizens for terrain-alone
   evaporate under audit: Collatz has a root (1) and can implement the composite honestly;
   external structures (DOM, visual trees) have roots; infinite sources stream lazily. The
   genuinely-rootless citizen (an infinite grid with no origin) is hypothetical, and the
   capability lattice mints interfaces only for cells with citizens. If that citizen ever
   arrives, inserting a supertype above `IWalkableTreenumerable` is a compatible change
   *then* — deferral costs nothing.

So the contract is today's, unchanged and whole:

```csharp
public interface IWalkableTreenumerable<TValue, THandle> : ITreenumerable<TValue>
{
  TValue GetValue(THandle handle);
  ParentResult<THandle> GetParent(THandle handle);
  ChildResult<THandle> GetChildAt(THandle handle, int childIndex);
  ChildResult<THandle> GetRootAt(int rootIndex);
}
```

`TreeWalker` and `Extend` keep their current signatures (typed to the walkable). The
WALKER_DESIGN.md GetRootAt finding gets a resolution addendum pointing here.

### 1b. The buffer re-parent (RATIFIED; handle clause OPEN-2)

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
| Memoize buffers (live feed) | **Pull-through**: a probe is demand — `EnsureBuffered`/`EnsureChildAvailable` force the source exactly as far as the probe reaches (grow-precedes-read already speaks this; the pattern is already shipped and law-tested — `MaterializeWalkable` today IS a walkable over a growing store, `WalkablePreorderTreenumerable` + `LazyPreorderStore`). `Memoize`'s signature is unchanged — `IMemoizeTreenumerableBuffer` becomes walkable transitively. **Probe-cost disclosure (review 2026-08-13):** upward probes NEVER force — parents precede children in both layouts, so a held handle's ancestry is always already buffered. Downward probes force span-bounded enumeration, layout-shaped: `GetChildAt` on a preorder memo may complete the node's subtree span to answer "no such child" (level-order: one level ahead, cheap); `GetRootAt(k)` on preorder may finish k−1 root subtrees (level-order: leading entries, cheap). The memo's existing bargain (cross-dimension pulls force the same way), inside the laziness policy: documented, not hidden. **Disposal interaction:** disposing retires the feed — the buffered region stays fully walkable; probes past the frontier can no longer race and fail (HOW they fail is OPEN-6). `Complete()` ends the distinction: a completed memo probes like any capture. |
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
// Copse.Async — transcribes to IWalkableTreenumerable by await-strip
public interface IAsyncWalkableTreenumerable<TValue, THandle> : IAsyncTreenumerable<TValue>
{
  ValueTask<TValue> GetValueAsync(THandle handle);
  ValueTask<ParentResult<THandle>> GetParentAsync(THandle handle);
  ValueTask<ChildResult<THandle>> GetChildAtAsync(THandle handle, int childIndex);
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

1. The contract crosses colors: `IAsyncWalkableTreenumerable` + async `ParentResult` +
   manifest entries; the hand-written sync PoC files demote to `.g.cs` twins. Full suite
   green (pure representation change — no behavior, no signature drift).
2. Buffer re-parent in the async sources; probe implementations on the concrete buffers;
   regeneration; adjacency battery lands. Full suite green.
3. Dissolutions: intersection interface, wrapper, `MaterializeWalkable` → `Materialize`;
   docs and survey rows updated. Full suite green.
4. Coordination notes to the long-migration branch (handle width) recorded in memory and
   the migration's own docs when it lands.

## 6. The OPEN ledger — rulings owed before code

*(OPEN-1 and OPEN-6 — the terrain interface and the TreeWalker retype — were withdrawn
with the split, 2026-08-13; see §1a.)*

- **OPEN-2 (handle clause):** ratify the layout-instability clause — buffer handles are
  per-capture layout ordinals, never portable across captures/layouts.
- **OPEN-3 (the collapse):** `MaterializeWalkable` → `Materialize` is a breaking rename
  (alpha; release-notes flag). Confirm.
- **OPEN-4 (PoC classes):** `WalkablePreorderTreenumerable`/`WalkableLevelOrderTreenumerable`
  retire to internal vs stay public. Proposal: internal.
- **OPEN-5 (async shapes):** `ValueTask` probes, `Async` suffix, no CT (edges-only).
  Confirm the member shapes in §3.
- **OPEN-6 (disposed-memo probe failure):** how does a past-frontier probe fail on a
  memo whose feed was retired by disposal? Proposal: **throw** — a retired-feed probe is
  a lifecycle error, not a "no such node" fact; a `HasChild == false` miss would lie
  (the node may exist in the unenumerated source). The buffered region stays fully
  walkable either way.

## 7. Review rulings (2026-08-13, conversation)

- **The up-navigation slot question is a NON-ISSUE for this design** (Jason's ruling):
  knowing an ancestor's child index would only guarantee resuming enumeration of the
  grandparent's child group mid-stream — an enumerator-flavored guarantee the walker
  never made and by design refuses (stances carry no enumeration state; that was the
  cursor/enumerator split). Nothing on the walker's promised surface (`Value`,
  `MoveToParent`, `MoveToChild`, `Extend`, `Duplicate`, `Subtree`) consults a slot, and
  the comonad laws never mention one. The analysis survives as the navigation-price
  spectrum (WALKER_DESIGN.md) — the price sheet for a sibling-step or walk-forever
  feature if one is ever proposed; footnote: buffer layout-ordinals would make such a
  feature near-free on captures (level-order slots are arithmetic) while computed
  terrains pay the scan. Gates nothing here.
- **Memoize under the re-parent** — signature unchanged, walkable transitively, probes
  are demand; the racing-enumerator semantics Jason anticipated are the shipped
  grow-precedes-read pattern. Details folded into the §2 memo row (probe-cost
  disclosure, disposal interaction, `Complete()` as the distinction's end); the one new
  ruling owed is OPEN-6.
