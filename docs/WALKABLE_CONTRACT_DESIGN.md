# Walkable Contract Design: terrain, extent, and the buffer re-parent

**Status:** EXECUTED through step 2 (2026-08-13, commits 5f13931 + f575615). Step 1: the
contract crossed colors. Step 2: the re-parent landed — every capture walkable, probes as
demand via two incremental-scan adjacency engines, the adjacency-oracle battery and the
provider-parameterized law suites green, full suite 24,543. **The compiler forced step 3's
dissolution early** (CS0695: the intersection interface cannot coexist with the re-parent),
so `IWalkableTreenumerableBuffer` and its wrapper are already deleted; **step 3 COMPLETED
same day**: the `MaterializeWalkable` alias is deleted (call sites speak
`Materialize(BufferLayout.Preorder)`), the walkable PoC classes retired to `internal`
(OPEN-4; their `Copse.Tests` suites ride the existing IVT), and the walkable-only lattice
cell's citizens are now the LENS VIEWS (a `Subtrees()` label affords adjacency, owns
nothing). Survey §4 and the surface map's Materialize row updated. **OPEN-6 resolved by precedent**: the memo stores already throw
`ObjectDisposedException` on past-frontier pulls (the replay rule); probes inherit it
through the same `Ensure` calls — nothing was decided, only discovered. Deviation note:
the dimension-dispatched buffer's probes settle by one extra O(n) recapture (rare path,
paid once, layout-respecting); zero-copy would need the capture builders to expose their
stores — queued as a perf follow-up, with the settle's preorder pin on undecided layouts
mirroring the fresh-memo rule. Companions: [WALKER_DESIGN.md](WALKER_DESIGN.md) (the findings this design
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
   manifest entries; the hand-written sync contract and struct demote to `.g.cs` twins.
   (The walkable PoC *classes* stay hand-written sync in this step — their machinery is
   being absorbed and internalized in step 2, and authoring async sources for classes
   about to be restructured is churn; they cross colors in their final shape.) Full
   suite green (pure representation change — no behavior, no signature drift).
2. Buffer re-parent in the async sources; probe implementations on the concrete buffers;
   regeneration; adjacency battery lands. Full suite green.
3. Dissolutions: intersection interface, wrapper, `MaterializeWalkable` → `Materialize`;
   docs and survey rows updated. Full suite green.
4. Coordination notes to the long-migration branch (handle width) recorded in memory and
   the migration's own docs when it lands.

## 6. The OPEN ledger — RULED 2026-08-13 (one deferral)

*(OPEN-1 and OPEN-6-the-first — the terrain interface and the TreeWalker retype — were
withdrawn with the split, 2026-08-13; see §1a.)*

- **OPEN-2 — RATIFIED.** The layout-instability clause, with Jason's precision: handle
  spaces are **per-capture** — two captures of the same tree (or the same tree under two
  layouts) are foreign to each other; handles never travel between captures. (Handles
  were never treenumerator state at all — they live entirely outside the traversal
  protocol.)
- **OPEN-3 — CONFIRMED**, breaking rename, release-notes flag. Jason's discoverability
  caveat ("`Materialize` doesn't advertise that you get a walkable") is honored at the
  doc level: `Materialize`'s XML doc leads with the adjacency affordance ("captures are
  never address-poor"), and the return type shows the walkable members in IntelliSense.
  The name stays honest — walkability is a property of what materialization produces.
- **OPEN-4 — CONFIRMED**, internal. ("Happy to see stuff get deleted.")
- **OPEN-5 — CONFIRMED** as drafted in §3. Spawned a backlog item, recorded below.
- **OPEN-6 (disposed-memo probe failure) — DEFERRED**; must be ruled before step 2's
  memo probes build. Standing proposal: throw — and the revisit framing is that .NET
  already has the idiom for exactly this, `ObjectDisposedException` (a retired-feed
  probe is a lifecycle error; the miss would lie). The buffered region stays fully
  walkable either way.

### Backlog spawned by the review (not this workstream)

- **`MaterializeAsync` — the color bridge** (Jason, OPEN-5 discussion): async-pull into
  a synchronous buffer (`MoveNextAsync` feeding a sync flat store). Neither color can
  host it — `Copse.Linq.Async` does not reference `Copse.Linq` — so it is a citizen of
  a small both-colors bridge package, on the `SimpleSerializer` precedent (the one
  both-colors package). Would also finally fill the "no sync→async adapter exists" gap
  from the package-shape findings, in the async→sync direction.

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
- **The ergonomics review (2026-08-14, the capstone read-through).** Jason's finding: the
  acquisition block interrogates every reader the same four ways — *why materialize? why
  GetHandlesWithValues? what are handles for? do they belong to the pipeline or the
  capture?* Each answer was assigned the home where the question fires: (1) the
  "why materialize" answer is a SIGNPOST on both Core composites' docs (streams have no
  addresses; navigation lives on the capture; the O(n) is disclosed, never hidden) —
  landed; (2) the acquisition idiom became sugar — `FindHandles(predicate)` plural and
  the result-typed singular `FindHandle` (the **sentinel-collision clause**: ordinal
  handles start at zero, so `FirstOrDefault`'s miss masquerades as the root; the miss
  must be a fact, pinned by test) — landed; (3) the claim-ticket language already lives
  on the contract — sufficient; (4) handle-to-capture binding is contractual, not typed —
  see the declined ruling below. Remaining from the review: a getting-started walkthrough
  so the scenario test stops doubling as the front-door document.
