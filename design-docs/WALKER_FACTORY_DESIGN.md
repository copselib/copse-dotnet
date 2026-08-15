# The Walker Factory Design: the door-only walkable, the topology SPI, and the receipts rollout

**Status:** EXECUTED THROUGH STAGE C (A: 16c464e, B: 777adec + c9810c6, C: 2026-08-15 -- the cut). The walkable is a one-member factory; the probes are provider SPI; the walker is the entire public navigation surface. Supersedes
WALKABLE_CONTRACT_DESIGN.md §1a's contract shape (and resurrects its withdrawn topology
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
topology the source affords, and exit the story. It appears in no runtime call path — like
`IEnumerable` after `GetEnumerator` returns. `THandle` stays on the contract because
handles are public identity (stored, re-entered); the door's product carries them.

The four adjacency probes LEAVE the consumer contract. They do not die — they demote to
the provider SPI (§2). No consumer-facing signature mentions a probe again; the walker is
the entire public navigation API.

## 2. `ITreeTopology` — the provider SPI, resurrected (RENAMED from ITreeTerrain, Jason 2026-08-14: topology is the comonad's invariant subject — "mutate the topology and you fall out of the comonad"; the labeling rides on it, stated in the XML doc)

```csharp
public interface ITreeTopology<TValue, THandle>          // Core-hosted; provider-side
{
  TValue GetValue(THandle handle);
  ParentResult<THandle> TryGetParent(THandle handle);
  ChildResult<THandle> TryGetChildAt(THandle handle, int childIndex);
  ChildResult<THandle> TryGetRootAt(int rootIndex);
  // grows ONLY by receipts (§6) — e.g., TryGetNextSibling, TryGetExtent, when a
  // migration demands them; each member lands with its per-topology price sheet.
}
```

This is §1a's withdrawn `ITreeTopology`, returned in its correct role. The withdrawal
clause hedged: *"if that citizen ever arrives, inserting it is a compatible change then."*
The citizens arrived — every adjacency index, every lens view, every memo pull-through,
every foreign adapter is one — and the role inverted from the original proposal: not a
public supertype consumers meet, but the SPI standing BEHIND the walker. Public (foreign
providers must implement it; a DOM adapter is the canonical citizen), positioned as
provider surface exactly like the store SPIs, and invisible from consumer code.

