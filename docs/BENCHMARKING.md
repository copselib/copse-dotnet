# Continuous Benchmarking

This document explains the benchmark suite's organization and the continuous benchmarking setup.

## Suite organization

The suite is ~137 benchmarks in **ten families**, one `[BenchmarkCategory]` family tag per class,
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

Adding a whole new **family** is a bigger step: the workflow's matrix list, the publish job's
find-results loop, two store steps, and the Bencher loop all enumerate families explicitly in
`.github/workflows/benchmarks.yml`.

## Configuration notes

- `alert-threshold: '150%'`, `fail-on-alert: false` on the gh-pages stores — informational only.
- Bencher runs without `--error-on-alert` (trial mode); add it once thresholds have history and
  regressions should block.
- The first store step fetches gh-pages, the rest skip the fetch, the last one auto-pushes — one
  gh-pages commit per run.

## References

- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [github-action-benchmark](https://github.com/benchmark-action/github-action-benchmark)
- [Bencher](https://bencher.dev/docs/)
