# Laziness & Buffering Policy (foundational decision record)

> **Status: DECIDED 2026-07-13.** The umbrella policy over the streaming/buffering questions
> that each hard operator (Invert, LeaffixScan, OrderChildrenBy, StructuralMerge…) had been
> re-litigating individually. Builds on the [disclosure rule](OPERATOR_DIMENSION_AUDIT.md)
> (2026-07-11) and the emission-vs-arrival gap principle
> ([TRAVERSAL_DIMENSION_SPLIT.md](TRAVERSAL_DIMENSION_SPLIT.md)). Supersedes the old blanket
> "operations compose without materializing intermediate trees" claim wherever it appears
> (CLAUDE.md, README) — see "The promise" for the replacement wording.
>
> Companion inventory: [OPERATOR_SURFACE_MAP.md](OPERATOR_SURFACE_MAP.md) — what every
> operator returns and what it buffers, so this policy can be checked against reality.

## The realization behind it

The library had been optimized for the far edge case — billion-plus-node, lazily generated
trees — at the cost of the common case. "Keep everything lazy" had grown into "wrap things in
a lazy buffer wherever possible," which pushes a three-way burden onto every consumer: know
what buffers, know when it doesn't, know which results are `IDisposable` captures that must be
held and disposed. That is too much overhead for the common case.

The correction is **not** to abandon laziness — it is to make the common case brain-dead
simple while keeping great performance for the lazily-evaluated extreme cases *opt-in*.

## The promise

> **Operations compose without materialization when possible. We document when
> materialization does or might happen.**

And the burden clause that gives it teeth: **the library takes on the streaming work, even
when it is hard for us as developers, rather than asking consumers to take on the buffering
burden.** Capture is justified only by *semantics* (the emission-vs-arrival gap spans the
whole tree), never by implementation convenience.

## The user-facing model (three sentences)

- Returns `ITreenumerable` (or a narrow dimension interface) → **it streams**; compose
  freely, nothing is held.
- Returns `ITreenumerableBuffer` → **it captured the tree**; it is just data — no disposal,
  re-traverse at will.
- **`Memoize`** → the one power tool: a live, lazily-growing, *disposable* capture
  (`ILazyTreenumerableBuffer`) for expensive or huge sources — you asked for it, you own it.

## The rules

1. **`Memoize` is the only public operator that returns the disposable
   `ILazyTreenumerableBuffer`.** Every other buffering operator returns the non-disposable
   `ITreenumerableBuffer`. No new disposable returns, ever, without revisiting this record.

2. **Classification is by the emission-vs-arrival gap, per dimension** (the
   TRAVERSAL_DIMENSION_SPLIT principle), and it is a decision procedure, not a debate:
   - Gap bounded by O(depth) / O(width) → the operator **must stream** in that dimension.
     Wrapper complexity is our burden; a capture shortcut here would break the promise.
   - Gap = O(subtree) / O(tree) → **capture op** under the disclosure rule: buffer return
     type in the signature, materialization stated in the XML docs.
   - Return types that cannot carry the disclosure (enumerable/scalar): **amended
     2026-07-13** — such operators may gain cross-dimension overloads whose internal capture
     is *documented*: stated in the XML docs and registered in the surface map. The promise's
     contract is documentation; the buffer return type is the stronger form, used when
     available. The cost-class change must be part of the docs (first application:
     LeaffixAggregate's breadth-first entry — peak goes from largest-root-subtree to whole
     forest, and per-root laziness is lost, because breadth-first arrival interleaves every
     tree). The explicit hoist (`Materialize().LeaffixAggregate(...)`) remains available and
     is what the equivalence tests pin the documented entry to.

3. **Capture ops build deferred-once (`Tree.Lazy`-pinned), not eagerly and not
   per-traversal.** Construction is pinned to the first treenumerator acquisition, the build
   runs once, and every replay rides the capture. This is invisible to the consumer (no
   disposal, no re-enumeration surprises) and keeps composition free until first consumption.

4. **The documentation duty is part of the operator, not an afterthought.** Every capture
   op's XML doc states what is materialized and when (the existing Invert/LeaffixScan/
   OrderChildrenBy docs are the template). Capture ops are finite-tree-only; the type
   discloses the cost, the docs disclose the hang.

5. **The extreme-case story stays intact**: the streaming spine (Select, Where, prune/take,
   the aggregates and scans, the set operations) remains genuinely lazy with bounded state,
   the dimension split keeps impossible asks compile-time errors, and `Memoize`/`Materialize`
   remain the explicit, caller-owned escalations.

## What this re-prices

