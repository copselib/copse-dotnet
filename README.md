# Copse

[![NuGet prerelease](https://img.shields.io/nuget/vpre/Copse.Linq)](https://www.nuget.org/packages/Copse.Linq)

LINQ for trees. `ITreenumerable<T>` is to trees what `IEnumerable<T>` is to sequences — a lazy,
composable abstraction supporting depth-first and breadth-first traversal with 45+ operations:
`Where`, `Select`, `GetLeaves`, `PruneSubtreesWhere`, `SelectMany`, `LeaffixAggregate`, `Union`,
and more.

No equality contract required: node types need not implement `IEquatable<T>` or override
`GetHashCode`.

## Install

```sh
dotnet add package Copse.Linq --prerelease
```

`Copse.Linq` transitively brings in the rest of the sync family (`Copse`, `Copse.Core`, and the
shared substrate packages). Targets **net48**, **netstandard2.0**, **netstandard2.1**, and
**net8.0**.

## Examples

Adapt any tree shape by implementing `IChildEnumerator<THandle>` — a struct Copse pulls to
enumerate each node's children. The handle is whatever identifies a node in *your* structure
(an object reference, an index, here the number itself):

```csharp
using Copse;
using Copse.Core;
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

    public Option<HandleAndSiblingIndex<int>> MoveNext()
    {
        if (_disposed || _next > _last || _next > 7)
            return default;

        var child = new HandleAndSiblingIndex<int>(_next, _next % 2);
        _next++;
        return new Option<HandleAndSiblingIndex<int>>(child);
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

(The two-parameter `Treenumerable<TNode, TChildEnumerator>` is the node-is-its-own-handle
convenience; the three-parameter form takes a `handle → node` map for trees whose surfaced
values can't produce their own children.)

Once you have an `ITreenumerable<T>`, the full operation set is available. Operations compose
without materialization when possible — the streaming operators stay lazy end-to-end — and when
an operation does capture the tree (or might), its return type and docs say so:

```csharp
int[] preOrder = tree.GetPreorderTraversal().ToArray();  // [1, 2, 4, 5, 3, 6, 7]
int[] leaves   = tree.GetLeaves().ToArray();             // [4, 5, 6, 7]

// Select transforms values while preserving tree structure
int[] doubled  = tree
    .Select(node => node * 2)
    .GetPreorderTraversal()
    .ToArray();                                          // [2, 4, 8, 10, 6, 12, 14]

// PruneSubtreesWhere removes each matching node with its whole subtree
int[] topTwo   = tree
    .PruneSubtreesWhere((node, position) => position.Depth >= 2)
    .GetLeaves()
    .ToArray();                                          // [2, 3]
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
    .Accumulate;   // each result is a NodeAccumulation: the root's value paired with its fold
// 28  (1 + 2 + 3 + 4 + 5 + 6 + 7)
```

**Walk instead of traversing.** `Materialize()` captures any tree into a walkable buffer whose
`TreeWalker` navigates freely — parent, child by index, root by index — with `GetNode()`
reading the focused node:

```csharp
var capture = tree.Materialize();
var walker = capture.GetTreeWalker().MoveToRoot(0).Value;
int root = walker.GetNode();                                  // 1
int secondChild = walker.MoveToChild(1).Value.GetNode();      // 3
```

**Serialize and back.** `Copse.SimpleSerializer` speaks a header-free text format in both
layouts — preorder `"a(b(d,e),c)"` and level-order `"a;b,c;d,e"` — with deferred parsing
(each enumeration parses exactly as far as it reads):

```csharp
using Copse.SimpleSerializer;

var parsed = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)");
string roundTrip = parsed.SerializeDepthFirstTree();          // "a(b(d,e),c)"
```

## Packages

| Package | Description |
|---|---|
| `Copse.Linq` | LINQ-style tree operations (`Where`, `Select`, `GetLeaves`, `PruneSubtreesWhere`, `LeaffixAggregate`, `Union`, tree-walker navigation, …) — the package to install |
| `Copse` | The traversal engines: the depth-first/breadth-first engine over the child-pull protocol, plus the flat preorder/level-order decoders |
| `Copse.Core` | The contracts: `ITreenumerable<T>`, `ITreenumerator<T>`, the walker tier (`TreeWalker`, `ITreeTopology`, `IWalkableTreenumerable`) |
| `Copse.Vocabulary` / `Copse.Primitives` / `Copse.Traversal` / `Copse.Linq.Traversal` | The shared substrate (value types like `NodePosition`, the chunked collections, the path-state machinery) — installed transitively, not directly |
| `Copse.SimpleSerializer` | Header-free text serialization, both layouts, sync and async |
| `Copse.Core.Async` / `Copse.Async` / `Copse.Linq.Async` | The async family — the same surface over awaited pulls (these are the codegen sources the sync packages are generated from) |

## Documentation

Every public member ships XML documentation (IntelliSense). The examples above and the
[source](https://github.com/copselib/copse-dotnet) are the reference; a documentation site at
[copselib.org](https://copselib.org) is in progress.

## Benchmarks

Performance results are published at
[copselib.github.io/copse-dotnet](https://copselib.github.io/copse-dotnet/).

## License

MIT — see [LICENSE](https://github.com/copselib/copse-dotnet/blob/main/LICENSE).
© 2023–2026 Jason Boyd.

The disposable utilities in `Copse.Disposables` (shipped in the `Copse.Primitives` package:
`CompositeDisposable`, `RefCountDisposable`, `Disposable.Create`, …) are adapted from
[System.Reactive](https://github.com/dotnet/reactive) (MIT, © .NET Foundation and
Contributors) — same names, same semantics, no new concepts. See
[THIRD-PARTY-NOTICES.md](https://github.com/copselib/copse-dotnet/blob/main/THIRD-PARTY-NOTICES.md).
