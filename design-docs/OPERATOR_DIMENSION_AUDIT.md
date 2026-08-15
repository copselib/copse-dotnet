# Operator Dimension Audit

> **Status: REVIEWED & ACTED ON (2026-07-05).** First artifact of the
> [TRAVERSAL_DIMENSION_SPLIT.md](TRAVERSAL_DIMENSION_SPLIT.md) pick-up plan (step 1); reviewed
> with corrections (the GetLeaves/RootfixAggregate reclassification, the deleted `With*`
> operators) and its verdicts drove the narrow-overload set that shipped. Retained as the
> as-built classification and the first draft of the WhatIf cost model. Classifies every public
> operator by kind, dimension consumed, dimension preserved, emission-vs-arrival gap per
> dimension, and hidden escalations. Read against CLAUDE.md's "DFT vs BFT Scheduling and Visiting
> Behavior" (the S/V visit-stream model) and the `Where` write-up.

## Summary

**Operators audited: 52** (47 in `Copse.Linq/Treenumerable`, counting overload families as one
operator; 5 in `Copse.Linq.Experimental`, two of which are stubs and one commented out).
*(Scope note, 2026-08-15: the count is the STREAMING tier as of 2026-07-05 — the walker
tier that shipped after this audit (Extend/Subtrees/the lenses/SpanningSubtree/the doors
and acquisition scans) is inventoried in OPERATOR_SURFACE_MAP.md's walker-tier table, not
here; its operators consume the walker's door, not a traversal dimension, so this audit's
dimension taxonomy does not apply to them.)*

### Verdict counts

| Verdict | Count | Operators |
|---------|-------|-----------|
| **YES** — narrow overloads (single-dimension in, single-dimension out) | **24** | Select, Where, PruneBefore, PruneAfter, TakeNodesUntil, TakeNodesWhile, TakeTrees, SkipTrees, Do, Hide, RootfixScan, Union, Intersection, Subtract, SymmetricDifference, CountNodes, AnyNodes, AllNodes, Consume, GetTraversal, GetLeaves, GetRoots, CountTrees, RootfixAggregate |
| **YES (dimension-fixed consumer)** — one narrow overload only, on the dimension its *semantics* fix | **7** | GetLevels, LevelOrderTraversal (BFT); PreorderTraversal, PostorderTraversal, GetBranches, LeaffixAggregate, GetDepthFirstTraversal/GetBreadthFirstTraversal (DFT/BFT) |
| **RETHINK** — redesign per-dimension rather than mechanically overload | **2** | Invert, LeaffixScan |
| **NO** — stays `ITreenumerable`-only | **16** | Memoize, Materialize, Defer, Using, Empty, TakeLastTrees, SkipLastTrees, ToFormattedLines, ToFormattedString, ToDepthFirstTreeTokenizer, ToBreadthFirstTreeTokenizer, GetTreenumerator, ExpandNode*, Graft*, Collapse* (stub), InorderTraversal* (stub) |
| **DELETED** (2026-07-04 review) | **3** | WithContext, WithLevelIndex, WithParent — no current value, zero references; see surprise 3 |

`*` = Experimental.

### Recommended narrow-overload set (the streaming spine)

The design doc guessed "`Select`, `Where`, the prune/take family, and the aggregates
(`RootfixAggregate`/`LeaffixAggregate`/`CountNodes`/`GetLeaves`) — 8–10 operators." The code
supports a slightly **larger and cleaner** set than that guess, because the whole
`TreenumerableFactory.Create(bft, dft)` family is already dimension-preserving by construction:

**Dimension-preserving (both `IDepthFirst→IDepthFirst` and `IBreadthFirst→IBreadthFirst`):**
Select, Where, PruneBefore, PruneAfter, TakeNodesUntil, TakeNodesWhile, TakeTrees, SkipTrees, Do,
Hide, WithContext, RootfixScan, Union, Intersection, Subtract, SymmetricDifference.

**Dimension-agnostic consumers (take either narrow source):** CountNodes, AnyNodes, AllNodes,
Consume, GetTraversal (already strategy-parameterized); plus GetLeaves, GetRoots, CountTrees,
RootfixAggregate (currently hardcode DFT, but only by implementation accident — see correction
below).

**Dimension-fixed consumers (one overload, on the dimension their *semantics* fix):**
PreorderTraversal, PostorderTraversal, GetBranches, LeaffixAggregate (DFT); GetLevels,
LevelOrderTraversal (BFT).

