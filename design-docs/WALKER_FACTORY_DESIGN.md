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

## 10. Open question for the signature pass: the minting contract (2026-08-15)

Raised by Jason at the pre-merge review of the Core→Linq family IVT, and converged in
dialogue to a three-dial decision. Record of the arc:

**The ownership observation.** The topology is semantically the WALKABLE's property — the
tree's adjacency structure; the walker only carries a binding of it (vantage = focus ×
topology). Every one of the IVT's four reads (`DoorTopology`, walker-`Extend`,
walker-`Subtree`, the root-crossing in `TryGetTreeWalkerAtRootIndex`) is a CONSTRUCTION
site reaching back through the walker for the walkable's topology — the walker is merely
the only thing that publicly moves. The IVT compensates for ownership living in one place
and reachability in another.

**The sharp fact that started it.** The contract is currently unimplementable outside the
family: `ITreeTopology` is public SPI, but `TreeWalker`'s ctor is internal and no public
mint exists — a foreign provider (the DOM adapter) can implement the SPI and can never
write the door. The ecosystem is closed in fact while the two-audience documentation reads
open. The IVT is the family's private bridge over that gap.

**The minting contract, split (Jason's framing: "I won't give you the topology, but I'll
build something with the topology for you"):**

- **Inbound (topology in, walker out) — verdict: pure win.** A Core mint
  (`TreeWalker.Over(topology, focus)` / `TryOverRoot(topology)`) exposes nothing: only
  someone already HOLDING a topology can call it, and nothing public hands one out. Solves
  the foreign-provider door and every lens door (a lens IS a topology; it mints over
  itself). No leak in any direction.
- **Outbound (walker applies builders to its hidden topology) — verdict: cannot be both
  public and sealed.** Any recipe/transformer/CPS spelling must hand the builder the
  topology object, and a builder can keep it (`Apply((t, _) => t)` is extraction verbatim;
  a typed transformer can stash the argument). A structurally sealed build-for-me needs a
  CLOSED recipe vocabulary, which drags the lens family into Core — the placement wall.
  The honest options are exactly two: the IVT (the hard seal — "only the family's
  builders receive the topology" is precisely what IVT says; the current design is the
  honest spelling of that rule, not a hack) or a CPS window (`walker.Apply(recipe)` — a
  soft seal of pure ceremony).

**The paternalism resolution (Jason).** The seal protects nothing but the autocomplete
list. `ITreeTopology` exposes four read-only probes; every implementer is `internal
sealed` (the cast to reach a store cannot be NAMED outside the family); store accessors
are internal and wholesale access is read-only-struct-capped regardless. Topology-holding
was never dangerous — only casual probe DISCOVERABILITY was the two-spellings concern,
and that is ergonomics, not safety. The outbound dial is therefore low-stakes.

**The three dials for the signature pass** (independent; decide jointly with the store
try-door question — they are the same question at two rungs):

1. **The mint** — `TreeWalker.Over`/`TryOverRoot` in Core. Inbound-only, leak-free, opens
   the ecosystem. Recommended regardless of the other dials.
2. **`walker.TryAtRootIndex(k)`** — the jump's sibling for the virtual-root child axis
   (roots are not connected by parent/child steps; this is the one navigation the walker
   cannot express today). Moves no topology; kills the root-crossing IVT read.
3. **The outbound seal** — keep the family IVT (hard seal, lens construction stays
   in-family) OR add the `Apply` window and delete the IVT (open lens construction,
   ceremony seal). Pure taste; safety is layered elsewhere.

Related rulings recorded elsewhere the same day: `Extend`-on-the-contract examined and
rejected (solves neither the mint nor most extraction sites; re-runs the placement wall;
DIM unavailable on net48). The sentinel completion is CLOSED — not happening absent
something extremely compelling (ergonomics over academic correctness; the survey's
"materialized as a seed, not as a node" is the permanent account) — so the door's shape
has no live design question hanging over these dials.

### §10 addendum — EXECUTED same day (the seed-what-breaks experiment)

Jason chose not to wait for the signature pass: the ctor went public and the topology went
PRIVATE simultaneously, and the compile-error census decided the rest. Outcome:

- **All three dials landed at once, and dial 3 resolved itself.** The census showed every
  IVT read was a construction site whose only unreachable answer was the root axis — so
  `MoveToRoot(rootIndex)` (Jason's name: it IS a step, walking the virtual forest-root's
  child group as `MoveToChild` walks a node's; Move stays unmarked per the MoveNext
  precedent) plus the public ctor covered everything. No CPS window, no `Apply`, no seal
  to choose: **all four Core IVTs are deleted** and `Topology` is `private` — tighter than
  the `internal` the discussion started from.
- **`WalkerTopology`** (with `DoorTopology`, both now in `Copse.Linq.Topologies` — they
  are topologies, not treenumerables, so they left the `Treenumerables` namespace): the
  SPI reconstituted from a vantage, every answer a public walker step. The lens builders
  and the door-deferral consume it; the operator tier holds no access a third party lacks.
- **The counit lesson** (caught by the law suites, first run): walker-receiver `Extend`
  must label through the ORIGINAL walker (`walker.At(handle)`), not through the
  reconstituted topology — struct identity (`Duplicate().GetValue()` equals the walker)
  cannot survive wrapper indirection on the label path. The wrapper serves adjacency and
  streaming, where identity never matters; labels stay on the source topology.
- **The open-ecosystem pin**: `ForeignWalkableProviderTests` (in `Copse.Tests`, which
  holds NO Core IVT — its compiling is the proof) implements the contract entirely
  outside the family: native dictionary adjacency, string handles, a one-line door
  through the public ctor, both surfaces coexisting.
- The second contract member (`TryGetTreeWalkerAtRootIndex` on the walkable) was
  considered and REJECTED: derivable correct-by-construction from door + `MoveToRoot`;
  a contract member would re-open per-provider coherence obligations for zero capability
  gain, and the one-member charter sentence stays load-bearing.
- Rejected en route, recorded above: `Extend` on the contract; a named static mint
  (`Over`) — sugar the ctor already spells; a `TryOverRoot` helper — waits for a
  provider receipt.
