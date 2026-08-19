# Benchmark Project Improvements - Change Log

## Date: 2026-01-28

## Summary
Refactored the benchmark project to follow BenchmarkDotNet best practices, making it more flexible, maintainable, and production-ready.

## Changes Made

### 1. Program.cs - Complete Rewrite

**Before:**
- Manual list of benchmarks requiring commenting/uncommenting
- `[ShortRunJob]` on every benchmark class
- Tedious maintenance

**After:**
- Automatic benchmark discovery using `BenchmarkSwitcher.FromAssembly()`
- Auto-detects CI vs local environment
- Local: ShortRun for fast iterations
- CI: Full runs for accurate measurements
- No manual maintenance required

### 2. All 17 Benchmark Classes Updated

**Removed:** `[ShortRunJob]` attribute from all classes
**Added:** `[BenchmarkCategory]` attributes for filtering

#### Category Mapping

| Benchmark Class             | Categories                                |
|-----------------------------|-------------------------------------------|
| DepthFirstTreenumerator     | Traversal, DepthFirst                     |
| BreadthFirstTreenumerator   | Traversal, BreadthFirst                   |
| LevelOrderTraversal         | Traversal, LevelOrder                     |
| PostorderTraversal          | Traversal, Postorder                      |
| PreorderTraversal           | Traversal, Preorder                       |
| AllNodes                    | LINQ, Query                               |
| AnyNodes                    | LINQ, Query                               |
| CountNodes                  | LINQ, Query                               |
| GetLeaves                   | LINQ, Query                               |
| DepthFirstWhere             | LINQ, Filter, DepthFirst                  |
| BreadthFirstWhere           | LINQ, Filter, BreadthFirst                |
| Select                      | LINQ, Projection                          |
| PruneAfter                  | LINQ, Pruning                             |
| PruneBefore                 | LINQ, Pruning                             |
| SkipAllNodes                | LINQ, Skip                                |
| EnumerableToTree            | Conversion                                |
| RefSemiDeque                | DataStructures                            |

### 3. Bug Fix

**File:** `BreadthFirstTreenumerator.cs:25`
**Issue:** `CompleteBinaryTree_21` benchmark was calling `GetDepthFirstTraversal` instead of `GetBreadthFirstTraversal`
**Status:** Fixed

### 4. Documentation Created

- **BENCHMARK_CATEGORIES.md** - Reference guide for categories and usage examples
- **MIGRATION_GUIDE.md** - Migration instructions (for reference)
- **run-benchmarks.ps1 / .sh** - Quick command reference scripts
- **BENCHMARKING.md** - Updated with new workflow

### 5. GitHub Actions Workflow Updated

**File:** `.github/workflows/benchmarks.yml`

**Fixed:**
- Branch: `master` → `main`
- .NET SDK: 6.0.101 → 8.0.x
- Output file paths corrected
- Added PR benchmark comparisons
- Improved artifact handling

## Benefits

✅ **No more manual Program.cs edits** - benchmarks auto-discovered
✅ **Fast local development** - ShortRun mode automatically
✅ **Accurate CI tracking** - full benchmark runs in CI
✅ **Flexible filtering** - run by category or class name
✅ **Production-ready** - follows BenchmarkDotNet best practices
✅ **Bug fixed** - BreadthFirstTreenumerator now correct

## Usage Examples

### Local Development

```bash
# Run specific benchmark (what you're working on)
dotnet run -c Release -- --filter '*DepthFirstTreenumerator*'

# Run by category
dotnet run -c Release -- --filter '*' --category Traversal
dotnet run -c Release -- --filter '*' --category LINQ

# List all benchmarks
dotnet run -c Release -- --list flat

# Interactive mode
dotnet run -c Release
```

### CI (Automatic)

CI automatically runs ALL benchmarks with accurate measurements. No code changes needed.

## Verification

All changes verified:
- ✅ All 17 benchmark classes updated
- ✅ Build succeeds (Release configuration)
- ✅ Benchmark discovery works
- ✅ No `[ShortRunJob]` attributes remain
- ✅ All classes have `[BenchmarkCategory]` attributes
- ✅ Bug in BreadthFirstTreenumerator fixed

## Migration

No migration needed - everything updated in this change. The benchmark project is now production-ready and follows canonical BenchmarkDotNet patterns.

## Date: 2026-08-16 — The four-class buffer taxonomy (renames, history carried)

**The finding:** the `Memoize.Replay_*` grid's setup called `Materialize()` — correct in the
era when Materialize WAS Memoize + Complete (one artifact answered both replay questions),
silently wrong after the 2026-08-10 lazy rewrite split the products. The rows had been
measuring Materialize's flat-store replay all along (they are how the 25–35% flat-read win
finally surfaced), while the memo's own replay path had NO coverage.

**The taxonomy:** `{Materialize, Memoize} × {construction, replay}`, one class per question:

