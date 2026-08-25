# The Family Axis (design record)

> **Status: RATIFIED 2026-08-24.** A discussion record, not a work plan: the axis's three
> spellings are deliberate and do not converge; the one candidate unification (the sibling-index
> seat) was built, measured, and rejected the same day — the numbers are §5. The one open tenant
> is the `SiblingIndex` width seam (§6), deferred to the huge-trees work.

## 1. One problem, three spellings

"Hand a consumer this node's children" is answered three ways in the library:

| | `IChildEnumerator<TNode>` | topology `TryGetChildAt` | `DispatchTargets<TSource, TDispatch>` |
|---|---|---|---|
| shape | forward-only pull, one child per call | indexed random-access probe | sibling-complete view + write-once handles |
| implementer → consumer | user (adapter) → engine | provider (buffer, builder, lens) → walker/adapter | operator build → user's survey |
| family materialized? | never required | no (probe answers per index) | yes (CSR slice per parent) |
| cardinality | unknown until exhausted | miss-at-index | `Count` is an O(1) fact |
| miss vocabulary | typed miss (`Option`, stream ends) | typed miss (result struct per probe) | bounds (`ArgumentOutOfRangeException` = caller bug) |
| write channel | none | none | `Dispatch`, exactly-once, conservation-checked |

## 2. The ruling: an affordance ladder, not a redundancy

The three do NOT converge, for the same reason the traversal-dimension split exists: each rung
is the honest maximum its provider can afford, stated as a compile-time fact.

- The **pull** is where laziness lives. A live object graph or a lazy child sequence has no
  O(1) index to give; forcing the probe on it means hidden materialization. (The walker arc's
  standing lesson: never erase the child pull.)
- The **probe** is the walker's terrain — indexed navigation the comonad needs, affordable
  only where handles are addressable.
- The **view** is the dispatch tier's semantics made structural: sibling-complete visibility
  (a fairness split cannot allocate its edges independently) and exactly-once validation
  ("a target was missed" is only detectable when `Count` is known).

Merging any pair makes someone pay a price they didn't owe.

## 3. The fence

`DispatchTargets` seeing exactly one family is a guarantee, not a poverty. The dispatch
readiness clause deliberately leaves the cross-node survey sequence unspecified so parallel
builds stay possible — a pure callback cannot observe the order. Hand a survey anything
navigable (a walker, the tree, an adjacent family) and consumers will read beyond the family;
the unspecified order becomes observed behavior and the parallelism door closes permanently.
**Never widen the survey's view.**

## 4. Escalations between rungs (already built)

The ladder's adapters exist; none needs inventing:

- probe → pull: `TopologyChildEnumerator` — "the topology's indexed probe is the pull",
  written once, shared by every topology source.
- pull → probe: `Materialize` — the explicit O(n) escalation, disclosed by the buffer return
  type (same grammar as the dimension split's `Memoize`/`Materialize`).
- pull → view: the dispatch build's child-index pass (CSR over the preorder encoding),
  internal to the dispatch operators.

Coherence between rungs is pinned: the adjacency conformance batteries rebuild an oracle
model from the visit stream and diff every buffer's probes against it, so probe answers and
pull order can never drift apart.

## 5. The sibling-index seat: tried, measured, kept

The pull's answer carries `NodeAndSiblingIndex` — node plus sibling index — and the index is
always the pull ordinal (every provider echoes the counter it pulls with). The apparent
redundancy invited deletion: answer `Option<TNode>` alone and let the engines count, the way
the flat family's treenumerators (`NextSiblingIndex` on the parent slot) and the dispatch
build ("positions are derived, not stored") already do.

Built and measured 2026-08-24 on `feature/derived-sibling-index` (14d91be1; suite 24,653
green — the change is *correct*): DFT enumerator stack widened to a level struct
(enumerator + counter), BFT frame gained a counter riding it from schedule stack to visit
queue. Local A/B on the Traversal family, allocation columns exact:

| row | alloc main → branch | time |
|---|---|---|
| Bft_Binary | 27.5 MB → 29.6 MB (+7.7%) | +1.6% |
| Dft_DeepChains | 10.5 MB → 12.6 MB (+20%) | +6.2% |
| Dft_Triangle_SkipAll | 26,062 B → 34,231 B (+31%) | −8% |
| Bft_Triangle_SkipAll | 58,903 B → 67,127 B (+14%) | +13.6% |
| every other engine row | +13–14% | noise |

**Why it lost:** the seat is *transient* — it lives in the answer, i.e. in registers — while a
consumer-side counter is *persistent* state: 4 bytes per open level/frame, materialized in
`RefSemiDeque` chunks and copied on every push, pop, and stack→queue move. And providers pay
nothing for the seat: their counters double as their cursors (the value they were about to
produce anyway), so deletion freed no provider state. Worst case is a 4-byte enumerator
whose level slot doubles — exactly the benchmark corpus's engine trees.

**The principle, amended:** positions are *derived* where the deriver already pays for the
counter (the flat family's slots, the dispatch CSR, the path states' root counters); they are
*stored in the answer* where deriving would mint new persistent state (the pull). The seat
stays. Do not re-try the deletion; this section is its record.

## 6. The width seam (open, deferred)

All three rungs speak `int` sibling indices — `NodeAndSiblingIndex`, `NodePosition`, the
`DispatchTargets` indexer, the topology probes — and `SiblingIndex` wraps past
`int.MaxValue`, invisibly to the oracle conformance suites. Trees with tens of billions of
nodes exceed 32-bit indexing on a single sibling axis. This document is where that width
decision gets made once and inherited by every rung, instead of chased through the library;
the decision itself is deferred to the huge-trees branch, which forces the question.
