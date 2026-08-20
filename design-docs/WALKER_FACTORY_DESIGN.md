# The Walker Factory Design: the door-only walkable, the topology SPI, and the receipts rollout

**Status:** EXECUTED THROUGH STAGE C (A: 16c464e, B: 777adec + c9810c6, C: 2026-08-15 -- the cut), then §11 (the sentinel completion, 2026-08-20) EXECUTED: the door is TOTAL (`GetTreeWalker()` -- the unfocused stance), the steps answer in `TreeWalkerResult`, and "void" left the vocabulary. Sections 1-10 record the Stage A-C shape; where they disagree with §11, §11 is current. The walkable is a one-member factory; the probes are provider SPI; the walker is the entire public navigation surface. Supersedes
WALKABLE_CONTRACT_DESIGN.md §1a's contract shape (and resurrects its withdrawn topology
split in a corrected role); completes §12's two-audience policy; instantiates
CATEGORY_THEORY_SURVEY.md §10's foundation.
**Branch:** `experimental/walker`

> **ITreenumerable is an enumerator factory. IWalkableTreenumerable is a tree walker
> factory.**
> — the charter, verbatim (Jason, 2026-08-14)

## 1. The contract

> Stage-C shape; §11 made the door total: `TreeWalker<TValue, THandle> GetTreeWalker()`.

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

> Stage-C shape; §11 grew the carrier by exactly one discriminator bit (`_HasFocus`, the
> unfocused stance) and `Focus` became a guarded property -- the "no new state" clause
> below held until the algebra itself demanded the completed carrier.

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
mints). `WalkableTopology` landed as the deferral seam — "the topology this walkable's door
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
topology). Every one of the IVT's four reads (`WalkableTopology`, walker-`Extend`,
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
- **`WalkerTopology`** (with `WalkableTopology`, both now in `Copse.Linq.Topologies` — they
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

### §10 second addendum — Tree.FromTopology (same evening)

Jason's placement question ("does WalkerWalk belong here? Could this be Tree.FromTopology?")
found the TreenumerableFactory pattern repeating: WalkerWalk had ZERO Linq dependencies —
Copse-level machinery marooned in the operator tier, exactly the maroon the 2026-08-14
sweep folded into Tree. Executed public, on the receipt the foreign-provider pin had just
written (the test hand-rolled a child enumerator + engine tree for its streaming half —
twenty lines the internal adapter already automated):

- **`Tree.FromTopology(topology)`** — the Walk adapter on the one creation surface, PUBLIC.
  The open-ecosystem story completes: implement `ITreeTopology` over a native structure and
  BOTH halves of `IWalkableTreenumerable` are one line each — the door through the public
  `TreeWalker` mint, the streams through `FromTopology`.
- The labeled overload DIED: `ExtendWalkable` is a topology whose `GetValue` IS the
  observation, so it self-feeds (`FromTopology(this)`) — the labeling arrow resolves
  during the pull as its own probe. `SubtreeWalkable` already self-fed; now both lenses do.
- `WalkerWalk` deleted; the frame struct moved down as `(Async)TopologyChildEnumerator`
  beside the engine it serves. The walk floor of the tower cost the codebase one struct
  and one public factory method, total.

### §10 third addendum — the frame-of-reference ruling (the same night, Jason's review)

Reviewing the re-plumb, Jason found `WalkerTopology` "a little silly" — a wrapper around a
walker wrapping a topology, imitating a topology by step-dance. The interrogation that
followed ended the hiding games entirely:

- **The checkmate question**: making the walker castable to `ITreeTopology` (the explicit-
  impl proposal) admits topology-holding is safe — "and if that's the case, why are we
  playing these games to hide it from the consumer?" The safety rationale had dissolved at
  the paternalism resolution; everything since had served only its ergonomic shadow.
- **The frame-of-reference principle (Jason, ratified)**: TreeWalker does NOT implement
  `ITreeTopology` — the two types are distinguished by their signatures' frame. *A topology
  navigates relative to ANY handle, and its method signatures reflect that. A walker
  navigates relative to its FOCUS, and its signatures reflect that.* Same physics, two
  frames; neither impersonates the other.
- **The substrate principle (Jason, verbatim)**: "The topology didn't need to be hidden;
  the topology's substrate did — and we're doing that. We never expose the store outside
  the topology." The seal lives at the topology/STORE boundary (implementations sealed,
  accessors internal, wholesale views read-only), not at walker/topology. Every torn-down
  mechanism of the evening — the IVT, the wrapper, the explicit-impl proposal — was the
  seal enforced one layer too high.

