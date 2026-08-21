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
| `Streaming` | Where, PruneAfter, PruneBefore, Select, SelectMany | The streaming operator spine |
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

**They COMPOSE -- they do not merely sit underneath.** The positional `PruneAfter` builds an
`AsyncPruneAfterTreenumerable`: a light-tier citizen with `Relabels == false`. The row's next
operator probes `IAsyncSelectWhereTreenumerable`, finds it, passes the join-rule guard, and
composes — producing ONE driver over the raw `TriangleTree` carrying
`ComposedResultSelector(PruneAfterResultSelector, <the row's own leg>)`. The scaffolding
wrapper disappears *into* the machine being measured.

**Consequences for the historical record.** No past conclusion is wrong — same-run ratios
within a family remain valid for the chains they actually measured — but the labels
understate chain length by one:

- `Where.Dft_Triangle_Mixed` is `TriangleTree → positional PruneAfter → positional Where`,
  collapsed into one driver. **The tier-seal ruling (`e30bffc`, 2026-08-04) was calibrated on
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
and say so in the row's comment. Separately, now that prune-composed-with-filter is known to be a
real and common shape, it deserves a few DELIBERATE labelled rows rather than accidental
coverage.

When the fix lands it is a corpus epoch: every absolute shifts, so mark the commit for the
dashboard (see `CHANGELOG_BENCHMARKS.md`) and expect Bencher's thresholds to re-learn — the
same class of event the per-CPU testbeds already handle for fleet changes.

## References

- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [github-action-benchmark](https://github.com/benchmark-action/github-action-benchmark)
- [Bencher](https://bencher.dev/docs/)

### The fix that shipped: an isolation barrier with no per-pull cost

Every factory in `CanonicalTrees` now ends in `.Hide(HideScope.Treenumerable)`, and so do the
async/sync pair builders. That result claims no composition doors, so scaffolding can never join the
algebra under test, and it forwards acquisition and nothing else.

**It is deliberately not `Copse.Linq`'s `Hide`.** `Hide` does two jobs: its treenumerABLE strips the
doors (the isolation, free — one virtual call per acquisition), and its treenumerATOR wrapper hides
the concrete type too (a real cost on every `MoveNext`). Benchmarks need only the first. Measured on
`CountNodes`, `Hide` cost +17-25% on `Chain` and +31% on `Forest` — worst exactly where margins are
thinnest, on the cheap shapes that measure little more than the engine — while the scoped barrier reproduces the pre-barrier numbers to within noise on all eight rows. That distinction matters beyond tidiness:
a fixed per-pull layer shrinks every measured win by the fraction of the row it occupies, so a
single-digit engine improvement would partly disappear into the scaffolding. Nothing in the library
sniffs a treenumerator type (every probe is at the treenumerable layer), so leaving it visible reroutes nothing -- pinned by
`RepresentationPinTests.HideScopeLaw_*`.

**PARTIAL EPOCH -- corrected 2026-08-19 after the first CI A/B.** The barrier itself is free, but
that is not the same as the change being neutral: the whole point was to stop the prune COMPOSING, so
every row where it had been composing necessarily moves. See the measured A/B below.

### The measured effect (CI A/B, 2026-08-19): which rows broke and which did not

`2acd8e2` was the last full CI run on the contaminated corpus and `d2b4d14` the first on the
isolated one, same fleet — a clean A/B.

**Prune-free rows are the control.** `Chain` and `Forest` are built from `ToDegenerateTree` /
`ToTrivialForest` with no prune, so isolation cannot affect them. They span −4.8% to +3.8%,
mean ≈ +0.3% — normal fleet variation, and the two runs are comparable.

**Pruned rows moved well outside that band:**

| row | contaminated | isolated | Δ |
|---|---|---|---|
| `Where.Dft_Triangle_Mixed` | 52.3 ms | 59.8 ms | **+14.2%** |
| `Where.Bft_Triangle_Mixed` | 56.7 ms | 64.4 ms | **+13.4%** |
| `Compose.Bft_Triangle_SelectWhere_Composed` | 62.1 ms | 73.2 ms | **+17.8%** |
| `Compose.Bft_Triangle_FiveOperators_Composed` | 66.9 ms | 75.7 ms | **+13.2%** |

That is the accidental prune leaving the chain, working as intended: those rows were absorbing
an operator for free and now pay for it. `Where.Triangle_Mixed` — the row the tier-seal ruling
(`e30bffc`) was calibrated on — was running ~14% faster than its name implied.

**Allocation corroborates the mechanism.** Every row rose by a fixed 32–150 B and nothing
proportional to node count, on rows allocating from hundreds of bytes to 37 MB. That is one
extra treenumerator object per acquisition — the un-composed prune — not a per-node cost.

**What survived**: the `FiveOperators` DFT collapse holds at 2.86× (was 2.90×), so that
headline was never an artifact. The `SelectWhere` composed/stacked ratio softened, 0.79 → 0.88
DFT, because the composed spelling lost a free absorbed operator while the stacked one did not.

**The rule for reading history**: prune-free rows (`Chain`, `Forest`) are continuous across
`3aef7e0`. Every `Triangle`- and `Binary`-derived row is **not**, and wants a dashboard marker
there.

**Methodological note, recorded because it nearly shipped as a wrong conclusion**: the local
verification used the `CountNodes` family as its control and found no change — but `CountNodes`
applies no operator, so the prune had nothing to compose *with* there. It was the one family
structurally incapable of showing the effect. Verifying a composition change requires a row
that composes.

### The relabels door move (`51950ea`, 2026-08-19): a measured no-op

Same corpus both sides, so this isolates the door move. Timings moved within ±4.3% (most within
±2%, mean ≈ −0.7%, no systematic direction), and **38 of 41 Streaming allocations were
byte-identical**; the three that differed moved 43 B, 61 B and 1,628 B on rows allocating 3.8 KB
to 27.5 MB — diagnoser jitter, not machinery. The door move builds the same machines, which is
what the throw-on-false probe predicted: with the driver's `Relabels` always true, "stack
always" is the identical answer, not a conservative one.