| Class | Rows | Provenance |
|---|---|---|
| `Materialize` | `Preorder_Triangle/Chain`, `LevelOrder_Triangle/Chain` | renamed (was `DftCapture_*`/`BftCapture_*` — names now speak the call: declared `BufferLayout` + tree) |
| `MaterializeReplay` | `Dft_over_Preorder`, `Bft_over_Preorder`, `Bft_over_LevelOrder`, `Dft_over_LevelOrder` | moved from `Memoize.Replay_*_over_*Capture` |
| `Memoize` | `FirstPass_*`, `Partial_*` | unchanged |
| `MemoizeReplay` | same grid as MaterializeReplay | NEW — the re-covered memo replay path |

**History:** carried in both stores — gh-pages `data.js` renamed by data-surgery commit
(latency and Memory suites); Bencher renamed in place in the web UI (the repo's
`BENCHER_API_KEY` is a project RUN key and cannot mutate resources — the one-off
`bencher-rename.yml` workflow died 401 and was deleted).

**The convention this mints** (also in BENCHMARKING.md): a benchmark whose coverage is
justified by implementation sharing ("A covers B because B is built on A") must NAME the
sharing in its comment — the justification expires with the sharing, and nothing else
audits it.

---

## Date: 2026-08-16 — BufferProbes: the probe/topology layer gets its first rows

