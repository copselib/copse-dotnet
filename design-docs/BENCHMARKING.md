# Continuous Benchmarking

This document explains the benchmark suite's organization and the continuous benchmarking setup.

## Suite organization

The suite is ~139 benchmarks in **ten families**, one `[BenchmarkCategory]` family tag per class,
so the CI legs partition the suite by construction:

| Family | Classes | What it measures |
|---|---|---|
| `Traversal` | Traversal, TraversalScaling | The raw engine drain (the baseline everything else is implicitly measured against); Scaling holds the only Stress-tier rows |
| `VisitStream` | Preorder/Postorder/LevelOrderTraversal | The filtered visit-stream adapters (dimension-locked) |
| `Query` | CountNodes, AllNodes, GetLeaves | Predicate/extraction terminals |
| `Streaming` | Where, PruneAfter, PruneBefore, Select | The streaming operator spine |
| `Merge` | Union, SymmetricDifference | Structural merge; SymmetricDifference-on-identical is the suppression pole of the Union-on-identical emission pole (their gap isolates emission cost) |
| `Buffer` | Materialize, Memoize, Invert | Capture builds and replays (capture-dimension × drain-dimension) |
| `Aggregate` | Leaffix/RootfixScan, Leaffix/RootfixAggregate | The cumulative-scan and aggregation duals |
| `Serialization` | Serialization | TreeSerializer round-trips |
| `DataStructures` | RefSemiDeque | The chunked ref-access collections |
| `AsyncOverhead` | AsyncOverhead* pairs | Sync/async ratio pairs — a different instrument (see below) |

**Naming**: class = operator; method = `{Dft|Bft}_{Shape}[_{Variant}]`. Dimension-locked rows
(preorder is DFT-derived, leaffix order is inherent, …) carry no prefix — an absent dimension row
*is* the documentation that the dimension doesn't apply.

**Workloads** come exclusively from `CanonicalTrees` (`src/Copse.Benchmarks/CanonicalTrees.cs`) —
the documented shape/size registry. Read its doc comment before adding or resizing anything: it
records the size tiers (Mega ≈ 2^20 quantized per shape; Stress ≈ 2^22, engine rows only), the
noise-floor invariant and its origin, the per-shape quantization table, and the exception policy.

**AsyncOverhead is different on purpose**: each class pairs one workload in both colors with the
sync side as `Baseline`, so the Ratio column is the ValueTask seam cost. Pairs must stay in one
class (same CI leg, same machine) — same-run ratios are the only trustworthy numbers on shared
runners.

## What's automated

On every push to `main` (and manual `workflow_dispatch` against any branch):
- Ten matrix legs run in parallel, one per family (`--allCategories <Family>`), worst leg ~9 min.
- Each leg records its runner's CPU model into the artifact (shared runners are a CPU lottery —
  EPYC 7763 / EPYC 9V74 / Xeon 8573C observed, ~±30% apart — and every leg draws its own machine).
- A single publish job then:
  - stores time + memory results per family to `gh-pages` — **main only**; branch dispatch runs
    never touch the dashboard's trend lines;
  - uploads one Bencher report per family, filed under a **per-CPU-model testbed**
    (e.g. `amd-epyc-7763-64-core-processor`), so Bencher's per-benchmark t-test thresholds learn
    each CPU's population separately and fleet changes never read as regressions. Skipped until
    the `BENCHER_API_KEY` repo secret (a Bencher *project* key, prefix `bencher_run_`) exists.

Full BenchmarkDotNet artifacts upload on every run (30-day retention) — including
`HostEnvironmentInfo`, which records the CPU model. **Check it before believing any cross-run
delta**; same-run ratios are the only comparison the runner lottery can't fool.

## Viewing results

- **Dashboard** (gh-pages, source in `benchmark-dashboard/index.html`, deployed by
  `deploy-dashboard.yml`): grouping, filtering, sparklines, expandable charts with per-commit
  dates. Renamed or deleted benchmarks automatically become **archived** (hidden behind the
  "show archived" toggle, shown muted with their last-reported date) — history is never deleted,
  it just stops cluttering the live view.
