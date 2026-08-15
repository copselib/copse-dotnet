# Spike: DFT `Where` via Extraction (stream rewrite)

**Status: explored, NOT pursued.** Kept as a documented reference and source of insight.
**Branch:** `spike-dft-extraction` (do not merge — see "Decision").
**Date:** 2026-06-21.

---

## TL;DR

We built a clean-room, *stream-rewriting* ("extraction") implementation of the depth-first
`Where` operator as a candidate replacement for the production collapse-then-reconcile
`WhereDepthFirstTreenumerator`. It is **correct** — wired in as the production DFT `Where` path it
passes the **entire** test suite (15,177 tests). But it is **roughly par on code size** and a
**performance regression** (dramatically more allocation; slower on deep and heavily-filtered
trees). With no clear win and real swap risk on battle-tested code, we decided **not to adopt it**.

The valuable output is the *understanding* (below), not the code.

---

## Motivation / thesis

A first-principles reassessment of `Where` produced a clean mental model:

> **Filtering a node = splicing that node's visit stream into its parent's.** In a full
> (unfiltered) traversal, an extracted node's own interleave visits already occur *at the right
> times*; you can **reassign** them to the nearest accepted ancestor and relabel positions, rather
> than telling the engine to skip and then *manufacturing* the parent visits back.

Crucially this is clean for **DFT only**, because DFT's natural traversal order already equals the
effective (post-extraction) order — no reordering is needed. **BFT cannot use this approach**: its
level-order emission scatters a node's effective siblings across depths, so producing
effective-position order would require buffering = the Θ(N) "reorder wall" (see
[the BFT fundamental-limit note]). That asymmetry is itself a key finding.

The production wrapper instead forwards `SkipNode` to the base engine (which *collapses* a promoted
brood into one parent visit-slot at raw positions) and then reconciles — needing a sentinel node, a
second stack, and a one-step `_HasCachedChild` defer.

## What was built

- **`WhereDepthFirstTreenumerator2`** (`src/Copse.Linq/Treenumerators/Filter/`) — consumes the
  **full** inner DFT stream (`TraverseAll`), suppresses extracted nodes, reassigns their interleave
  visits to the nearest accepted ancestor, and relabels accepted nodes with compressed depth +
  renumbered sibling indices. One `List<Frame>` path stack; **no defer/cache, no sentinel**.
- Consumer traversal strategies are handled by **forwarding** to the inner where the engine's native
  semantics match (`SkipNode` collapse, `SkipDescendants`, `SkipNodeAndDescendants`), plus three
  spike-side mechanisms: a `CollapseSkipped` frame flag (suppress reassigning onto a consumer-
  collapsed node), an empty-collapsed-slot drop, and for `SkipSiblings` a prune + visit-count cap +
  a stack of skip scopes with collapse-aware effective-parent lookup.
- **`Treenumerable.WhereSpike(...)`** — isolated wiring; and on this branch
  `Treenumerable.Where` is swapped to the spike for the DFT path so the whole suite validates it as
  a drop-in. (BFT still uses production.)
- **`DepthFirstExtractionScan`** test — exhaustive over every predicate subset of 19 stress trees
  (1,840 cases incl. the i-group `a(b(d,e,f),c(g,h,i))`).

Commits: `808e33f` (core, defer-free) -> `64df666` (consumer strategies, 76->6 failures) ->
`b32f857` (empty collapsed slot, 6->3) -> `9a4c72c` (SkipSiblings, full suite green).

## Results

### Correctness — full drop-in
Wired in as the production DFT `Where`, the spike passes the **entire** suite (Copse.Tests 438 +
Copse.Linq.Tests 14,739, 0 failures), plus the 1,840-case exhaustive predicate scan matches
production exactly (a *different* algorithm, so agreement is strong evidence).

### Simplicity — roughly par
| | Production | Spike |
|---|---|---|
| code lines (non-comment) | 196 | 207 |
| defer / cache | `_HasCachedChild` | **none** |
| data structures | 2 `RefSemiDeque` + sentinel | 1 `List<Frame>` + aux flags |
| promotion handling | **delegates to base engine** | **self-contained** (vanilla inner stream) |

The *predicate-only* core was genuinely simpler and defer-free, but the full consumer-strategy
matrix (especially `SkipSiblings` combined with collapse, effective-roots, and nesting) reabsorbed
the complexity to roughly par. The spike's distinctive qualities are **defer-free** and
**decoupled from base-engine promotion**.

