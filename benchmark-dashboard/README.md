# Benchmark dashboard

Source of truth for the Copse benchmark dashboard published at
<https://copselib.github.io/copse-dotnet/> (the root redirects to `/benchmarks/`).

`index.html` is the whole dashboard: **no build step, no external libraries** (charts are inline
SVG, so chart text is real selectable/searchable DOM). It reads `window.BENCHMARK_DATA` from a
sibling `data.js`. It replaces the stock `benchmark-action/github-action-benchmark` template, which
rendered one Chart.js `<canvas>` per benchmark — an unsearchable, uncategorized wall.

## How it gets published — the two-workflow arrangement

`gh-pages` serves three files:

| File | Owned by | Contents |
|------|----------|----------|
| `index.html` (root) | **`deploy-dashboard.yml`** | redirect to `benchmarks/` (source: `benchmark-dashboard/root-redirect.html`) |
| `benchmarks/index.html` | **`deploy-dashboard.yml`** | this dashboard |
| `benchmarks/data.js` | **`benchmarks.yml`** | benchmark history (`window.BENCHMARK_DATA`) |

Two workflows cooperate, touching **disjoint files**:

1. **`.github/workflows/benchmarks.yml`** — runs BenchmarkDotNet on every push to `main`, appends
   results to `benchmarks/data.js` via `benchmark-action/github-action-benchmark`, and posts
   regression alerts (150% threshold).
   **Key fact:** that action only writes its *default* `index.html` when none exists — it **never
   overwrites** an existing one. So it leaves this custom dashboard alone and only manages `data.js`.

2. **`.github/workflows/deploy-dashboard.yml`** — because the action won't deploy a *custom*
   `index.html`, this workflow does. On any push to `main` that changes `benchmark-dashboard/**`
   (or the workflow itself), and on manual **workflow_dispatch**, it checks out `gh-pages` in a
   worktree, copies `benchmark-dashboard/index.html` into `benchmarks/index.html`, and pushes **only
   if it changed**. It shares the `benchmarks` concurrency group so the two workflows never push
   `gh-pages` at the same time.

## Editing the dashboard

1. Edit `benchmark-dashboard/index.html` on `main` (directly, or via a branch you merge to `main`).
2. The push to `main` triggers `deploy-dashboard.yml`, which syncs it to `gh-pages` automatically.

No manual `gh-pages` commit is needed. **Do not hand-edit `benchmarks/index.html` on `gh-pages`** —
it isn't version-controlled there and the next deploy overwrites it. Edit here instead.

## Previewing locally

The dashboard needs a `data.js` beside it. Grab the live one (this folder's `.gitignore` keeps it
from being committed), then open the file — it works straight from `file://`, no server:

```sh
git show origin/gh-pages:benchmarks/data.js > benchmark-dashboard/data.js
# then open benchmark-dashboard/index.html in a browser
```

Without `data.js` the page still renders but shows "no data".

## Re-deploy / self-heal

If `gh-pages` is ever reset/orphaned, or you want to force a re-sync after an edit:
**Actions → "Deploy Benchmark Dashboard" → Run workflow** (workflow_dispatch).

## Epoch markers

An **epoch** is a commit whose benchmark runs are not comparable to earlier history — a corpus
change, a measurement-boundary change, a runner-fleet change. Declare one by adding a trailer
to the benchmarked commit's message:

```
Benchmark-Epoch: <one-line reason>
```

Events predating the convention can be curated into the `EPOCHS` list at the top of
`index.html` (`{sha, label}`, any unique sha prefix).

Markers are global — an epoch re-baselines the whole corpus. On the dashboard: a dashed
vertical rule in every chart (hover for the reason), the 5-run trend median restarts at the
boundary, and the Δ-since-prev column and "biggest movers" suppress deltas that would cross
it — a cross-epoch delta is exactly the fake regression the marker exists to flag.

## Archived rows

A row that stops appearing in fresh runs (a rename, a retired benchmark) is not deleted —
the dashboard detects the stale series and marks it archived. Archived rows are hidden by
default behind the "show archived" toggle, render as muted dashed sparklines when shown,
are counted in the "N archived hidden" note, and are excluded from "biggest movers". A
renamed benchmark therefore starts a fresh series under its new name while the old series'
history stays reachable — the mechanism behind consult-first row renames (see
`design-docs/CHANGELOG_BENCHMARKS.md` for each rename's record).