> **Correction (review, 2026-07-04):** the draft originally classified `GetLeaves` — and with it
> `RootfixAggregate`, `GetRoots`, and `CountTrees` — as DFT-fixed. That conflated "the current
> implementation hardcodes DFT" with "the semantics require DFT." Leaf detection must visit every
> node in either dimension and needs only O(1) state in both: under DFT, a scheduled node is a
> leaf iff the next scheduled node's depth does not increase; under BFT, a node is a leaf iff its
> first visit (VisitCount == 1) is not followed by a child schedule. `GetRoots` drives
> `SkipNodeAndDescendants`, which yields identical output under either dimension. Only emission
> *order* differs for GetLeaves/RootfixAggregate (pre-order vs level-order of the leaves) — the
> same way `GetTraversal` differs, which is fine for a consumer. The audit now distinguishes
> **dimension-fixed by semantics** (PostorderTraversal cannot be a BFT operation) from
> **dimension-fixed by current implementation** (redirectable when the narrow overloads land).

### Surprises vs the design doc's guess

1. **The narrow-overloadable set is defined by the factory shape, not by "streaming-ness."** Every
   operator built on `TreenumerableFactory.Create(() => bftWrapper(source.GetBreadthFirst…), () =>
   dftWrapper(source.GetDepthFirst…))` is *already* dimension-preserving and same-as-requested —
   there is **no operator anywhere that wires one output dimension to the other input dimension**.
   The only cross-dimension dependency in the whole library is `Invert`-over-DFT, and it is
   *masked* by materialization rather than expressed as a cross call. So the split's containment
   worry ("which operators secretly need the other dimension") has exactly one true instance.
2. **`RootfixScan` is dimension-preserving, but `RootfixAggregate` is DFT-fixed *by
   implementation accident*** — it is `RootfixScan(...).GetLeaves()`, and `GetLeaves` currently
   hardcodes `GetDepthFirstTreenumerator`. On review this is not semantic (see correction above):
   leaf detection is O(1)-state in either dimension, so both operators gain BFT support together
   when the narrow overloads land.
3. **`WithLevelIndex` and `WithParent` are not safely re-enumerable.** Each closes over a single
   mutable `List<int>`/`List<NodeContext>` created once at composition time and shared by *every*
   treenumerator the result hands out (both dimensions, every re-enumeration). Two concurrent
   traversals — or DFT then BFT off the same composed tree — corrupt each other. This violates the
   library's lazy-re-enumerable contract and should be fixed (per-treenumerator state) before
   either gets typed. Not a dimension issue per se, but the audit surfaced it.
   **Review outcome (2026-07-04): all three DELETED** — `WithLevelIndex`, `WithParent`, and
   `WithContext` (an identity `Select` promoting `NodeContext` into the value). All were
   experiments toward a general "attach computed context to each node" mechanism that never
   found a satisfying shape; none had current value or references. The general mechanism is the
   real requirement, tracked in the split design discussion (path-threaded context =
   `RootfixScan`; traversal-order-threaded context = a possible per-enumeration-state Select/Do
   primitive, admitted only on real demand).
4. **`LeaffixScan` and `Invert` are the only tree→tree operators that eagerly materialize the whole
   forest** (both end in `new PreorderTree<T>(…)` over `List.ToArray()`), and both consume the
   source **DFT regardless of how the *result* is later traversed** — the result's BFT dimension is
   served by the `PreorderTree` engine off the already-built arrays, not by a BFT pass over the
   source. `Invert` additionally does a *second* O(n) copy into mirrored arrays. These are the two
   "hidden O(n)" operators; there are no others among the tree→tree set.