Executed: `TreeWalker.Topology` is a PUBLIC readonly field (the vantage's bound-physics
half; XML doc carries both principles); `WalkerTopology` deleted (an eager bridge from a
vantage in hand is just the property read); `WalkableTopology` keeps the deferral role and
resolves to the door walker's property; the counit closure fix reverted (labels mint over
the same topology reference natively); the c5f9c2d internalization ruling formally
REVERSED, with the paternalism resolution as justification. What survives untouched:
the public ctor mint, `MoveToRoot`, `Tree.FromTopology`, zero IVTs, the foreign-provider
pin, the Topologies namespace.

### §10 fourth addendum — LazyTopology on the creation surface (the review's last kill-attempt)

Jason hunted the last topology class. The irreducibility analysis held — the deferral is
genuine load-bearing state (knock-once cache + centralized miss semantics; the contract
promises neither cheap nor idempotent doors, so a per-probe knock would trust the weakest
citizen) — but two of his observations relocated and renamed it:

- **The name**: with WalkerTopology dead, the source-pair justification for
  "WalkableTopology" had evaporated; the mechanism was the only identity left, and the
  machinery grammar already had its word. **LazyTopology** — Jason's original proposal,
  right on the second pass ("it was just early: the pair had to die before the mechanism
  was the only identity left").
- **The placement**: zero Linq dependencies — the maroon pattern's third strike
  (TreenumerableFactory, WalkerWalk, now this). Moved to Copse. The no-IVT law forces the
  move to be a PUBLICATION, and the store policy dictates the shape: implementations stay
  sealed, so the public face is a factory returning the contract — **TreeTopology.Lazy(source)**,
  the topology tier's creation surface beside Tree's. The trio now reads as one family:
  Tree.Lazy (treenumerable, call-by-need), TreeTopology.Lazy (topology, call-by-need),
  Tree.FromTopology (streams from adjacency). Receipt: any third-party view over an
  arbitrary walkable faces exactly the lens family's deferral problem; the lenses are the
  public form's first consumers.

Copse.Linq.Topologies — created, populated, and emptied within one night — is deleted.
The operator tier owns zero topology classes.

## 11. The total door — the sentinel completion, reversed into the design (2026-08-20)

Ruling (Jason, reversing the sentinel refusal recorded in CATEGORY_THEORY_SURVEY §11; his
conditions — "the algebra hangs together and the code comes out clean" — discharged in
survey §12): **the sentinel is a walker state, the unfocused stance, and the door goes total.**

**EXECUTED 2026-08-20** (full suite 24,607 green — 24,548 plus the new pins; async sources
authored, sync twins regenerated). The build followed this spec with three pleasant
surprises: (1) every door implementation COLLAPSED to pure construction — the root-0
probe and the `Option` plumbing deleted everywhere, and the three memo doors became
synchronous (`new ValueTask<>(new TreeWalker<>(EnsureTopology()))` — no probe, no await);
(2) `LazyTopology` lost its null-topology branches (the total door always binds physics,
so the empty forest's own topology answers); (3) the acquisition scans went from one
knock PER ROOT to one knock total (roots seeded from the unfocused stance's child group).
`TryGetTreeWalkerAtRootIndex` is spelled door + `MoveToChild(k)` — the sentinel's
MoveToChild literally, per the old §1a resolution. One new citizen as specified:
`TopologyWalkable`, the identity view behind the unfocused stance's `Subtree()`. Pins landed
in `UnfocusedStanceTests` (sync, the mapping tables below) + `AsyncUnfocusedStanceTests` (color
mechanics); the flipped pins were exactly the predicted ones (up-step parity in the law
suites, the foreign-provider climb, the spanning disjoint miss). `HasFocus` shipped under
its placeholder name — the rename window is open until the pre-beta signature pass.

**The four-item delta** (all inside the alpha breaking window, all release-notes flagged):

1. **Door total.** `TryGetTreeWalker()` → `GetTreeWalker()`, returning the unfocused walker
   always; the empty forest is the unfocused walker alone (born inhabited). Try exits per the
   grammar — the acquisition can no longer miss. Breaking on the §1 charter member, and
   the charter reaches its final, `GetEnumerator`-symmetric form: both factories total,
   emptiness answered at the boundary, never at entry.
2. **`MoveToParent` from a root = answer** (the unfocused walker); stepping up from it = the one
   remaining upward miss. **CLIMB HAZARD — the review item to watch**: "step up while it
   succeeds" now terminates standing ON the unfocused stance, one step later than today, where
   `GetValue` throws. Walkers need a focus-detection affordance (`HasFocus`; name open) or
   the `TryGetValue` test, and the climb idiom gets respelled in the XML docs.
3. **`GetValue` throws at the unfocused stance** — the violation channel; `IEnumerator.Current`
   before the first `MoveNext` is the platform precedent. `TryGetValue` (`Option<TValue>`
   shaped) is the lawful value-altitude extract: `None` exactly at the unfocused stance.
4. **Downstream misses become answers.** LCA of disjoint targets = the unfocused stance;
   `SpanningSubtree` of targets in different trees = the spanning under an unfocused walker (miss arity
   drops 2→1 — only the empty-target case remains); the `HasValue` propagation through
   the LCA helpers simplifies away.

**What does not change**: streams and the visit protocol, `Where`, the scan/dispatch
tier, `Extend`, `Subtrees`, all interior walker behavior, every green law pin. Shipped
`Extend`/`Subtrees` are the interior part of the completed extend (survey §12); the
completed form is derived, never an operator. Machinery note: the virtual-root child
group already answers MoveToChild from the unfocused stance (the birth-bound index) — the unfocused stance is
a state of the existing struct over the existing topology. New public surface: the total
door spelling, the focus-detection affordance, and nothing else until receipts demand it.

**Hoist ruling** (survey §12): inclusive is the surface — `Subtree()` at the unfocused walker
IS the source forest, and door-then-hoist is the identity round trip with no case
analysis. Exclusive (strictly-below) stays in the derivation layer as the factoring half.

**Acceptance pins** (transcribed from the ruling conversation's worked mappings; run over
three providers — the empty forest `()`, the forest `a,b`, the tree `a(b(d,e),c)`):

| pin | statement |
|---|---|
| door totality | `GetTreeWalker()` answers on all three sources, empty included |
| round trip | door then inclusive hoist ≡ source (visit-stream conformance), all three |
| stance table, `a,b` | unfocused: `TryGetValue` misses, hoist = `a,b`; at `a`/`b`: value answers, hoist = single-root forest |
| stance table, `a(b(d,e),c)` | at `a`: hoist single-rooted at a; at `b`: hoist = `b(d,e)`; no unfocused special case anywhere in the map |
| climb | from `d`: `MoveToParent` answers ×3 (b, a, unfocused), misses ×4; HasFocus false only at the top |
| counit unfocused | duplicate of the unfocused walker: label at focus = the walker itself — the outer extract forests used to break |
| completed extend | root `f(unfocusedWalker)` + interiors `Extend(f)` satisfies extract∘extend = f at every focus, the unfocused stance included |
| violation channel | `GetValue` at the unfocused stance throws; `TryGetValue` misses; both pinned |
| spanning | disjoint targets answer unfocused; empty targets still miss |

**Open for the build**: the focus-detection affordance's name; whether
`TryGetTreeWalkerAtRootIndex` respells now that the door beneath it is total; the axis
self-inclusion grammar (NOT ruled here — the completion gives the catalog's AndSelf
question a principled frame: or-self = +focus row, contributed exactly when valued — but
the axis default is its own decision).

### §11 perf addendum — the promotion cliff and the flat step result (2026-08-20)

The BufferProbes history-bench (pre-sentinel HEAD vs the completion, same box) found the
warm walker sweep at ~2x (8 → 15–27 ms/1M nodes, run-dependent) with the topology layer
untouched (direct probes identical) and real-workload rows mild (memos +10–20%, span
paths flat). Scratchpad-harness bisection decomposed it:

- **The nesting, not the guards.** `Option<TreeWalker>` was always 24 bytes and always
  memory-class on the SysV ABI; the baseline survived on inlining + struct promotion.
  Adding the walker's focus bool took the nested aggregate from 3 leaf fields to 4,
  off the JIT's promotion path — a 2-field control walker restored baseline exactly,
  and a 16-byte/3-leaf flags prototype ran the FULL unfocused-guard semantics at ~9 ms.
  Ruled-out suspects, each measured ~nil: inline `throw new` (ThrowHelper fix),
  `AggressiveInlining`, cold-arm extraction.
- **The shipped consequence: `TreeWalkerResult`** (resurrected name) — the step family's
  flat three-state answer (missed / focused / unfocused in one outcome byte packed in
  the walker's padding; 16 bytes, three fields; internal mints, option-shaped surface:
  `HasValue`/`Value`/`TryGetValue`, so call sites carried over textually). Steps and the
  root-index door speak it; `SpanningSubtree` stays `Option`-spoken (an operation, not a
  step). The `Option` grammar rule stands; the step family holds a measured exemption.
- **The honest residual: a JIT devirtualization roulette.** With the focus bit present in
  ANY 3-leaf spelling, the sweep's steady state lands process-by-process anywhere in
  9–18 ms (guarded devirt + PGO either fully inline the probe chain or partially don't);
  only the discriminator-free two-field layout rolled a reliable 8. Absolute scale: ~3.5 ns per
  step-op at the slow end, on the most walker-hostile pattern that exists. Doctrine
  unchanged: the walker is the readable vocabulary; bulk sweeps belong to the span paths
  and to `walker.Topology` (public). Bencher watch: the BufferProbes family's warm rows
  will read noisy across CI runs until the roulette is understood; judge same-run ratios
  only. Revisit hook: .NET's physical-promotion work may erode the cliff from under us.