### Performance — regression (DFT `Where`, BenchmarkDotNet ShortRun)
| Benchmark | Production | Spike | Runtime | Prod alloc | Spike alloc |
|---|---|---|---|---|---|
| TrivialForest WhereAll (1M, shallow) | 54.8 ms | 37.7 ms | spike **1.5x faster** | 1.42 KB | 45.78 MB |
| TrivialForest WhereNone (1M, shallow) | 9.7 ms | 28.0 ms | spike **2.9x slower** | 1.38 KB | 45.78 MB |
| TriangleTree PruneAfter 1448 (deep ~1448) | 86.5 ms | 767.2 ms | spike **8.9x slower** | 75.3 KB | 48.19 MB |

(ShortRun = 3 iterations, so runtime CIs are wide; allocations are deterministic. The
`*DegenerateTree_1M` benchmarks were **excluded** — a ~1M-deep path makes the spike O(N*H) = O(N^2),
which hangs for tens of minutes; production is O(N) and runs fine. That asymmetry is the prediction.)

Why:
- **Allocation ~33,000x worse** on shallow trees: `Frame` is a `class`, so the spike heap-allocates
  one object per node; production keeps a `struct` in a pooled `RefSemiDeque`.
- **Runtime worse on deep trees (8.9x):** O(N*H) per-node path walks (`YieldedAccepted` propagation
  + nearest-ancestor search) vs production's O(N) (O(1)/node via its stacks).
- **Runtime worse when heavily filtering (2.9x):** the spike consumes the *full* inner stream even
  when everything is filtered; production short-circuits via `SkipNode`. **This one is architectural,
  not just an implementation artifact.**
- **Faster on shallow keep-all (1.5x):** the spike's lean push/pop loop beats production's
  two-stack + sentinel + cache bookkeeping when there is no depth penalty — i.e. the spike's *core*
  is fast; allocation and depth-walks are what sink it.

## Decision: not pursued

Par simplicity + a performance regression + the swap risk of replacing a ~2-year battle-tested
class — with **no clear win** — does not justify adoption. Most of the perf gap (per-node `class`
allocation, O(N*H) walks) is *implementation*, not the approach, and is fixable (struct frames in a
`RefSemiDeque`, O(1) `YieldedAccepted`/ancestor bookkeeping); the WhereNone full-stream cost is the
one genuinely inherent penalty. But there was no compelling reason to invest further.

## What to keep (the actual value)

1. **The mental model:** `Where` = splice the filtered node's visit stream into its parent's.
2. **The DFT/BFT asymmetry:** DFT's natural order == effective order, so a wrapper stream-rewrite is
   viable and defer-free; **BFT hits the Θ(N) reorder wall** and cannot. The two operators are
   structurally different problems — the "one engine-native `ExtractNode` unifies DFT+BFT" idea is
   therefore *partially falsified* (DFT doesn't need the engine at all; BFT can't use a wrapper).
3. **Consumer strategies carry irreducible complexity** — especially `SkipSiblings` combined with
   collapse and effective-roots. Production parks this complexity in the base engine; the spike has
   to reproduce it in the wrapper.
4. **If ever revisited:** the spike's distinctive merit is being **decoupled from base-engine
   promotion**. That only becomes worth its perf cost if the base engine is reworked and we want DFT
   `Where` insulated from its promotion semantics.

## Pointers

- Code: `src/Copse.Linq/Treenumerators/Filter/WhereDepthFirstTreenumerator2.cs`,
  `src/Copse.Linq/Treenumerable/Treenumerable.WhereSpike.cs`,
  `src/Copse.Linq.Tests/DepthFirstExtractionScan.cs`.
- Production for comparison: `WhereDepthFirstTreenumerator.cs` (same folder).
- Reproduce benchmarks (Release): on each of `main` and `spike-dft-extraction`,
  `dotnet run -c Release --project src/Copse.Benchmarks -- --filter "*DepthFirstWhere.WhereAll_TrivialForest_1M" "*DepthFirstWhere.WhereNone_TrivialForest_1M" "*DepthFirstWhere.TriangleTree_PruneAfter_1448"`.

[the BFT fundamental-limit note]: ../CLAUDE.md