- **"Ship the capture shape, earn the streaming shape" is NOT the default sequencing.** Rule
  2's burden clause means a bounded-gap operator gets its streaming implementation even when
  that is the hard road (the Where BFT prefix-carry and the lockstep merge are the
  precedents). Capture-first shipping is reserved for whole-tree-gap operators, where capture
  is the *final* shape, with specialized builds (Invert's zero-allocation LIFO emit) as
  benchmark-gated refinements.
- **The deferred machinery is demoted, not discarded** (pending the queue below): the
  capability lattice ([TREE_CAPABILITY_INTERFACES.md](TREE_CAPABILITY_INTERFACES.md)),
  child-enumerator-level transforms, and the parked
  [preorder window](PREORDER_BUFFER_IDEA.md) become optional cheaper-when-rich follow-ups
  with the capture ops as their permanent correct fallback — no longer foundational
  prerequisites for shipping operators.

## Discussion queue (noted 2026-07-13, deliberately not decided)

1. **StructuralMerge: is the lazy solution worth the readability?** The lockstep co-traversal
   feels like the right landing (bounded O(depth)/O(width), no capture), but the
   implementation cost was high and the readability cost is permanent. Revisit with the
   [final assessment](STRUCTURAL_MERGE_PATH_ASSESSMENT.md) in hand once the merge work
   settles: if the answer is ever "not worth it," rule 2's burden clause needs a
   complexity-bounded escape hatch, written here.
2. **How far to demote the deferred machinery.** The capability lattice / child-level
   transforms (e.g. OrderChildrenBy as an O(sibling-group), infinite-safe child-enumerator
   wrapper) and the preorder window: keep parked until a consumer demands them, or schedule
   one deliberately as the perf tier for a shipped capture op?
3. ~~**`Consume`.**~~ RESOLVED (2026-07-14→15, the full arc): probes added, then REVERTED once the real constituency spoke — Consume is the mechanical walk (unit tests, benchmarks), Complete() finishes a lazy capture, Materialize delivers a settled buffer. One word, one meaning.
4. **THE NAMING SESSION** (queued 2026-07-15; scope grew across the chimera discussion). ~~(a) Memoize*Buffer -> Memoize*Store~~ EXECUTED 2026-07-15 (54d9dbf). Remaining scope -- likely wants its own grid over "WHEN does work happen" and "WHAT is deferred": (b) the feed-column names do not telegraph their granularity -- Jason's formulation: LazyBuilt = DEFERRED construction of the entire store (one-shot build, all-or-nothing); Memoize = LAZY construction of the store (resumable visit-stream feed, pays per node). "LazyBuilt" arguably describes the memoize one better than the one it names; candidates worth weighing when revisited: Deferred*Store / Incremental*Store, or grid-derived names. (c) Tree.Defer / Tree.Lazy themselves (Jason, 2026-07-15) -- VERIFIED against the archived Rx docs (an AI search summary claiming Rx Defer "reuses the sequence across subscriptions" was a hallucination; the real doc: the factory is invoked "whenever a new observer subscribes", and its example exists to show re-subscription yielding FRESH data): Tree.Defer matches Rx Defer's call-by-name exactly, Tree.Lazy matches System.Lazy<T>'s pinned-once exactly -- these two are the best-precedented names on the surface and likely SURVIVE the session. The genuine defendant: "lazy" spread across THREE granularities -- Tree.Lazy (call-by-need, pinned once), LazyBuilt*Store (deferred one-shot build), ILazyTreenumerableBuffer (incremental capture) -- while Defer (call-by-name, per acquisition) borrows Rx vocabulary. A session with a deferral-semantics grid (per-acquisition / pinned-once / incremental x what-is-deferred: tree construction / store build / capture growth) should settle the whole "when does work happen" vocabulary in one sitting: Defer, Lazy, LazyBuilt, Memoize, deferred-once capture ops, Complete, Materialize.

5. **Store/wrapper cohesion pass.** The preorder/level-order stores, streams, and their
   treenumerable wrappers accreted; the operator-side private builders duplicate machinery
   that belongs on the stores as constructors/factory methods. Concrete first finding:
   `Treenumerable.Invert.g.cs::BuildMirror` and
   `Treenumerable.OrderChildrenBy.g.cs::BuildOrderedChildren` share (a) an identical
   capture loop (depth-first walk → `values[]` + `subtreeSizes[]`, OrderChildrenBy adding a
   parallel `keys[]`) and (b) an identical stack-driven span-hop emission skeleton differing
   only in how each sibling group is ordered (reverse vs. sort). Candidate shape: a
   `PreorderArrayStore.CaptureFrom(source[, selector])` factory plus a sibling-group-reorder
   emission, with `LazyBuiltPreorderStore` composing them. Inventory of all ad-hoc
   construction sites: see [OPERATOR_SURFACE_MAP.md](OPERATOR_SURFACE_MAP.md). Remember:
   edits go in the async sources (`Copse.Async`), the sync `.g.cs` is generated.