5. **`TakeLastTrees`/`SkipLastTrees` do a hidden extra full acquisition** (`GetRoots().Count()`)
   before the real traversal — a second, independent DFT pull of the source. Cheap in space
   (`GetRoots` uses `SkipNodeAndDescendants`, so O(#roots)), but it makes them impure-source-hostile
   and non-streaming (they must count all roots before yielding the first).

---

## Full audit table

Legend — **Consumes**: which inner treenumerator the operator pulls when the *result* is traversed
DFT / BFT. "same" = same-as-requested (dimension-preserving passthrough). **Gap**: emission-vs-arrival
buffering class when consumed in that dimension (`—` = not offered / not applicable in that dimension).

| Operator | Kind | Consumes (DFT-req / BFT-req) | Narrow overloads | Gap DFT | Gap BFT | Hidden escalations | Verdict |
|----------|------|------------------------------|------------------|---------|---------|--------------------|---------|
| **Select** | tree→tree | same / same | both | O(1) | O(1) | none | YES |
| **Where** | tree→tree | same / same | both | O(d) (2 path stacks) | O(w) (accepted frontier + prefix) | none | YES |
| **PruneBefore** | tree→tree | same / same | both | O(d) | O(w) | none (shares Where machinery) | YES |
| **PruneAfter** | tree→tree | same / same | both | O(1) | O(1) | none (stateless wrapper) | YES |
| **TakeNodesUntil** | tree→tree | same / same | both | O(1) | O(1) | none | YES |
| **TakeNodesWhile** | tree→tree | same / same | both | O(1) | O(1) | none (inverts predicate) | YES |
| **TakeTrees** | tree→tree | same / same | both | O(1) | O(1) | none (→ TakeNodesUntil) | YES |
| **SkipTrees** | tree→tree | same / same | both | O(d) | O(w) | none (→ PruneBefore) | YES |
| **Do** | tree→tree | same / same | both | O(1) | O(1) | none | YES |
| **Hide** | tree→tree | same / same | both | O(1) | O(1) | none | YES |
| **WithContext** | tree→tree | same / same | — | O(1) | O(1) | none (→ Select) | **DELETED** (see surprise 3) |
| **RootfixScan** | tree→tree | same / same | both | O(d) (path stack) | O(w) (cur+next level) | none | YES |
| **Union** | tree→tree | same / same | both | O(d) (merge stack) | O(w) (merge queue) | none | YES |
| **Intersection** | tree→tree | same / same | both | O(d) | O(w) | none (→ Union+PruneBefore) | YES |
| **Subtract** | tree→tree | same / same | both | O(d) | O(w) | none (→ Union+Where+Select) | YES |
| **SymmetricDifference** | tree→tree | same / same | both | O(d) | O(w) | none (→ Union+Where) | YES |
| **CountNodes** | tree→scalar | strategy param / strategy param | both (either source) | O(1) | O(1) | none | YES |
| **AnyNodes** | tree→scalar | strategy param / strategy param | both | O(1) | O(1) | none | YES |
| **AllNodes** | tree→scalar | strategy param / strategy param | both | O(1) | O(1) | none (→ AnyNodes) | YES |
| **Consume** | tree→void | strategy param / strategy param | both | O(1) | O(1) | buffer overload finishes furthest-along dim | YES |
| **GetTraversal** | tree→enum | strategy param / strategy param | both | O(1) | O(1) | none | YES |
| **GetDepthFirstTraversal** | tree→enum | DFT (hardcoded) / — | DFT only | O(1) | — | none | YES (fixed) |
| **GetBreadthFirstTraversal** | tree→enum | — / BFT (hardcoded) | BFT only | — | O(1) | none | YES (fixed) |
| **PreorderTraversal** | tree→enum | DFT (hardcoded) / — | DFT only | O(1) | — | none | YES (fixed) |
| **PostorderTraversal** | tree→enum | DFT (hardcoded) / — | DFT only | O(d) (path deque; reorders) | — | none | YES (fixed) |
| **LevelOrderTraversal** | tree→enum | — / BFT (hardcoded) | BFT only | — | O(1) | none | YES (fixed) |
| **GetLevels** | tree→enum | — / BFT (hardcoded) | BFT only | — | O(w) (one level buffered) | yields TNode[] per level (inherent) | YES (fixed) |
| **GetRoots** | tree→enum | DFT (SkipNodeAndDescendants) — impl accident | both (either source; identical output) | O(1) | O(1) | none | YES |
| **GetLeaves** | tree→enum | DFT (SkipNode) — impl accident | both (either source; order differs) | O(1) | O(1) (first-visit rule) | none | YES |
| **GetBranches** | tree→enum | DFT (semantics) / — | DFT only | O(d) (current branch) | — | copies branch array per leaf (inherent) | YES (fixed) |
| **CountTrees** | tree→scalar | DFT (→ GetRoots) — impl accident | both (either source) | O(1) | O(1) | none | YES |
| **RootfixAggregate** | tree→enum | DFT (→ GetLeaves) — impl accident | both (either source; order differs) | O(d) | O(w) (rides RootfixScan) | none | YES |
| **LeaffixAggregate** | tree→enum | DFT (hardcoded) / — | DFT only | O(subtree_max) (per-root buffers) | — | List reused per root; peak = largest root subtree | YES (fixed) |
| **Invert** | tree→tree | — / BFT (streaming) or buffer replay | BFT→BFT; buffer→full | — (untypeable; via buffer) | O(w) (level-reversing stream transform) | buffer overload builds mirrored arrays once, lazily (zero-copy view planned) | **REWORKED 2026-07-05** |
| **LeaffixScan** | tree→tree | DFT (materialize) / DFT (materialize) | see RETHINK | **O(n)** (eager) | **O(n)** (eager) | **eager full-forest List → ToArray → PreorderTree** | **RETHINK** |
| **Memoize** | tree→buffer | captures requested dim | captures requested dim | O(n) (capture) | O(n) (capture) | O(n) space is the point (idempotent on a buffer) | NO (upgrade op) |
| **Materialize** | tree→buffer | Memoize + Consume | Memoize + Consume | O(n) | O(n) | O(n) space is the point | NO (upgrade op) |
| **Defer** | factory | factory().DFT / factory().BFT | (grow from demand) | O(1)+ | O(1)+ | none | NO |
| **Using** | factory | resource+DFT / resource+BFT | (grow from demand) | O(1)+ | O(1)+ | none (owns resource via Dispose) | NO |
| **Empty** | factory | — / — | trivially both | O(1) | O(1) | none | NO (not worth surface) |
| **TakeLastTrees** | tree→tree | DFT (count pre-pass + SkipTrees) | — | O(d) | O(w) | **extra full GetRoots() acquisition; must count all roots first (non-streaming)** | NO |
| **SkipLastTrees** | tree→tree | DFT (count pre-pass + TakeTrees) | — | O(1) | O(1) | same as TakeLastTrees | NO |
| **WithLevelIndex** | tree→tree | same / same | — | O(d) | O(d) | shared mutable List across all treenumerators — not re-enumerable | **DELETED** (see surprise 3) |
| **WithParent** | tree→tree | same / same | — | O(d) | O(d) | shared mutable List — same hazard; BFT dimension semantically wrong | **DELETED** (see surprise 3) |
| **ToFormattedLines** | tree→enum | DFT (token stream) + Reverse | — | O(n) | — | reverses whole token stream + Stack of all lines | NO |
| **ToFormattedString** | tree→scalar | DFT (→ ToFormattedLines) | — | O(n) | — | joins all lines | NO |
| **ToDepthFirstTreeTokenizer** | tree→tokens | DFT / — | DFT only (niche) | O(1) | — | none | NO |
| **ToBreadthFirstTreeTokenizer** | tree→tokens | — / BFT | BFT only (niche) | — | O(1) | none | NO |
| **GetTreenumerator** | accessor | strategy param | n/a | O(1) | O(1) | none (raw dispatch) | NO |
| **ExpandNode** (exp) | tree→tree | DFT / **BFT throws NotImplemented** | DFT only (incomplete) | O(d)? | — | BFT unimplemented | NO |
| **Graft** (exp) | tree→tree | DFT / **BFT throws NotImplemented** | DFT only (incomplete) | O(d)? | — | BFT unimplemented | NO |
| **Collapse** (exp) | tree→enum | **NotImplemented** | — | — | — | stub | NO |
| **InorderTraversal** (exp) | tree→enum | **NotImplemented** | — | — | — | stub | NO |
| **RepeatTrees** (exp) | — | commented out | — | — | — | not compiled | — |

---

## The disclosure rule (adopted 2026-07-11)

Supersedes the per-operator "no native BFT overload" recommendations below for **capture
operators** (tree→tree operators whose semantics require an O(n) capture regardless of source
dimension). The rule:

> **An operator may capture implicitly iff its return type discloses it
> (`ITreenumerableBuffer<T>` in the signature). Where the return type cannot carry the
> disclosure (enumerable/scalar returns), the operator stays dimension-fixed and the caller
> escalates explicitly.**

Rationale (decided against the alternative of consumer-side opt-in — buffer-input-only
signatures): the capture cost is intrinsic to these operators' semantics, not to the source's
dimension — a depth-first source pays the same O(n) a breadth-first one would, so a compile
error on one entry point marks no cost cliff; it just charges ceremony to one caller class for
a price every caller pays. The buffer return type is a stronger disclosure than LINQ ever gave
(`Reverse`/`OrderBy` read typographically identical to `Select` there), and the explicit shape
stays available to anyone who wants the capture hoisted and visible at the call site
(`source.Materialize().LeaffixScan(...)`). Compile errors are reserved for the impossible, not
the merely costly. `Tree.Defer` re-enumeration by sibling capture ops is intended behavior,
not a hazard: Defer's contract IS fresh-per-acquisition; capture-once intent is what
`Lazy`/`Memoize`/`Materialize` express.

Consequences as built:
- **Invert** already conformed (its composite/DFT arms capture implicitly behind the buffer
  return type); it additionally keeps its genuinely-streaming BFT-narrow arm.
- **LeaffixScan** and **OrderChildrenBy** gained breadth-first-narrow source overloads
  (capture via `Materialize`, then the depth-first build over the capture's replay) plus the
  composite disambiguator overloads that a second narrow overload makes mandatory.
- **LeaffixAggregate** is the carve-out: it returns an enumerable, so no type-level disclosure
  is possible, and its DFT form is genuinely lazy per root (peak = largest root subtree,
  early-out works) — a buffering BFT overload would change its cost class silently. It stays
  dimension-fixed.

## RETHINK candidates

### Invert (mirror)

Today `Invert` does two hidden O(n) things and consumes the *wrong* dimension: it drives the
source's **DFT** treenumerator to completion into flat pre-order `List<TNode>` + `List<int>`
subtree-size buffers, then **copies** those into a second pair of mirrored arrays with a stack
walk, and returns a `PreorderTree`. Both the result's DFT and BFT consumption then ride the
`PreorderTree` engine over the mirrored arrays — so the source is always fully materialized no
matter how the caller reads the result.

The organizing principle explains why: **mirror-over-DFT reverses arrival order.** The root emits
instantly, but the second node the DFT consumer is owed (the mirror's first child = the source's
*last* child) arrives from the source almost last. Gap = the whole tree, hit between the first and
second `MoveNext`. There is no bounded streaming strategy; the materialization is intrinsic.
Mirror-over-**BFT** is the benign case: reversing every sibling group at level *k* yields exactly
level *k* reversed end-to-end, so the gap is one level → **O(width)**, BFT's native frontier class.

**Per-dimension redesign:**
- `Invert(this IBreadthFirstTreenumerable<T>) : IBreadthFirstTreenumerable<T>` — lazy, O(width):
  buffer one level, emit it reversed. No materialization, no `PreorderTree`.
- **No DFT overload.** Mirror-over-DFT is untypeable without a visible `.Memoize()`; callers write
  `.Memoize().Invert()` so the O(n) lives in *their* code, deliberately.
- **Buffer-aware fast path.** Subtree sizes are invariant under mirroring (the current code's own
  comment), so mirroring a memo/`PreorderTree` capture is a lazy reverse-child replay over the
  *existing* arrays — the current step-2 stack walk made lazy, zero copied arrays. Sniff the buffer
  *type* at composition time; read capture *state* at enumeration time. Mirror twice → two views,
  zero copies.

### LeaffixScan

`LeaffixScan` is a bottom-up (leaffix) scan that must return a tree of the *same shape* with every
node's accumulated value, so its result is inherently O(n) in size. But the current implementation
is eager and DFT-fixed: one forward DFS pass fills whole-forest `List<TAccumulate>` +
`List<int>` buffers, then `.ToArray()` into a `PreorderTree`. Nothing is lazy — acquiring the
result treenumerator has already traversed and buffered the entire source.

Contrast `LeaffixAggregate` (its scalar sibling), which is *already* streaming: it reuses the
buffers per root and yields each root the moment its subtree closes, so peak memory is the largest
root subtree, not the whole forest, and early-out stops early.

**Per-dimension redesign:**
- A **lazy DFT** `LeaffixScan` treenumerator can emit each node's post-order result the moment its
  subtree closes, holding only the current root-to-node path plus the closed-subtree accumulations
  beneath the current path — O(depth) working set for the scan frontier (the same shape
  `LeaffixAggregate` already achieves per root), instead of eager O(n). Result type stays a tree;
  it just stops pre-building the arrays.
- **BFT** is the hard direction: leaffix values flow child→parent, i.e. against BFT's top-down
  arrival, so a streaming BFT leaffix needs the whole last level (all leaves) before any internal
  value resolves — O(width) at minimum and plausibly O(n) for bushy-then-narrow shapes. Recommend
  **no native BFT overload**; the BFT dimension of the result rides a buffer (exactly the memoize
  cross-order path), matching the doc's "PreorderTree exits Linq / LeaffixScan resolves with the
  dimension split" note. This is why LeaffixScan is RETHINK, not a mechanical YES: the honest DFT
  overload is O(depth) lazy, but the operator cannot offer a symmetric BFT one.

---

## WhatIf cost model notes (first draft of the cost lattice)

Lattice: **{O(1), O(d), O(w), O(n), O(n·d)}** where *d* = max depth, *w* = max width (frontier), *n*
= node count. **Time** below is per-full-traversal *source reads* (all full traversals are Θ(n)
reads unless noted); **Space** is the resident emission-vs-arrival buffer (the gap). The lattice
composes operator-by-operator along a chain (max of the members' space classes; sum of extra
passes for time).

| Operator | Time (DFT / BFT) | Space (DFT / BFT) | Notes |
|----------|------------------|-------------------|-------|
| Select, WithContext | O(n) / O(n) | O(1) / O(1) | pure map |
| Do, Hide, PruneAfter | O(n) / O(n) | O(1) / O(1) | stateless wrappers |
| TakeNodesUntil/While, TakeTrees | O(n) / O(n) | O(1) / O(1) | early-out shortens *n* |
| Where, PruneBefore, SkipTrees | O(n) / O(n) | O(d) / O(w) | canonical DFT-depth / BFT-width asymmetry |
| RootfixScan | O(n) / O(n) | O(d) / O(w) | path stack vs two-level buffer |
| Union, Intersection, Subtract, SymmetricDifference | O(n) / O(n) | O(d) / O(w) | merge stack vs merge queue; n = n_left+n_right |
| CountNodes, AnyNodes, AllNodes, Consume | O(n) / O(n) | O(1) / O(1) | strategy-param; Any/All early-out |
| GetTraversal, PreorderTraversal, LevelOrderTraversal | O(n) | O(1) | dimension-native stream |
| GetLeaves, GetRoots | O(n) / O(n) | O(1) / O(1) | dimension-agnostic (current impl hardcodes DFT) |
| GetLevels | O(n) | O(w) | one level resident; BFT-fixed |
| PostorderTraversal, GetBranches | O(n) | O(d) | reorder-within-path; DFT-fixed by semantics |
| RootfixAggregate | O(n) / O(n) | O(d) / O(w) | rides RootfixScan + GetLeaves; dimension-agnostic |
| LeaffixAggregate | O(n) | O(subtree_max) ≤ O(n) | per-root buffer reuse; DFT-fixed |
| **Invert (current)** | **O(n)** DFT-consume + O(n) copy | **O(n)** | materialize+copy; both result dims |
| **Invert (reworked BFT)** | O(n) | **O(w)** | windowed; buffer-view fast path → O(1) extra over a capture |
| **LeaffixScan (current)** | **O(n)** | **O(n)** | eager PreorderTree |
| **LeaffixScan (reworked DFT)** | O(n) | **O(d)** | lazy post-order emit |
| Memoize / Materialize | O(n) | **O(n)** | capture is the point; O(n) is contractual, not hidden |
| TakeLastTrees, SkipLastTrees | **O(n) + count pre-pass** | O(d) / O(1) | non-streaming (counts roots first) |
| WithLevelIndex, WithParent | O(n) / O(n) | O(d) / O(d) | shared-state; not re-enumerable (fix) |
| ToFormattedLines/String | O(n) | O(n) | reverse whole stream + all lines |

**Lattice gaps / reserved cells.** Nothing in the *current* library is O(n·d) — that class is
reserved for the future **cross-order re-scan** strategy (`AsBreadthFirst()` over a rewindable DFT
layout: depth+1 file passes → O(n·d) time, O(d) space), per TRAVERSAL_DIMENSION_SPLIT.md. It is the
canonical example of "time escalation as visible in code as `Memoize()` is for space," and the
reason the lattice carries an O(n·d) rung with no current occupant.

**Composition rule sketch for WhatIf.** For a chain, resident space = max over members of the
member's space class *in the traversed dimension*; source-read time = n × (1 + number of members
that force an extra pass — today only TakeLastTrees/SkipLastTrees, plus any future re-scan). The
dimension split absorbs the worst cross-order cells statically (they don't typecheck), so WhatIf
only ever reports over chains that are already affordable in the requested dimension — its job
narrows to distinguishing O(1)/O(d)/O(w)/O(n) residency and flagging the O(n·d) opt-in.