- **Bencher** (`bencher.dev/console`, project `copse-dotnet`): the Perf page renders once you
  select branch + testbed + measure + benchmarks; per-report links are printed in the publish log.

## Running locally

```bash
cd src/Copse.Benchmarks

# One family
dotnet run -c Release -- --allCategories Streaming

# One class / one row
dotnet run -c Release -- --filter '*Where*'
dotnet run -c Release -- --filter '*Union.Dft_Chains*'

# List everything
dotnet run -c Release -- --list flat
```

Local runs automatically use fast `ShortRun` mode; CI uses the accurate default job (detected via
`GITHUB_ACTIONS`). Local absolute numbers are only comparable to other runs on the same machine.

## Adding a benchmark

1. Add rows to the matching operator class (or a new class in `Benchmarks/`), tagged
   `[BenchmarkCategory("<Family>", "<Sub>")]` — the **first** tag must be an existing family, which
   is what routes it to a CI leg, a dashboard suite, and a Bencher report automatically.
2. Take workloads from `CanonicalTrees`; follow the `{Dft|Bft}_{Shape}[_{Variant}]` naming; give
   both dimension rows if the operator supports both, and the capture×drain matrix if it returns
   a buffer.
3. Sanity: every time row should clear ~1 ms on the slowest runner (~10 ms target) — see the
   noise-floor notes in `CanonicalTrees`.
4. **Coverage-transfer expires with the sharing that justified it** (the 2026-08-16 lesson —
   see CHANGELOG_BENCHMARKS.md): if a row's coverage argument is "A covers B because B is
   built on A," the row's comment must NAME that sharing — when B's implementation diverges,
   nothing else audits the claim, and the suite quietly stops meaning what it meant. Row
   names speak the operator call under test (its method and arguments), never an
   implementation detail that can drift.

Adding a whole new **family** is a bigger step: the workflow's matrix list, the publish job's
find-results loop, two store steps, and the Bencher loop all enumerate families explicitly in
`.github/workflows/benchmarks.yml`.

## Configuration notes

- `alert-threshold: '150%'`, `fail-on-alert: false` on the gh-pages stores — informational only.
- Bencher runs without `--error-on-alert` (trial mode); add it once thresholds have history and
  regressions should block.
- The first store step fetches gh-pages, the rest skip the fetch, the last one auto-pushes — one
  gh-pages commit per run.

## Reading ratios across machines and eras (doctrine, 2026-08-17)

Ratified after the leaffix composed-route paradox (three failed fix rounds before a
three-arm profile resolved it — the "regression" was a measurement artifact):

- **A ratio is a property of (spelling, machine) whenever the arms differ in cost SHAPE.**
  Per-visit wrapper costs are hardware-dependent (+7% on the CI runner pool, +23% on the
  codespace, for the same wrapper); fixed array-pass costs are stable everywhere. A ratio
  of a wrapper-shaped arm over a pass-shaped arm therefore moves with the machine, and
  comparing that ratio across eras (pre-change CI runs vs post-change CI runs) silently
  assumes machine-stability of relative costs — invalid. Corollary: CI under-credits
  wrapper-elimination features and over-weights array passes.
- **The dashboard's series stay valid for accidental regressions** — a real regression
  moves every machine the same direction. What they cannot adjudicate is a REROUTE (the
  same spelling switching implementation route): that verdict needs same-machine,
  same-run arms.
- **The reroute-verdict rule:** any change that reroutes a spelling gets a multi-arm
  harness verdict (plain / veneer / composed — or the routes in question) on one machine
  in one session, before CI deltas are read as truth. The scan harness lives outside the
  suite deliberately (BDN ceremony is unnecessary for A/B/C on one box): a ~40-line
  console app, 3 warmup + 20 timed iterations per arm, arms interleaved to catch drift.

## What the canonical trees actually measure (doctrine, 2026-08-19)

Discovered while asking a much smaller question — whether the suite exercised the positional
overloads of the operators at all.

**The canonical trees are built WITH OPERATORS, and those operators join the chain under
test.**

```csharp
MegaTriangleTree() = new TriangleTree().PruneAfter((n, position) => position.Depth == 1448)
MegaBinaryTree()   = new CompleteBinaryTree().PruneBefore((n, position) => position.Depth == 20)
```