**The gap:** the adjacency engines (`PreorderAdjacencyIndex` / `LevelOrderAdjacencyIndex` —
the machinery behind the buffer's TryGetParent/TryGetChildAt/TryGetRootAt surface) had zero
coverage, direct or indirect: every Buffer row rides the visit-stream decoders, which never
consult them; Materialize rows construct an index but never advance its scan; Aggregate rows
are stream sources, so the receiver-smart path never fires. The walker-era consumers
(GetTreeWalker navigation, buffer-receiver LeaffixScan) were flying dark. Seeded BEFORE the
planned adjacency-engine rework so the rework lands as a visible step in the series.

**New family** (`BufferProbes`, Buffer leg, MegaTriangle, MemoryDiagnoser):

| Row | Temperature | Pins |
|---|---|---|
| `Walk_over_MaterializedPreorder` | warm | steady-state probe reads, completed preorder engine |
| `Walk_over_MaterializedLevelOrder` | warm | completed level-order (isolates the parent merge) |
| `Walk_over_MemoizedPreorder` | cold (fresh `Memoize()` per invocation, fed from the settled capture) | the growing engine's incremental scan — time AND allocation, every invocation |
| `Walk_over_MemoizedLevelOrder` | cold | growing level-order twin |
| `LeaffixScan_over_MaterializedPreorder` | — | the bulk-fold seam (guard rail: must not move with probe machinery changes) |

Coverage is indirect through the walker surface (the engines are internal — no public door,
and no-IVT is law); the routing assumption is named in the class comment per the expiry
convention above.

---

## Date: 2026-08-16 — Materialize transpose rows (presize fast-path instrumentation)

**The gap:** every Materialize construction row captures from an ENGINE source — unknown
length, so the chunked build buffer is irreducible there. The counted-source capture paths
(transpose from a settled buffer; settle from a completed memo) had no rows, and the
planned presize fast-path (exact-size final arrays, skip the chunks: the disclosed 2n
transient drops to 1n) would land invisibly.

**New rows** (`Materialize` class, Buffer leg — same-leg by construction):

| Row | Body |
|---|---|
| `Preorder_from_LevelOrder` | settled level-order capture → `.Materialize(BufferLayout.Preorder)`, one forcing pull |
| `LevelOrder_from_Preorder` | the mirror transpose |

Seeded BEFORE the presize change; its 2n→1n step shows in these rows' Alloc column
(hardware-independent). The engine-source rows correctly cannot move — unknown length keeps
the chunks — which is itself the control. Settle-from-memo stays uncovered for now: it rides
the same CaptureFrom core as the transpose (the sharing named here per convention #4 — if
the memo settle ever grows its own capture path, this coverage claim expires).

---

## Date: 2026-08-16 — LeaffixScan projection witnesses (citizenship instrumentation)

Two rows added to `LeaffixScan` (Aggregate leg): `Select_Accumulate_Dft_Chain` /
`Select_Accumulate_Bft_Chain` — scan, project `.Accumulate`, consume. Seeded on MAIN while
the spelling is a stream veneer (full pair buffer + per-pull projection; first local
reading 112 ms / 88.1 MB and 125.6 ms / 60.0 MB), BEFORE the projection citizenship
(feature/select-composable, SELECT_INTO_CAPTURES_DESIGN.md) flips the same spelling's
route to a composed 1-wide build. The spelling never changes; the step will. Same-run
ratio against `Dft/Bft_Chain` prices the projection; the Compose family and the Select
family's Composition rows are the control (the streaming lattice must not move).

---

## Date: 2026-08-16 — RootfixScan projection witnesses (streaming-tier citizenship)

The rootfix mirror of the LeaffixScan witnesses: `RootfixScan.Select_Accumulate_{Dft,Bft}_Chain`
(Aggregate leg), seeded on main while the spelling stacks a Select wrapper over the scan's
treenumerator. RootfixScan STREAMS (no buffer), so unlike leaffix the expected step is
TIME-ONLY — the streaming projection citizenship removes one per-pull wrapper hop; nothing is
stored on either route. First local reading: 80.7 ms (Dft; the ~104 MB is the engine's
O(depth) chain path state, present on the plain rows too) and 70.0 ms / 2.2 KB (Bft).
Same-run ratio against the plain Chain rows prices the wrapper.

---

## Date: 2026-08-17 — FromSelect witnesses (compose-left instrumentation)

Four rows pricing an UPSTREAM Select wrapper on scan pulls, seeded on main while both scans
walk the wrapper per visit: `LeaffixScan.FromSelect_{Dft,Bft}_Chain` (123.5 ms / 90.2 MB and
126.5 ms / 61.5 MB local) and `RootfixScan.FromSelect_{Dft,Bft}_Chain` (96.1 ms and 75.0 ms —
+20-30% over the plain Chain rows). The LEAFFIX pair flips when feature/select-composable
merges (the compose-left door: the capture walks the un-projected inner raw); the ROOTFIX
pair holds the veneer baseline for the rootfix door, which is DEFERRED — its step lands
whenever that mirror is built. Same-run ratio against the plain rows prices the wrapper.

---

## Date: 2026-08-17 — Chained-projection and scan-of-scan witnesses

Two `LeaffixScan` rows (Aggregate leg) seeded ahead of the thin-shape refactor
(SELECT_INTO_CAPTURES_DESIGN.md): `Select_Select_Dft_Chain` — the functor law's benchmark
row; two Selects over the scan's buffer must collapse to one product build under ANY
citizenship machinery (baseline 89.8 ms / 84.06 MB — allocation identical to the
single-Select row proves today's collapse; a double-materializing route shows as a step up).
`Twice_Dft_Chain` — scan-of-scan; the second scan currently misses the span fast path's
concrete-type sniff and folds through the walker probes (baseline 231.9 ms / 272 MB — the
priced downgrade); the refactor's return to plain buffers heals it as a visible step down.

---

## Date: 2026-08-19 — The canonical trees are built with operators (finding, no rows changed)

No benchmark was added, renamed or rerouted. What changed is how the existing rows must be
READ, and it is significant enough to sit in the log so a future reader hits it before
trusting an era comparison.

`MegaTriangleTree()` is `new TriangleTree().PruneAfter((n, position) => …)` and
`MegaBinaryTree()` is `new CompleteBinaryTree().PruneBefore((n, position) => …)`. The prune
wrapper is a light-tier citizen, so a row's next operator does not stack on top of it — it
COMPOSES WITH IT, into a single machine over the raw generator. Every Triangle/Binary row
therefore measures its own operator plus a prune, one operator longer than its name; the Compose
family's chains are each one longer than advertised; and the tier-seal ruling (`e30bffc`) was
calibrated on `Where.Triangle_Mixed`, whose "light tier Func-donor" was this scaffolding rather
than a user-written operator. Same-run ratios within a family remain valid for the chains they
actually measured — nothing in the record is wrong, the labels are just short by one operator.

Ruled the same day: bound the generators natively (`TriangleTree(maxDepth)`,
`CompleteBinaryTree(maxDepth)`), identical node counts, no prune, no barrier. That lands as a
CORPUS EPOCH — every absolute shifts, Bencher's thresholds re-learn, and the gh-pages series
needs a marker at the boundary. Full analysis, measurements and rejected alternatives:
design-docs/BENCHMARKING.md, "What the canonical trees actually measure".

### 2026-08-19 (amended same day) — the barrier shipped, no rows renamed, no epoch

The corpus finding above is fixed. Every `CanonicalTrees` factory and the async/sync pair builders
now end in `.Hide(HideScope.Treenumerable)` — the shipped operator, scoped to strip the composition
doors and forward acquisition, nothing else.

The unscoped `Hide()` was tried first and rejected on measurement: it also wraps the treenumerator,
which costs on every pull (+31% `Forest`, +17-25% `Chain` on `CountNodes`, plus +48 B/row) and would
have diluted every future single-digit engine win. That prompted the `HideScope` parameter, so the
benchmarks now use the shipped operator rather than a local copy. The scoped barrier reproduces the
pre-barrier numbers on the rows this box can resolve. **CORRECTED 2026-08-19 after the first CI
A/B: the BARRIER is free, but isolation is not neutral -- prune-free rows (Chain, Forest) are
continuous, while Triangle/Binary rows shifted +13-18% as the accidental prune left the chain.
Those rows DO need a dashboard marker at 3aef7e0. Full A/B in BENCHMARKING.md.**

Also removed: `Treenumerables.cs` / `TreeShape.cs`, a dead parallel tree factory carrying the same
contamination, and `Union`'s private `HalfForest`, which moved into the factory as `MegaHalfForest`.
Every benchmark now takes its trees from `CanonicalTrees`, so the barrier cannot be bypassed.
