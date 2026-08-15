# Design: Brood-Boundary Migration into the Core BFT Engine

**Status: assessed, NOT pursued.** The full migration is **NO-GO**; a small, scan-gated "lesser win" is available (see Decision). Kept as a documented reference so the migration is not re-attempted without the cadence argument below.
**Branch:** `bft-brood-core` (design only — no engine code was changed).
**Date:** 2026-06-25.

---

## TL;DR

The "brood boundary" concept proved its worth *aligning two streams* in the Union BFT, and it is the
right **mental model** and **vocabulary** for BFS-under-skips (`SkipSiblings` = truncate a brood;
`SkipNode` promotion = splice a brood). But migrating it into the core `BreadthFirstTreenumerator`
does **not** simplify the engine's regression-prone owed-parent-visit machinery. The blocker is the
**emission cadence** — one visit per `MoveNext` — which is orthogonal to how a brood is represented:
at a promotion splice, the brood owner's owed re-visit *and* the next spliced child are both due at
one step, so one must be **deferred**, and that deferral (`OwesPromotedParentVisit` /
`PayOwedParentVisitAndDeferChild` / `_HasDeferredScheduledChild`) is exactly what an explicit
boundary cannot remove. The decisive evidence: the Union BFT is *built on* explicit brood /
visit-order indices and **still grew the identical dance**. **Name it, don't migrate it.**

---

## The idea

A **brood** is one parent's run of children in level order; a **brood boundary** marks where one
parent's brood ends and the next begins. The hypothesis was that broods are a *unifying primitive*
for breadth-first traversal, because the two operations that make BFT hard are brood operations:

- `SkipSiblings` = **truncate** the current brood.
- `SkipNode` promotion = **splice** the skipped node's brood into its parent's brood.

If brood boundaries were explicit in the engine's queue, "is the parent's between-children visit
owed here?" might become a *local, structural* check instead of cross-flag bookkeeping.

## Why the core engine was the target

`BreadthFirstTreenumerator` emits the same `(Mode, Node, VisitCount, Position)` visits as the
depth-first engine, in level order. Its complexity is concentrated in one place: **SkipNode
promotion**, where a skipped node's children are promoted into its slot and the parent's interleaved
visit is *swallowed* and must be manufactured/deferred. The machinery:

- `swallowedParentVisit` detection (`BreadthFirstTreenumerator.cs:~160`)
- `OwesPromotedParentVisit` (a per-queue-entry flag on `InternalNodeVisitState`)
- `PayOwedParentVisitAndDeferChild` + `_HasDeferredScheduledChild`
- `_DepthOfLastActedOnNode` and the effective-root test

This is the engine's regression magnet — at least four distinct, subtle bug-fix commits all live here:

| | Commit | Bug |
|---|---|---|
| #1 | `4e695ad` | parent-visit **over**-count under 3 concurrent SkipNode promotions |
| #2 | `3963b8e` | lost grandparent-visit when a promoted subtree ends in skip-subtree |
| #3 | `da24838` | SkipSiblings dropping later roots for an effectively-root promoted node |
| #4 | `92d03d7` | parent-visit **under**-count when a child subtree ends in SkipNode |

The bar for "GO" was: does an explicit brood model make these four classes *structurally impossible*?

## Verdict: NO-GO — the cadence argument

A treenumerator emits **exactly one visit per `MoveNext`**. Trace the canonical splice
`a(b(c,d), e)` with `b:SkipNode` — b's brood `{c,d}` splices into a's brood, giving effective brood
`{c,d,e}`; DFT visits `a` three times:

```
S a               open brood owner = a; a owes a V a between each effective child
V a
S c   (promote)   c enqueued under a
... at S d, two emissions are due at the SAME boundary:
      V a   owner re-visit, owed — b's own between-children visit was swallowed
      S d   next child of the spliced brood
    only one can be emitted this MoveNext  ->  the other MUST be deferred.
```

An explicit brood boundary makes the *detection* of the owed visit trivially local (it would retire
the `swallowedParentVisit` heuristic). **But it does not change the fact that two emissions are due
at one boundary and the cadence permits one.** The defer-and-catch-up (`OwesPromotedParentVisit` ->
`PayOwedParentVisitAndDeferChild` -> `_HasDeferredScheduledChild`) survives essentially unchanged.
The boundary tells you a visit is *owed*; it does not let you emit two things in one step.

### The proof: the dance reappeared on a brood foundation

