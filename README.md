# Copse

[![NuGet prerelease](https://img.shields.io/nuget/vpre/Copse.Linq)](https://www.nuget.org/packages/Copse.Linq)

LINQ for trees. `ITreenumerable<T>` is to trees what `IEnumerable<T>` is to sequences — a lazy,
composable abstraction supporting depth-first and breadth-first traversal with 40+ operations:
`Where`, `Select`, `GetLeaves`, `PruneBefore`, `LeaffixAggregate`, `Union`, and more.

No equality contract required: node types need not implement `IEquatable<T>` or override
`GetHashCode`.

## Install

```sh
dotnet add package Copse.Linq --prerelease
```

`Copse.Linq` transitively brings in `Copse` and `Copse.Core`. Targets **net48**, **net8.0**, and **netstandard2.0**.

## Examples

Adapt any tree type by implementing `IChildEnumerator<T>` — a struct Copse calls to enumerate each
node's children:

```csharp
using Copse;
using Copse.Linq;
using Copse.Treenumerables;
using System.Linq;

// Node n has children 2n and 2n+1 — a complete binary tree capped at 7.
struct BinaryChildren : IChildEnumerator<int>
{
    private int _next;
    private readonly int _last;
    private bool _disposed;

    public BinaryChildren(int parent)
    {
        _next = parent * 2;          // first child of n is 2n...
        _last = parent * 2 + 1;      // ...and its second (last) child is 2n+1
        _disposed = false;
    }

    public ChildResult<int> MoveNext()
    {
        if (_disposed || _next > _last || _next > 7)
            return default;

        var child = new NodeAndSiblingIndex<int>(_next, _next % 2);
        _next++;
        return new ChildResult<int>(child);
    }

    public void Dispose() => _disposed = true;
}

ITreenumerable<int> tree = new Treenumerable<int, BinaryChildren>(
    ctx => new BinaryChildren(ctx.Node), new[] { 1 });
//       1
//      / \
//     2   3
//    / \ / \
//   4  5 6  7
```

Once you have an `ITreenumerable<T>`, the full operation set is available. Operations compose
without materialization when possible — the streaming operators stay lazy end-to-end — and when
an operation does capture the tree (or might), its return type and docs say so:

```csharp
int[] preOrder = tree.GetPreorderTraversal().ToArray();  // [1, 2, 4, 5, 3, 6, 7]
int[] leaves   = tree.GetLeaves().ToArray();           // [4, 5, 6, 7]

// Select transforms values while preserving tree structure
int[] doubled  = tree
    .Select(node => node * 2)
    .GetPreorderTraversal()
    .ToArray();                                        // [2, 4, 8, 10, 6, 12, 14]

// PruneBefore removes a node and its descendants when the predicate is true
int[] topTwo   = tree
    .PruneBefore((node, position) => position.Depth >= 2)
    .GetLeaves()
    .ToArray();                                        // [2, 3]
```

**`Where` is structural.** A filtered-out node's children are promoted to the nearest remaining
ancestor — unlike `IEnumerable.Where`, which is a flat element filter:

```csharp
// Remove even nodes. Children of 2 (which are 4 and 5) become children of 1.
// 4 and 6 are also removed but have no children, so they simply vanish.
int[] filtered = tree
    .Where(node => node % 2 != 0)
    .GetPreorderTraversal()
    .ToArray();
// Result tree: 1(5, 3(7))  =>  [1, 5, 3, 7]
```

**`LeaffixAggregate`** folds bottom-up, one value per root: each family's completed child
accumulations are reduced pairwise (the edge accumulator), then the node folds itself in
once (the node accumulator); leaves answer through the leaf selector directly:

```csharp
int subtreeSum = tree
    .LeaffixAggregate(
        leaf => leaf,                                        // each leaf's own accumulation
        (accumulate, childAccumulate) => accumulate + childAccumulate,
        (accumulate, node) => accumulate + node)
    .First()
    .Accumulate;   // results are NodeAccumulations: the root's value paired with its fold
// 28  (1 + 2 + 3 + 4 + 5 + 6 + 7)
```

## Packages

| Package | Description |
|---|---|
| `Copse.Core` | Interfaces, enums, and position types (`ITreenumerable<T>`, `IWalkableTreenumerable<T,H>`, `TreeWalker`, `NodePosition`, …) |
| `Copse` | Depth-first and breadth-first traversal engine |
| `Copse.Linq` | LINQ-style tree operations (`Where`, `Select`, `GetLeaves`, `PruneBefore`, `LeaffixAggregate`, `Union`, tree-walker navigation, …) |
| `Copse.SimpleSerializer` | Text-format tree serialization for debugging and testing |
| `Copse.Core.Async` / `Copse.Async` / `Copse.Linq.Async` | The async family — the same surface over awaited pulls (these are the codegen sources the sync packages are generated from) |

## Documentation

Full documentation is coming to [copselib.org](https://copselib.org) (WIP). For now, the examples
above and the [source](https://github.com/copselib/copse-dotnet) are the reference.

## Benchmarks

Performance results are published at
[copselib.github.io/copse-dotnet](https://copselib.github.io/copse-dotnet/).

## License

MIT — see [LICENSE](LICENSE). © 2023–2026 Jason Boyd.

The disposable utilities in `Copse.Disposables` (`CompositeDisposable`, `RefCountDisposable`,
`Disposable.Create`, …) are adapted from [System.Reactive](https://github.com/dotnet/reactive)
(MIT, © .NET Foundation and Contributors) — same names, same semantics, no new concepts.
See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