`MegaChainTree` (`ToTrivialForest`) and `MegaDeepChainsTree` (`DeepTree`) are prune-free,
which is exactly why they make good controls.

**They FUSE — they do not merely sit underneath.** The positional `PruneAfter` builds an
`AsyncPruneAfterTreenumerable`: a light-tier citizen with `Relabels == false`. The row's next
operator probes `IAsyncSelectWhereTreenumerable`, finds it, passes the join-rule guard, and
composes — producing ONE driver over the raw `TriangleTree` carrying
`ComposedResultSelector(PruneAfterResultSelector, <the row's own leg>)`. The scaffolding
wrapper disappears *into* the machine being measured.

**Consequences for the historical record.** No past conclusion is wrong — same-run ratios
within a family remain valid for the chains they actually measured — but the labels
understate chain length by one:

- `Where.Dft_Triangle_Mixed` is `TriangleTree → positional PruneAfter → positional Where`,
  fused into one driver. **The tier-seal ruling (`e30bffc`, 2026-08-04) was calibrated on
  this row**, diagnosed as "the light tier is the one Func-donor splice participant" — and
  that participant is the canonical tree's own prune, i.e. scaffolding rather than a
  user-written operator. The mechanism was identified correctly, and the shape is realistic
  (bound-by-depth-then-filter is ordinary code); what was invisible is that it was there at
  all.
- Every Compose row is one operator longer than its name. `SelectWhere_Composed` is
  prune+Select+Where; `FiveOperators_Composed` is six operators; the reunification's
  1.1x → 2.9x collapse was measured on a six-operator chain.
- **Any change to prune machinery moves every row in the suite at once.** Observed
  2026-08-19: converting the positional overloads to a different spelling inflated every
  Triangle and Binary row across unrelated families, and was initially misread as machine
  load.

**Measured cost of the scaffolding participating** (`CountNodes`, whose own operator was
untouched; the prune-free trees held within 1.3% across builds, confirming machine state):

| row | main | positional overloads rerouted |
|---|---|---|
| `Dft_Binary` (PruneBefore base) | 100.6 ms / 2995 B | 109.3 ms / 3299 B |
| `Dft_Triangle` (PruneAfter base) | 31.5 ms | 59.2 ms |

Reverting *only* PruneBefore's reroute returned `Dft_Binary` to 99.96 ms and **2995 B —
allocation matching main byte for byte**. Allocation is a machinery fingerprint that does not
drift with load; when a timing comparison is in doubt, it is the more trustworthy signal.

**The fix (ruled 2026-08-19; the historical shift is accepted): bound the generators
natively.** `new TriangleTree(maxDepth: 1448)` and `new CompleteBinaryTree(maxDepth: 19)`
instead of unbounded-plus-prune. Node counts are identical (1449 × 1450 / 2 is exactly the
`MegaTriangle` constant), so no shape moves; what disappears is a per-node positional
predicate and a wrapper that silently joined every chain under test.

Rejected alternatives, and why:

- `Materialize().Hide()` — changes the workload FAMILY (engine tree → flat store, a
  substantially faster decode path), so every row would answer a different question than the
  corpus was built to ask. Its laziness is separately awkward: the build would land inside the
  first measured invocation unless hoisted to `[GlobalSetup]` and forced.
- `Hide()` alone — correctly isolates composition (it is the isolation barrier and claims no
  doors), but keeps the prune's per-node cost in every baseline and adds a wrapper layer per
  pull.

**The rule going forward: scaffolding must not be an operator.** If a corpus tree ever needs a
shape the generators cannot produce, isolate it with `Hide()` so it cannot join the algebra,
and say so in the row's comment. Separately, now that prune-fused-with-filter is known to be a
real and common shape, it deserves a few DELIBERATE labelled rows rather than accidental
coverage.

When the fix lands it is a corpus epoch: every absolute shifts, so mark the commit for the
dashboard (see `CHANGELOG_BENCHMARKS.md`) and expect Bencher's thresholds to re-learn — the
same class of event the per-CPU testbeds already handle for fleet changes.

## References

- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [github-action-benchmark](https://github.com/benchmark-action/github-action-benchmark)
- [Bencher](https://bencher.dev/docs/)