The Union BFT (`StructuralMergeBreadthFirstTreenumerator`) is built on **visit-order indexing —
broods made explicit** (`_LeftFrontIndex == front.LeftIndex` is literally "is this inner positioned
on the front's brood?"). Despite that foundation it carries the full owed-visit dance:

| Core engine | Union BFT (on the brood foundation) |
|---|---|
| `OwesPromotedParentVisit` | `leftOwes` / `rightOwes` |
| `PayOwedParentVisitAndDeferChild` (the `VisitCount++`) | `RevisitLeadNodeVisitInQueue()` |
| `_HasDeferredScheduledChild` | `_SkipInnerAdvanceThisStep` |

It even needed the *same gating subtlety* as bug #1: the owed re-visit must fire only when the
pending schedule is the owner's own child (a promoted nephew can sit deeper yet belong to the
owner's scheduling phase — "depth can't tell these apart"). Visit-order indices were necessary to
decide *which stream is on the front*, but they sit **beside** the owed-visit handling, not in place
of it.

Three independent re-derivations of the identical detect -> catch-up -> stall structure — core
engine, Union BFT, and Where BFT (`_PendingParentVisit` / `_ConsumeNextInnerParentVisit`) — is the
signature of an **intrinsic consequence of the deferred-visit cadence under promotion**, not an
artifact of the engine lacking brood boundaries.

### Bug-class scorecard

| Class | Made structurally impossible by broods? |
|---|---|
| #1 over-count / wrong owner | **No** — already guarded by moving the flag *per-entry* (`OwesPromotedParentVisit`); a brood-owner model re-expresses the existing guard, adds none |
| #2 lost grandparent-visit (skip-subtree exit) | **No** — cadence-forced deferral; there is a *control-flow* unification opportunity, but it is independent of brood labeling |
| #3 SkipSiblings drops later roots | **No** — this is about the *ancestor stack* (effective-root test), which a brood-id does not encode |
| #4 under-count (child subtree ends in SkipNode) | **No** — symmetric to #2 |

**0 of 4.** The `OwesPromotedParentVisit` / `PayOwedParentVisitAndDeferChild` /
`_HasDeferredScheduledChild` trio survives the migration intact.

### A correction worth keeping

The "broods bound the allocation footprint" idea (a brood-*relative* skip prefix instead of
per-absolute-depth state) **does not apply to the core engine** — it has no per-absolute-depth
state. That footprint bug lived in the *Where wrapper's* `List<int>` (`_PredSkipPrefix`), and was
fixed independently in `ca567e0` (tail-carry). The core engine has nothing to reclaim here.

## The lesser win (GO — optional, low-risk, scan-gated)

Two genuinely positive changes fall out of the analysis:

1. **Brood-relative `OpenChildCount`** on `InternalNodeVisitState` (the owner's scheduled-child
   tally), retiring `_DepthOfLastActedOnNode` and its two opaque depth-watermark tests
   (`BreadthFirstTreenumerator.cs:242, 291`). "The front's brood is exhausted" becomes a local check
   on the owner entry rather than a cross-step absolute-depth watermark — the one spot broods buy
   real clarity, and it untangles the #2/#4 skip-exhaustion payment arms. ~−10..−20 LOC, **1** flag
   eliminated, perf-neutral.
2. **Adopt "brood" as documented vocabulary** — e.g. `OwesPromotedParentVisit` ->
   `OwesBroodOwnerVisit`; document `SkipSiblings` = truncate-brood, `SkipNode` = splice-brood, and
   the owed visit = "the brood owner's between-sub-brood visit." Cost ~0; makes all three
   implementations (engine, Union, Where) describable in one shared vocabulary on the documented
   regression magnet.

### Migration plan (validate-alongside — the house style)

1. Add `OpenChildCount`; maintain it **alongside** `_DepthOfLastActedOnNode` (do not remove yet).
   `Debug.Assert` that the new brood-close check `==` the old depth-watermark check at every decision
   point.
2. Run `Copse.Tests/DepthFirstVsBreadthFirstTests` at the **deep** budget (raise `PerTreeBudget`
   toward ~1.5M; covers the 3-concurrent-skip case), plus `Where2InProcessScan` + `UnionInProcessScan`
   (the wrappers ride this engine).
3. Once the asserts hold across the deep scan, delete `_DepthOfLastActedOnNode` and the two
   depth-watermark expressions; keep the brood-relative check. Apply the vocabulary renames in the
   same series.

Each step is independently revertible; the gate is the exhaustive scan, not new tests.

### What "GO on the full migration" would have required

A representation under which the owed visit need not be deferred — a way to emit the owner re-visit
and the next child without consuming two `MoveNext` steps, or to reorder so they never coincide. The
cadence forbids the first; reordering is the Θ(N) "reorder wall" already documented for BFT in the
[DFT Where extraction spike](DFT_WHERE_EXTRACTION_SPIKE.md). No brood representation clears either bar.

## Decision

**Do not migrate broods into the core engine to dissolve the owed-visit machinery** — the cadence
makes the deferral intrinsic, and the Union BFT is live proof that the dance survives an explicit
brood foundation. **Adopt the brood vocabulary** (high value, ~0 risk) and, optionally, the
`OpenChildCount` clarity refactor (scan-gated). The valuable output of this investigation is the
*understanding* — that this machinery is fundamental to BFS-under-promotion, not accidental — which
tells future work to stop trying to delete it.