- **CONSIDERED AND DECLINED: branded handles.** A per-capture handle type (each capture
  minting its own) would make cross-capture handle misuse a compile error instead of a
  documented clause — the one reader-question the type system cannot currently answer.
  Declined: the generic-parameter tax lands on every signature in the walker tier for a
  mistake the per-capture clause documents and ordinary use rarely invites. Recorded so
  the next person who feels this itch finds the reasoning, not a blank.

## 8. The walker crosses colors (EXECUTED 2026-08-14 — commits 998a764 + 01b3aaf; OPEN-7/8/10 as proposed, OPEN-9 deferral stands. **LOCATION HALF SUPERSEDED BY §9** — the `Copse/Walker` and `Copse.Async/Walker` paths below record the state this section built; the placement pass then moved the carrier to Core and the tier to Linq, and those folders no longer exist)

> **Execution record:** 8a landed (contract family in the Core pair; `NodeAndSiblingIndex`
> to Vocabulary under its charter — "the value types the Core contracts speak"; namespaces
> held per the substrate practice, zero consumer churn). 8b landed (nine async sources in
> `Copse.Async/Walker`, sync twins generated into `Copse/Walker` — base-package citizens;
> the hand-written `Copse.Linq/Walker` core retired; the lens family stays Linq-side;
> `HandleAndValue` to Vocabulary as the walk's node type). OPEN-10 resolved at ZERO
> transcriber cost — the ValueTask regex is text-level and already collapses observer
> arrows, and the `new ValueTask<T>(x)` unwrap existed; async `Extend` takes async
> observers. The Walk adapter resolves labels DURING the pull (the engine's node is the
> `HandleAndValue` pair, its map the sync `.Value` read) — the shape that lets an async
> observer label an engine whose map arrow is synchronous. One deliberate sync surface
> change: `TreeWalker.Value` became `GetValue()` (a probe is a method; the async source is
> the truth). Async mechanics suite: `AsyncTreeWalkerLawTests`, first-run green. Full
> suite 24,532.

The last thing keeping the walker tier PoC-grade: its eleven operator files are
hand-written sync in `Copse.Linq/Walker/`, outside the codegen single-sourcing every
other color-flavored citizen lives under. Two of Jason's instincts shaped this design in
review: "TreeWalker is fundamental enough that it might deserve an interface in
Copse.Core" and "it doesn't belong in Copse.Linq — otherwise we duplicate it for the
async tier."

**The duplication concern is answered by codegen, not shared placement** — the async
tier is authored once (`AsyncTreeWalker` etc. in the async color) and the sync twins are
generated, replacing today's hand-written files: one source, two colors, the house
mechanism. And `Copse.Linq.Traversal` cannot host the walker: that project is the
color-NEUTRAL Linq substrate (`BufferLayout` lives there because both colors speak it),
while `TreeWalker` is color-flavored to the bone — its terrain field is the sync
walkable contract, its async twin has different member shapes. But the instincts land
on two real re-homings, along the RUNG axis:

### 8a. The contract goes to Core (PROPOSED — OPEN-7)

`IWalkableTreenumerable` + `ParentResult`/`ChildResult` move to the
`Copse.Core`/`Copse.Core.Async` pair. The walkable contract extends `ITreenumerable`,
is the fourth rung of the capability ladder, and is implemented by every buffer in the
library — it sits in the concrete `Copse` project only because the PoC put it there. By
the same logic that homes `ITreenumerable` in Core, the walkable rung belongs beside it.
(Mechanics: the async sources move projects and the manifest rows re-point; the
transcription is unchanged.)

### 8b. The walker core sinks a rung (PROPOSED — OPEN-8)

Dependency audit: `TreeWalker`, `TreeWalkerResult`, `Extend`/`ExtendWalkable`,
`Subtrees`/`SubtreeWalkable`, `WalkerWalk`, the doors (`WalkerAt`/`GetRootWalker`), and
`GetHandles` consume only the walkable contract and the hierarchical engine — both
`Copse`-level (contract Core-bound per 8a). Only the LENS family (`PruneAfterWalkable`
and future Select/PruneBefore lenses) needs `Copse.Linq`'s operator machinery. So the
walker core is authored in `Copse.Async` and generated into `Copse` — base-package
citizens, which is Jason's "fundamental enough" instinct landing as PACKAGE PLACEMENT:
the walker ships with the engine and the factories, not with the operators. The lens
family stays a `Copse.Linq.Async` → `Copse.Linq` pair.

### 8c. `ITreeWalker` is deferred pending a second citizen (PROPOSED — OPEN-9)

Three findings from the interface review: (1) interface-typed walkers box — the
measured lesson; the interface may exist only as a generic CONSTRAINT (the
`TChildEnumerator` discipline), never a field or return type; (2) the self-type
problem — `Extend`/`Duplicate` return "your own kind," which C# interfaces cannot
express cleanly, so any `ITreeWalker` would carry `Value` + step verbs only: a
navigation interface, not the comonad's; (3) `DagWalker` is NOT its citizen — dag steps
are edge-atomic (in-edge groups), and the collapse law makes the dag walker a SIBLING
contract, not an implementer. The restraint rule therefore holds: today `TreeWalker`
has one implementation and no polymorphic consumer; the interface is minted when the
second citizen (the rich navigation stance from the spectrum, or an address-walker)
arrives, with the constraint-only design recorded here so it is ready.

### 8d. Async member shapes (PROPOSED — OPEN-10)

```csharp
public readonly struct AsyncTreeWalker<TValue, THandle>
{
  public THandle Focus { get; }                                    // unchanged: data, not I/O
  ValueTask<TValue> GetValueAsync();                               // extract -- a property cannot await
  ValueTask<AsyncTreeWalkerResult<TValue, THandle>> MoveToParentAsync();
  ValueTask<AsyncTreeWalkerResult<TValue, THandle>> MoveToChildAsync(int childIndex);
  AsyncTreeWalker<TResult, THandle> Extend<TResult>(...);          // observer arrow: OPEN-10
  IAsyncWalkableTreenumerable<TValue, THandle> Subtree();          // view construction: no I/O
}
```

- **OPEN-10, the observer arrow**: does async `Extend` take sync observers only
  (`Func<walker, TResult>` — transcribes cleanly; observers that probe must block, which
  async observers exist to avoid) or async observers
  (`Func<walker, ValueTask<TResult>>` — honest, but the sync transcription must
  collapse the arrow, a new transcriber capability)? Lean: async observers, since an
  observer's whole purpose is probing and probes are async in that color; the
  transcriber grows the arrow-collapse rule once, the way it grew the Renames table.
- Steps and extract are `ValueTask` (probes are pulls; completed captures answer
  synchronously); no CancellationToken (edges-only); `Duplicate` stays
  `Extend(walker => walker)`; the no-unfocused invariant and result-struct family
  transfer verbatim.

### Execution order (once OPEN-7..10 are ruled)

1. 8a — contract family to the Core pair; manifest re-points; pure move, suite green.
2. 8b — walker core authored async in `Copse.Async`, sync twins generated into `Copse`;
   the hand-written `Copse.Linq/Walker` core files retire; lens pair stays put. The law
   suites re-run unchanged (they speak the public surface).
3. 8d — the async walker surface ships with its own law suite (the async twin of
   `TreeWalkerLawTests`, over async providers).
4. Docs: survey §4 notes the walker tier is single-sourced; CLAUDE.md's project map
   gains the walker's placement.

## 9. The placement pass (RATIFIED and EXECUTED 2026-08-14 — the architecture review)

Jason's solution-architecture review re-ruled §8's placement half and landed on the
design's cleanest statement: **carriers in Core, algebras in Linq, for both tenants** —
the comonad placed exactly as the monad always was.

- **`TreeWalker` + `TreeWalkerResult` → the Core pair** (Jason: "we don't need the
  interface [OPEN-9's deferred `ITreeWalker`] — maybe we just put the TreeWalker there").
  Core now holds both tenants' carriers: `ITreenumerable` (monad) and `TreeWalker`
  (comonad), beside the contracts they consume. The comonad members that could not come
  along — `Extend`, `Duplicate`, `Subtree`, whose results are walkables and walkables
  STREAM, which needs the Walk adapter above Core — became Linq extensions: the same
  split `Select`/`Where` have always had from `ITreenumerable`. The constructor went
  PUBLIC (construction IS the trust-based door; `WalkerAt` stays as discoverable sugar)
  and the terrain field became the public `Walkable` property (a vantage is focus ×
  terrain; the operator tier builds from the pair).
- **The rest of the walker tier → `Copse.Linq(.Async)`** (Jason's charter argument:
  operator-dedicated machinery lives WITH its operators, the `Where`-treenumerator
  precedent; and nothing in the base project depended on the tier). New layout:
  `Copse.Linq/WalkableTreenumerable/` = the extension partials as
  `static partial class WalkableTreenumerable` (Extend, Subtrees, Get*/Find*, doors,
  lenses, SpanningSubtree, and the carrier's extracted comonad members);
  `Copse.Linq/Treenumerables/Walkable/` = the machinery (`ExtendWalkable`,
  `SubtreeWalkable`, `WalkerWalk`, `PruneAfterWalkable`). The singular/plural folder
  convention (extensions/types) now spans both receiver families.
- **`HandleResult` stays Linq** — Jason's ruling completed the results rule: *results
  live beside the operation that speaks them* (`ChildResult`/`ParentResult` contract-
  spoken → Core; `TreeWalkerResult` step-spoken → Core with its carrier; `HandleResult`
  `FindHandle`-spoken → Linq).
- §8b's location half is thereby REVISED (base-package placement retracted; the
  fundamentality instinct belonged to the contract, which 8a honored, and now to the
  carrier, which Core holds); its single-sourcing half stands unchanged — every file
  above remains authored async, generated sync.

Deferred from the same review, both since LANDED (2026-08-14): `TreenumerableFactory`
folded into `Tree` down in `Copse` (the carrier-intro is operator infrastructure with no
Linq dependency; "one word one meaning"), and the vague `Extensions/` folders renamed to
their receivers' names (`TreenumeratorExtensions/`, `ListExtensions/`).

## 10. The naming grammar, the search law, and the one extension class (RULED + EXECUTED 2026-08-14)

The naming-conventions review (Jason-driven, same day as §9) produced three rulings; the
canonical grammar statement lives in OPERATOR_SURFACE_MAP.md ("Naming grammar" section) —
this is the walker-arc record of what changed and why.

- **The search law — `Find` retired, both arities.** `FindHandles`/`FindHandle` (§7's
  acquisition sugar) and `HandleResult` deleted: both were `GetHandlesWithValues` plus
  consumer LINQ — the "do our thing, then call LINQ" shape the surface refuses. The law:
  *searches are not surface; the library exposes total enumerations and probes, and LINQ
  owns the rest.* An extension earns surface only if it needs information per-element LINQ
  cannot reach (child-lookahead, depth, traversal semantics, receiver-recovery).
  `GetHandlesWithValues` stays as the one earned exception (receiver-recovery: a value
  predicate mid-chain cannot reach the probe without naming the receiver twice). A
  search's honest miss is the **empty sequence** — result structs belong to operations
  whose answer-arity the structure fixes (steps, LCA, the spanning root); a missed search
  flows typed through result-typed consumers (`SpanningSubtree` of an empty search). The
  sentinel trap outlives the singular as a pinned WARNING (never `FirstOrDefault` over
  ordinal handles — the miss masquerades as the root). The results rule loses a citizen
  with its speaker: `HandleResult` was `FindHandle`-spoken, and died with it.
- **Overload-unity, upgraded to structure.** First ruled as: same-name cross-tier
  overloads (the lens family) share one static class, so betterness picks the specific
  receiver from ONE candidate set — same-namespace placement resolves identically today,
  but a class move would SILENTLY fall back to the streaming overload (it compiles; the
  lens's pair-citizenship just vanishes). Then Jason upgraded the rule from a per-method
  judgment to a structural invariant: **one extension class per color** —
  `static partial class Treenumerable` / `AsyncTreenumerable` — for BOTH receiver tiers.
  `WalkableTreenumerable`/`AsyncWalkableTreenumerable` (the classes) are gone; the class
  name follows the `Enumerable`/`Queryable` idiom (the class is not named `*Extensions`;
  the folder is). Resolution is now closed under refactoring: every extension, every
  tier, one candidate set, forever.
- **The Try law (Jason's, superseding this section's first grammar draft — RULED + SWEPT
  same day).** My "`Get` returns data, never a citizen" law was falsified by the oldest
  contract in the library: `GetDepthFirstTreenumerator` mints an object and wears `Get`,
  the `GetEnumerator` sense. The grammar that survives: **`Get` = acquisition or read;
  `TryGet` = acquisition whose miss is typed** (the result struct is the async spelling of
  the BCL try-pattern — `out` cannot cross an `await`); bare `Get` that can fail THROWS
  (violation channel) — the prefix pair encodes the two-channel doctrine. Swept: contract
  probes `GetParent`/`GetChildAt`/`GetRootAt` → `TryGetParent`/`TryGetChildAt`/
  `TryGetRootAt` (Core, both colors, all implementers); `WalkerAt` → `GetTreeWalkerAt`
  (walker acquisition named like treenumerator acquisition; bare Get — trust door, no
  probe fires, no typed miss); `GetRootWalker` → `TryGetTreeWalkerAtRootIndex` (the name
  spells RootIndex because when `THandle` is `int` only names keep root-ordinal and handle
  apart); `GetValue` stays bare (forged handle = violation, throws). `Move*` stays
  unmarked on `IEnumerator.MoveNext`'s precedent (partial movement, unmarked since the
  BCL's beginning) — Jason's pragmatic ruling; a full Try-everything-that-fails-gracefully
  pass was considered and deferred. The one-op-one-file pass also split the doors file and
  gave `GetHandlesWithValues` its own file; the walker-receiver extensions got their own
  class (`TreeWalker`/`AsyncTreeWalker`) under the refined one-class rule: **one static
  class per receiver-CONVERTIBILITY family** (the walker converts to neither interface, so
  cross-resolution is impossible and the class wears the receiver's name).
- **The folder shape.** `Copse.Linq(.Async)/TreenumerableExtensions/` holds receiver-tier
  subfolders `Treenumerable/` and `WalkableTreenumerable/` (tier readability stays in the
  tree; the class unifies). This supersedes §9's "singular/plural folder convention"
  clause — extension folders now all wear the explicit `*Extensions` suffix
  (`TreenumerableExtensions/`, `TreenumeratorExtensions/`, `EnumerableExtensions/`,
  `ListExtensions/`), which also retires the one-letter `Treenumerable/` vs
  `Treenumerables/` near-collision. Namespaces held (`Copse.Linq`); zero call-site churn.

## 11. The receiver-smart collapse — the tier's load-bearing proof (EXPERIMENT + COLLAPSE, 2026-08-14)

Jason's challenge ("prove TreeWalker provides real value: reimplement a buffering
operator over it, as readable and as fast") ran as a three-part experiment
(`LeaffixScan2`/`3`, `Invert2` — all since retired) and collapsed into the shipped
operators the same day. The findings, in the order his skepticism forced them:

- **The incumbents were secretly adjacency algorithms already.** LeaffixScan's engine is a
  raw capture + child-index build + reverse-ordinal fold on private arrays; Invert even
  had a dedicated buffer overload and a span-arithmetic emit — but both re-captured a
  buffer receiver from its own visit stream, because pre-seam there was no path from a
  buffer to the arrays it stands on. The walker tier is that private engine's vocabulary
  made public, not a new capability.
- **The seams** (all internal; zero changes to `TreeWalker`, the walkable contract, or the
  store types): `PreorderAdjacencyIndex.Store`, the buffer's `TryGetPreorderStoreAsync`
  (tuple-shaped — `out` cannot cross an `await`; the async spelling of the try-pattern),
  `LazyPreorderStore.EnsureBuiltStore`, and **probes-at-birth** (Materialize's
  declared-layout buffers share ONE lazy store between the stream half and the adjacency
  index, so the settle's double capture survives only on the undecided organic path).
- **The shipped shape** (this section's deliverable): `LeaffixScan` (seed flavor) and
  `Invert` sniff their receiver — the `Materialize` / LINQ-`Count` idiom. A
  preorder-affording capture folds in place: the concrete buffer hands its raw store to a
  span algorithm (no probes, no child-index build, no positions build); a foreign
  walkable (a memo) folds through the public probes, completing exactly once with no
  second skeleton; level-order captures and true streams take the engine. Guard:
  `AffordsInPlaceFold` — a concrete buffer settles itself preorder from any
  non-level-order state; a foreign walkable is fold-safe only when already preorder.
  In-place results carry probes at birth. Measured: 3.0–4.6x on buffer receivers, stream
  parity (the engines are unchanged); `ReceiverSmartOperatorTests` pins every receiver
  shape against the engine oracle.
- **The verdict on the tier**, precisely: the *walkable receiver* is where the win lives
  (don't re-buffer what's already buffered); the *store door* is the wholesale fast lane
  (internal for now — Jason's ruling: the first real external consumer names the public
  signature; his standing signal — he might collapse the contract into the store surface
  then — is answered in the memory record: the memo, infinity permission, the lens
  family, and THandle genericity all stand on the probe contract); the *walker struct* is
  the readable public spelling (the probe folds read as stances and steps) and the
  consumer-facing cursor. Remaining rollout candidates: OrderChildrenBy (the sort-each-group
  emit is the mirror's generalization), the dispatch/bypass flavors (need a positions
  pass), and the missing node-count affordance on completed captures.

## 12. The store policy and the skeleton's admission (RULED 2026-08-14)

The foundation restatement lives in CATEGORY_THEORY_SURVEY.md §10; this is the contract-side
record of its rulings:

- **Contracts are public; encodings are family-internal.** The store SPI interfaces
  (`IPreorderStore` et al.) and the flat wrappers stay public — the provider story is
  shipped fact (SimpleSerializer implements four stores across the assembly line), and a
  badly-implemented provider contract is the irreducible risk of ANY SPI, managed by
  validity predicates and batteries, not accessibility. The CONCRETE array stores and
  their unvalidated ctors are the family's encodings: they go internal at the pre-beta
  signature pass, with family IVT (`Copse` → `Copse.Linq`, plus the async pair) — Jason's
  ruling: IVT is normally tests-only, but the lockstep package family makes it skew-safe
  and this is the case for it. Sleeper payoff: the long migration's width change stops
  being a public break on the store ctors.
- **Wholesale access hands capability, never memory**: the read-only store struct
  (`Count`/`GetValue`/`GetSubtreeSize`) is the exposure shape — the arrays never escape.
  The seams already conform (`TryGetPreorderStoreAsync` hands the struct); a future
  SIMD-shaped consumer escalates to `ReadOnlySpan` accessors, which keep the no-mutation
  guarantee at full speed.
- **The skeleton is a lawful carrier**: `WalkerLawProviders` admits a skeleton-direct
  terrain (raw store rewrapped with nothing else, `PreorderSkeletonValidity`-checked on
  entry) to every walker/comonad law suite, so the span schedules are certified as
  extends. The validity predicate is code in TestUtils, with its own negative pins — it is
  the definition of what a skeleton provider owes.
- **The DAG dual is sketched** (recorded in the dag workstream's memory for the branch's
  resumption): spans become CSR + the in-edge transpose; the topo scans are the Sourcefix/
  Sinkfix schedules; phase 2b's materialized-result ruling makes receiver-smartness the
  common case there; DAG Transpose is an O(1) index swap on a skeleton carrying both
  directions; schedules are CLASS-RESTRICTED on DAGs (overlapping closures — the
  scan/dispatch tier keeps O(V+E), the general closure observer is the path-semantics
  canary); the validity predicate grows a transpose-consistency leg.
