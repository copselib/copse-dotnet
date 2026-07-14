# Store Family Review (flat stores, streams, and their builders)

> **Status: REVIEW 2026-07-13; decisions 1–2 of the decision list DECIDED 2026-07-13**
> (taxonomy adopted with renames — executed same day; factory placement = option 3, the
> `Copse`/`Copse.Async` layer — factories not yet built, pending the operator-flag #4 spike).
> The deep-dive behind discussion-queue item 3 of
> [LAZINESS_AND_BUFFERING_POLICY.md](LAZINESS_AND_BUFFERING_POLICY.md); raw inventory in
> [OPERATOR_SURFACE_MAP.md](OPERATOR_SURFACE_MAP.md) §2–4.
>
> **The naming rule as adopted:** *traversal things carry dimension names
> (`DepthFirst`/`BreadthFirst`); storage things carry encoding names
> (`Preorder`/`LevelOrder`).* Hence `MemoizeDepthFirstSourceTreenumerable` (about the
> source's traversal affordance) kept its name, while the memo's storage types were renamed:
> `MemoizeDepthFirstBuffer` → `MemoizePreorderBuffer`, `MemoizeBreadthFirstBuffer` →
> `MemoizeLevelOrderBuffer`, and their SPI adapter structs likewise (`MemoizePreorderStore`,
> `MemoizeLevelOrderStore`). Every store now carries a one-line taxonomy header stating its
> grid position.

## The question that prompted this: why is there no async `LevelOrderArrayStore`?

Because **the async layer reuses the sync array stores internally, behind the grow seam.**
`AsyncLazyBuiltPreorderStore` awaits its one-shot build into a plain `PreorderArrayStore`,
then answers every subsequent `Ensure*Async` with a completed `ValueTask` over it (its own
comment: "every call after that answers from the completed PreorderArrayStore"). The async
SPI's design supports this exactly — `Ensure*` await, `Get*` stay synchronous pure reads.

So an `AsyncPreorderArrayStore`/`AsyncLevelOrderArrayStore` would be nothing but
completed-`ValueTask` shims around the sync structs, and no product path needs one: every
async route to a completed store goes through a lazy-built (or stream-fed / memo) wrapper
that already holds the sync store. The one consumer that hands a *pre-built* store directly
to an async decoder — benchmarks — hand-rolled a private `CompletedAsyncPreorderStore`
(preorder only).

**Verdict:** not missing by accident, but the fact is written down nowhere, the latent
adapter got duplicated privately in benchmarks, and it intersects the known "no sync→async
adapter exists" package gap. See proposal C.

## Findings

**F1 — The family is smeared across four projects with an asymmetric async story.**
Sync SPIs + completed array stores: `Copse.Primitives/FlatStores` (namespace `Copse`).
Async SPIs: inside `Copse.Async` (namespace `Copse.Async`) — there is no
`Copse.Primitives.Async`. Growing stores: internal to `Copse.Linq(.Async)/Stores`
*(moved out of `…/Treenumerators` 2026-07-13 — stores are not treenumerators; namespace is
now `Copse.Linq(.Async).Stores`)*.
Serializer stores: `Copse.SimpleSerializer`. Decoders/wrappers: `Copse(.Async)`.
*(RESOLVED 2026-07-13 — the rule exists and is now stated in CLAUDE.md as **the color
rule**: the sync and async families share no contracts; Vocabulary/Primitives/Traversal are
the color-neutral substrate under both. Tree data and vocabulary are neutral; traversal
contracts are per-color. Under that rule every placement above is not just locally
defensible but forced: neutral stores below both colors, async SPIs in the async color
because `ValueTask` polyfills are color baggage, growing stores beside the per-color
operators that feed them. Primitives' package description was updated to state its actual
promise — it speaks tree vocabulary freely and references neither color's contracts.)*

**F2 — Constraint that settles factory placement: `Copse.Primitives` references only
`Copse.Vocabulary`.** It cannot see `ITreenumerator` (Copse.Core) or `IAsyncTreenumerator`
(Copse.Core.Async). Therefore a `CaptureFrom(treenumerator)` factory **cannot live on the
array stores** where they are today. The codegen-paired layer that sees both treenumerators
and stores is `Copse` (sync, ← Core + Primitives) / `Copse.Async` (← Core.Async +
Primitives) — the same pair that already hosts the decoders.

**F3 — Two canonical capture loops are re-implemented at ~10 sites** (shapes A and B, map
§3): character-identical between `Invert.BuildMirror` and
`OrderChildrenBy.BuildOrderedChildren`, with variants in LeaffixScan (close-hook),
LeaffixAggregate (no-store, per-root reuse), the memo buffers (resumable `PullOne`/`Consume`
forms), serializer (text-driven), benchmarks, and tests.

**F4 — Duals are inconsistent.** `LazyBuiltLevelOrderStore`: generated + tested, **zero
product consumers** (orphan). `StreamFedPreorderStore`: doesn't exist (nothing needs it).
Completed-store async adapters: benchmark-private, preorder-only. The preorder↔level-order
transpose: benchmarks only (deliberately measured out of product). Per the dual-symmetry
preference, orphans are acceptable — but each should be a *decision*, and the
`LazyBuiltLevelOrderStore` orphan may be about to get its consumer (see D4).

**F5 — Naming crosses its axes.** The family's own convention is encoding names
(`Preorder`/`LevelOrder`) for layout and dimension names (`DepthFirst`/`BreadthFirst`) for
traversal — held everywhere except the memo cluster, which named its *preorder* capture
`MemoizeDepthFirstBuffer` (by feed dimension). *(RESOLVED 2026-07-13: renamed to
`MemoizePreorderBuffer`/`MemoizeLevelOrderBuffer` + `MemoizePreorderStore`/
`MemoizeLevelOrderStore` per the adopted naming rule above.)* Second seam: the
unboxing-adapter idiom has two conventions — standalone `Memoize*Store` structs vs the
serializer's nested `*StringStore<T>.Handle` structs (still open, hygiene item E). Third:
tests re-implement the public array stores under word-order-swapped names
(`ArrayPreorderStore`) (still open, hygiene item E).

**F6 — Shape-A's arrival-selector varies without a stated reason**: operator builds filter
`Mode == SchedulingNode`; memo/tests use `VisitCount == 1`. Equivalent in DFT (documented
only inside `MemoizeDepthFirstBuffer`).

## Proposed target state

**A. Name the taxonomy, then let names follow it.** Every member is (encoding: preorder |
level-order) × (completion: completed | growing) × (feed: none | one-shot build | stream |
visit-stream, resumable). Concretes today, sorted by that grid: completed+none =
`*ArrayStore`, `*StringStore`; growing+build = `LazyBuilt*Store`; growing+stream =
~~`StreamFedLevelOrderStore`~~ (deleted 2026-07-13; the stream-drain capability lives on as
the one-shot `LevelOrderCapture.CaptureFrom(ILevelOrderStream)`); growing+visit-stream =
`Memoize*Buffer` (+ SPI adapter structs).
The grid makes gaps and orphans visible and is the doc header each store should carry.

**B. Capture factories live in `Copse`/`Copse.Async` as codegen-paired statics** (per F2 —
they *cannot* go on the stores in Primitives). Shape: a `PreorderCapture` /
`LevelOrderCapture` static class (names bikesheddable) exposing
`CaptureFrom(source)` → `PreorderArrayStore`, plus the two hooks the variants need: a
per-node selector (OrderChildrenBy's keys) and a close-hook (LeaffixScan's accumulator).
`LazyBuiltPreorderStore(() => PreorderCapture.From(...))` becomes the one written-once
composition every capture op rides. Out of scope for the factory: LeaffixAggregate's
per-root reusable-buffer form (different lifetime), the memo's resumable forms (different
protocol), serializer text parsing (different feed). Those keep their loops; the factory
absorbs sites 1, 2, 9, 11 of map §3 and any future capture op.

**C. Name the latent completed-store async adapter once, publicly** — `AsAsync()` on the
completed stores or `CompletedAsync*Store` wrappers in `Copse.Async` — replacing the
benchmark-private copy and closing the preorder/level-order asymmetry. This is also the
bottom tier of the known missing sync→async bridge, so build it as that story's foundation,
not a one-off.

**D. Resolve the duals deliberately (cross-links to the pending operator flags):**
- **D4a. `LazyBuiltLevelOrderStore` orphan — RESOLVED 2026-07-13 exactly as predicted**:
  flag #5 went eager-on-first-pull; the orphan is now Invert-F's BFT-first deferral seam,
  and `StreamFedLevelOrderStore` was deleted — its incremental drain preserved one-shot as
  the stream-shaped `LevelOrderCapture.CaptureFrom(ILevelOrderStream)`.
- **D4b. `StreamFedPreorderStore`**: don't build it; note it as the named gap.
- **D4c. Transpose stays benchmark-only** (the measured decision stands), but if factories
  land (B), it becomes a natural named factory if ever needed.

**E. Small hygiene, low priority:** pick one adapter-idiom convention (nested `.Handle`
reads better — the adapter is meaningless without its owner; renaming `Memoize*Store` is
cheap since they're internal); ~~document (not rename) the memo cluster's
feed-dimension naming~~ *(overtaken: Jason chose to rename — done 2026-07-13)*; switch
`FlatFamilyConformanceTests` to the public array stores unless their independence was
deliberate (unlike `EngineTree`, no oracle argument applies — the tests exercise the
*decoders*, and feeding them the public stores tests more product code, not less);
pick one shape-A arrival-selector for the factory (B) and document the equivalence once.

## Decisions needed

1. ~~Adopt the taxonomy + factory placement (A, B)?~~ **DECIDED and BUILT 2026-07-13.**
   Taxonomy with renames executed; `PreorderCapture` (plain + side-channel) and
   `LevelOrderCapture` (plain) live in `Copse/Stores` (async sources in
   `Copse.Async/Stores`, namespace `Copse(.Async).Stores`). As-built notes: **public**, not
   internal+IVT — no product-to-product IVT precedent exists and the array stores they
   return are already public; Invert's build is the first consumer (its zero-key LIFO emit
   stays specialized — benchmark rows); **LeaffixScan stays bespoke** — its close-hook needs
   `ChildAccumulations`, a Copse.Linq type this layer cannot reference, exactly the boundary
   proposal B predicted; OrderChildrenBy adopts the side-channel form at its rebase;
   shape-A's selector standardized on `Mode == SchedulingNode` with the VisitCount-1
   equivalence documented in the factory header.
2. Build the public completed-store async adapter now (C), or defer until the sync→async
   bridge story is taken up?
3. ~~Which way does operator flag #5 (Invert-F BFT-first arm) go?~~ **DECIDED 2026-07-13:
   eager-on-first-pull** (see D4a — orphan adopted, `StreamFedLevelOrderStore` deleted,
   dispose-time cost surprise gone; benchmark A/B on the Invert family gated the change).
4. Hygiene items (E): fold into the cohesion pass or leave as notes?
