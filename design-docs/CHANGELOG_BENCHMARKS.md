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
