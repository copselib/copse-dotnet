---
name: benchmark-epoch
description: Use BEFORE committing any change that breaks benchmark comparability — canonical tree/corpus changes, measurement-boundary changes (Hide/Isolate scaffolding), benchmark harness or runner changes, or BenchmarkDotNet job config changes. Also when reading dashboard history across a known break. Declares a Benchmark-Epoch commit trailer so the dashboard marks the boundary instead of charting a fake regression.
---

# Benchmark epochs: marking comparability boundaries

An **epoch** is a commit whose benchmark runs are not comparable to earlier history. The
dashboard (benchmark-dashboard/index.html) detects them and: draws a dashed vertical rule in
every chart (reason on hover), restarts the 5-run trend median at the boundary, and suppresses
the Δ-since-prev and "biggest movers" deltas that would cross it — showing "epoch" instead.

## How to declare one

Add a trailer to the **benchmarked commit's message** (the commit the benchmark workflow runs
against — the trailer travels in `commit.message` into the gh-pages data):

```
Benchmark-Epoch: <one-line reason>
```

For an event that predates the convention, curate it into the `EPOCHS` list at the top of
`benchmark-dashboard/index.html` instead: `{sha: "<unique prefix>", label: "<reason>"}`.

## When an epoch is warranted

- The **canonical trees change shape or size** (CanonicalTrees.cs constants, tree factories).
- The **measurement boundary moves** — what scaffolding composes into rows (the
  Hide(HideScope…) barrier, prune-based bounding, anything in BENCHMARKING.md's
  "what the canonical trees actually measure").
- **Row semantics change**: a benchmark method measures a different code path than before
  while keeping its name.
- The **runner fleet or job configuration changes** in a way that shifts absolute numbers
  (BenchmarkDotNet job, TFM, machine class).

## When an epoch is NOT warranted

- **A perf change in the library itself.** That delta is the signal the dashboard exists to
  show. Epoching it would hide it.
- **A fix that keeps history comparable.** Precedent: the HideScope corpus-isolation fix was
  deliberately declared NO-epoch because the rows measured the same thing before and after.
- **Row adds/renames** — those are the archived-row mechanism's job, and any benchmark add or
  rename is consult-first with Jason regardless (standing rule).
- **Bencher-side fleet changes** — Bencher's idiom is a new testbed, not an epoch.

Deciding whether history broke is a judgment call: when unsure, ask Jason before committing —
the trailer is easy to add at commit time and awkward to retrofit.

## Mechanism details

Detection is `/^Benchmark-Epoch:\s*(.+)$/m` over the stored commit message plus the curated
`EPOCHS` list — both in benchmark-dashboard/index.html. Markers are **global** (an epoch
re-baselines the whole corpus; there is no per-suite marker). The raw series line stays
connected across the boundary; only the trend and the deltas break. The dashboard README's
"Epoch markers" section is the human-facing record of the same convention.
