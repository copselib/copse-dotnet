# Continuous Benchmarking Setup

This document explains the continuous benchmarking setup for the Copse project.

## Overview

The project uses [BenchmarkDotNet](https://benchmarkdotnet.org/) for performance benchmarking and GitHub Actions for continuous integration. Benchmarks run automatically on every push to `main` and on pull requests.

## What's Automated

### On Push to Main
- Runs all benchmarks in the `Copse.Benchmarks` project
- Stores results in the `gh-pages` branch
- Compares against historical data
- Alerts if performance regresses by >150%
- Tracks regressions (>150% threshold) in the stored data — no commit comments or notifications are posted; regressions are visible on the GitHub Pages dashboard
- Publishes results to GitHub Pages

### On Pull Requests
- Runs all benchmarks
- Compares PR performance against main branch
- Comments on PR if performance changes significantly
- Does NOT fail the build (informational only)
- Uploads results as artifacts for review

## Viewing Benchmark Results

### GitHub Pages Dashboard
Once set up, benchmark trends will be available at:
```
https://<your-username>.github.io/<repository-name>/benchmarks/
```

### Artifacts
Every benchmark run uploads full BenchmarkDotNet artifacts that can be downloaded from:
- Actions → Select workflow run → Artifacts section
- Artifacts are retained for 30 days

## Configuration

### Alert Threshold
Currently set to **150%** regression (line 58, 76 in [benchmarks.yml](../.github/workflows/benchmarks.yml))

To adjust:
```yaml
alert-threshold: '150%'  # Change to desired percentage
```

### Fail on Alert
Currently set to `false` - benchmarks won't fail the build on regression

To make regressions fail the build:
```yaml
fail-on-alert: true  # Change to true
```

## Running Benchmarks Locally

The benchmark project automatically uses **fast ShortRun mode** for local development and **accurate default mode** in CI.

### Run Specific Benchmark Class
```bash
cd src/Copse.Benchmarks
dotnet run -c Release -- --filter '*DepthFirstTreenumerator*'
```

### Run by Category
```bash
# Run all traversal benchmarks
dotnet run -c Release -- --filter '*' --category Traversal

# Run all LINQ benchmarks
dotnet run -c Release -- --filter '*' --category LINQ

# Run depth-first related benchmarks (traversal + LINQ where)
dotnet run -c Release -- --filter '*' --category DepthFirst

# Combine categories
dotnet run -c Release -- --filter '*' --category Traversal --category LINQ
```

Categories are declared by each benchmark class's `[BenchmarkCategory]` attribute — that's the
source of truth. The vocabulary currently in use: feature areas (`Traversal`, `LINQ`,
`Conversion`, `DataStructures`, `Serialization`), operations (`Query`, `Filter`, `Projection`,
`Pruning`, `Skip`, `Merge`, `Invert`, `Leaffix`, `Materialize`, `Memoize`), and traversal
dimensions (`DepthFirst`, `BreadthFirst`, `Preorder`, `Postorder`, `LevelOrder`).

### List Available Benchmarks
```bash
dotnet run -c Release -- --list flat
dotnet run -c Release -- --list tree
```

### Interactive Mode
Run without arguments to choose interactively:
```bash
dotnet run -c Release
```

### Run All Benchmarks (Like CI)
```bash
cd src/Copse.Benchmarks
dotnet run -c Release -- --filter '*'
```

### Export Results
```bash
cd src/Copse.Benchmarks
dotnet run -c Release -- --filter '*DepthFirst*' --exporters json html
```

## Adding New Benchmarks

1. Create a new class in `src/Copse.Benchmarks/Benchmarks/`
2. Add `[MemoryDiagnoser]` and `[BenchmarkCategory]` attributes
3. Mark benchmark methods with `[Benchmark]`
4. **No Program.cs changes needed** - auto-discovered!

Example:
```csharp
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("YourCategory")]
public class MyBenchmark
{
    [Benchmark]
    public void MyOperation()
    {
        // Code to benchmark
    }
}
```

The benchmark will automatically be discovered and run in CI. Locally, you can run it with:
```bash
dotnet run -c Release -- --filter '*MyBenchmark*'
```

## First-Time Setup

### Enable GitHub Pages
1. Go to repository Settings → Pages
2. Source: Deploy from a branch
3. Branch: `gh-pages` / `/(root)`
4. Save

The first benchmark run on `main` will create the `gh-pages` branch automatically.

### Workflow Permissions
The workflow requires these permissions (already configured):
- `contents: write` - to push results to gh-pages
- `deployments: write` - for GitHub Pages deployment
- `pull-requests: write` - to comment on PRs

## Best Practices

### Benchmark Stability
- Benchmarks run on GitHub-hosted runners (ubuntu-latest)
- Shared infrastructure may cause variability
- Alert threshold accounts for normal variance
- Focus on significant regressions (>150%)

### Benchmark Duration
- **Local**: Automatically uses `ShortRunJob` for fast iterations (~15-30s per benchmark)
- **CI**: Automatically uses default job for accurate measurements (~1-2min per benchmark)
- Detection is automatic via `GITHUB_ACTIONS` environment variable

### Memory Benchmarks
- `[MemoryDiagnoser]` tracks allocations
- Useful for detecting allocation regressions
- Minimal overhead on runtime benchmarks

## Troubleshooting

### Workflow Fails - Results File Not Found
The workflow automatically finds the results file. If it fails:
1. Check BenchmarkDotNet.Artifacts/results/ was created
2. Verify benchmarks actually ran (check logs)
3. Ensure at least one benchmark completed successfully

### No GitHub Pages Site
1. Verify gh-pages branch was created
2. Check Settings → Pages is configured
3. Wait a few minutes after first push to gh-pages
4. Check Actions tab for Pages deployment

### Benchmarks Take Too Long
Consider:
1. Using `[ShortRunJob]` for development
2. Filtering benchmarks: `--filter '*SpecificClass*'`
3. Running fewer iterations locally
4. Splitting into separate workflow files by category

## References

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [GitHub Action Benchmark](https://github.com/benchmark-action/github-action-benchmark)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