**Roots stay on the topology** (revising §1a's extent finding for the new shape): roots
are the virtual forest-root's child group — the ratified reading — and a walker standing
at a root steps to the next root through the same sibling machinery as any other sibling
group. The contract's door is sugar over `TryGetRootAt(0)`.

## 3. The walker

```csharp
public readonly struct TreeWalker<TValue, THandle>       // Core; two fields, unchanged count
{
  private readonly ITreeTopology<TValue, THandle> _Topology;  // was: the walkable
  public readonly THandle Focus;
  // ctor stays the trust door; steps stay result-typed; NO new state, ever --
  // the surface may grow (by receipts), the carrier may not.
}
```

The field re-aims one level down: **walker → topology → arithmetic**, one dispatch, the
walkable never in the path (Jason's stack-frame ruling). The door binds the topology at
birth — the concrete capture installs its span-backed index, the memo its pull-through,
the lens its rewriting view; the walker never knows which physics it holds
(topology-at-birth: probes-at-birth promoted to the design's organizing move).

Later lever, unbuilt until measured: `TreeWalker<TValue, THandle, TTopology>` with a
struct constraint (the `TChildEnumerator`/`TStore` pattern's third application) for
zero-dispatch navigation. Navigation is consumer-scale; bulk work bypasses walkers
through the span paths — the tiering the receiver-smart measurements established.

## 4. The algebra drops to topology altitude

Lenses and `Extend` are TOPOLOGY TRANSFORMERS: topology in, topology out.

- `SubtreeTopology(topology, root)` — the severed view (two answers rewritten).
- `PruneAfterTopology(topology, predicate)` — the restriction lens's adjacency half.
- `ExtendTopology(topology, observer)` — the comonad's relabeling; the observer receives
  **the walker** (the vantage as a value — its honest type at last, replacing the
  unbundled `(source, handle)` pair).

The walkable/walker surfaces are door veneers over these. Pair-citizenship (a lens's
stream half = the streaming operator) is unchanged in substance; the seam label moves.

## 5. Staging — three phases, green throughout

- **A (additive):** mint `ITreeTopology`, retype the walker's field, add the door to the
  existing contract. The old probes remain on the contract, implemented by delegating to
  each citizen's topology. Suite stays green; nothing consumer-visible breaks.
- **B (migration):** roll consumers onto walker/door spellings one extension at a time —
  the operators' fallback folds first (already walker-shaped; cheapest receipts), then
  `SpanningSubtree`, `Subtrees`, the acquisition scans, the battery, the catalog. Each
  migration keeps its oracle: old buffer spelling ≡ new walker spelling, pinned.
- **C (the cut):** delete the probes from `IWalkableTreenumerable` when nothing speaks
  them. Breaking, release-notes flag, pre-beta.

## 6. The receipts methodology (the surface-discovery engine)

**Every time a migrating operation does something with a buffer that the walker cannot
do, that is a discovered missing walker feature.** No member lands speculatively; each
arrives with a receipt naming the migration that demanded it, its per-topology price
sheet, and its oracle pin. The restraint rule, automated; consumer-names-the-signature,
applied to the whole surface. Expected early receipts (predicted, not pre-admitted):
per-focus extent, child stepping, root enumeration, descendant ranges (ordinal-borne —
extensions over `int`-handled topologies, never contract members), sibling steps
(`TryGetNextSibling` cheap everywhere; `Previous` honest about its preorder asymmetry).

## 7. Deferred-by-receipts ledger

Door arity and the jump (`walker.At(handle)`? — lean: walker-side, contract stays one
member) · `Topology` property visibility on the walker · `GetHandles`/`GetHandlesWithValues`
relocation · every capability member. Settled by the first migration that needs each.

## 8. Carrier-neutrality check (the DAG dual)

`IDagnumerable` + `TryGetDagWalker()`; the topology SPI's dual speaks CSR rows and
arrival groups; Sourcefix/Sinkfix schedules unchanged; the receipts methodology carries
over verbatim. Nothing in this design is tree-shaped except the tree instance.

## 9. The receipts ledger (running)

**2026-08-14 — Stage B, first migrations: `LeaffixScan`'s and `Invert`'s in-place folds
rewritten in pure stance vocabulary.** One depth-first walk of doors + steps + extract per
operator: no handle arithmetic, no handle-space enumeration (`GetHandles` gone), no
re-entry (`GetTreeWalkerAt` gone), no sizes prepass (the mirror's two passes became one).
The walk assigns its own preorder numbering, so the receiver's handle space is never
assumed — which DISSOLVED the `AffordsInPlaceFold` guard: any capture now folds in place,
whatever its layout (a level-order buffer or memo folds through its own probes instead of
paying the engine's re-capture; conformance rows verify against the engine oracle).

**Receipt: ZERO new walker features were needed.** Door family + steps + extract are
fold-complete for structural work; ordinal indexing was only ever speed, and the concrete
span path owns speed. First evidence for the surface-discovery engine: the walker's
starting surface is algorithmically sufficient, and future members must earn entry on
ergonomics or price, not capability.

**2026-08-14 — Stage B complete: the acquisition scans go pure-stance, and the jump lands.**
`GetHandles`/`GetHandlesWithValues` rewritten as stance walks (doors + steps; a row is
where the walk stood and what it extracted there); the battery's provider identity retyped
to `ITreeTopology` (SPI conformance names its subject); the capstone acquisition test speaks
the consumer spelling end to end. **Receipt #2: `At(handle)` — the jump** — a sibling
stance on the same topology, the trust door addressed: stored handles re-enter through a
vantage already held, which is what re-entry must be once the walkable is door-only. The
door machinery clause is ratified in place (`TryGetTreeWalkerAtRootIndex` probes the root
group; doors may touch topology — consumers never need to). Production-side, nothing
outside the SPI's citizens and the door machinery speaks a probe; the law suites' remaining
probe calls are SPI-coherence checks that retype mechanically at Stage C's cut.

**2026-08-15 — Stage C executed: the cut.** `IWalkableTreenumerable` is the charter's
one-member factory — the topology inheritance and probes are gone from the public
contract. The walker's constructor went internal (the doors and the jump are the only
mints). `DoorTopology` landed as the deferral seam — "the topology this walkable's door
will bind," knocked once at the first probe — which let `Extend` and the lens ctors stay
lazy and sync-shaped with no empty-forest special case (the empty door misses honestly
everywhere; pinned). The handle doors rewired to door-then-jump; the probe-suppliers
(buffer, memos, materialize wrapper) dropped their now-orphaned probe members; the three
lens views declare the SPI explicitly (they ARE topologies, handed to walkers by their
doors). Tests reach the SPI through the family seam (`TopologyOf`, door → walker →
topology, Core→tests IVT) — the coherence checks now say what they always meant: walker
steps against raw topology answers. BREAKING, release-notes flag: consumers who probed a
walkable now navigate through the walker; providers implement `ITreeTopology` plus the
door.
