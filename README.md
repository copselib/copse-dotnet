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
    private bool _disposed;

    public BinaryChildren(int parent) { _next = parent * 2; _disposed = false; }

    public bool MoveNext(out NodeAndSiblingIndex<int> result)
    {
        if (_disposed || _next > 7) { result = default; return false; }
        result = new NodeAndSiblingIndex<int>(_next, _next % 2);
        _next++;
        return true;
    }

    public void Dispose() => _disposed = true;
}

ITreenumerable<int> tree = new Treenumerable<int, BinaryChildren>(
    ctx => new BinaryChildren(ctx.Node), 1);
//       1
//      / \
//     2   3
//    / \ / \
//   4  5 6  7
```

Once you have an `ITreenumerable<T>`, the full operation set is available. All operations are lazy
and compose without materializing intermediate trees:

```csharp
int[] preOrder = tree.PreOrderTraversal().ToArray();  // [1, 2, 4, 5, 3, 6, 7]
int[] leaves   = tree.GetLeaves().ToArray();           // [4, 5, 6, 7]

// Select transforms values while preserving tree structure
int[] doubled  = tree
    .Select(ctx => ctx.Node * 2)
    .PreOrderTraversal()
    .ToArray();                                        // [2, 4, 8, 10, 6, 12, 14]

// PruneBefore removes a node and its descendants when the predicate is true
int[] topTwo   = tree
    .PruneBefore(ctx => ctx.Position.Depth >= 2)
    .GetLeaves()
    .ToArray();                                        // [2, 3]
```

**`Where` is structural.** A filtered-out node's children are promoted to the nearest remaining
ancestor — unlike `IEnumerable.Where`, which is a flat element filter:

```csharp
// Remove even nodes. Children of 2 (which are 4 and 5) become children of 1.
// 4 and 6 are also removed but have no children, so they simply vanish.
int[] filtered = tree
    .Where(ctx => ctx.Node % 2 != 0)
    .PreOrderTraversal()
    .ToArray();
// Result tree: 1(5, 3(7))  =>  [1, 5, 3, 7]
```

**`LeaffixAggregate`** folds bottom-up: leaves are seeded first, then each parent is computed from
its children's accumulated results:

```csharp
int subtreeSum = tree
    .LeaffixAggregate(
        leaf         => leaf.Node,
        (node, kids) => node.Node + kids.Sum())
    .First();
// 28  (1 + 2 + 3 + 4 + 5 + 6 + 7)
```

## Packages

| Package | Description |
|---|---|
| `Copse.Core` | Interfaces, enums, and position types (`ITreenumerable<T>`, `NodePosition`, `NodeTraversalStrategies`, …) |
| `Copse` | Depth-first and breadth-first traversal engine |
| `Copse.Linq` | LINQ-style tree operations (`Where`, `Select`, `GetLeaves`, `PruneBefore`, `LeaffixAggregate`, `Union`, …) |
| `Copse.SimpleSerializer` | Text-format tree serialization for debugging and testing |

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
