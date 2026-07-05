# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with this repository.

## Project Overview

### Summary

**Copse** is a high-performance .NET tree traversal and manipulation library providing efficient, memory-conscious algorithms for:
- Tree traversal (Depth-First and Breadth-First)
- LINQ-style operations on trees (Select, Where, SelectMany, Union, etc.)
- Tree construction, filtering, pruning, and aggregation

### Core Functionality

#### Tree Traversal Engine:

- Two traversal strategies: depth-first and breadth-first
- Dynamic tree pruning during traversal using NodeTraversalStrategies flags
- Two-phase node visits (scheduling vs. visiting) for flexible processing
- Generic design adapts to any tree structure via custom child enumerators

#### LINQ-Style Operations (45+ methods):

- Query: Where(), Select(), CountNodes(), GetLeaves(), GetLevels()
- Aggregation: RootfixAggregate(), LeaffixAggregate(), cumulative scans (RootfixScan/LeaffixScan)
- Set operations: Union(), Intersection(), Subtract(), SymmetricDifference()
- Transformation: Invert() (mirror), Memoize()/Materialize(), pretty printing
- (SelectMany is designed but not yet implemented — see docs/SELECTMANY_DESIGN.md.)

#### Performance Optimizations:

- Custom RefSemiDeque<T> / RefAppendOnlyList<T> with ref semantics for zero-copy state management
- Lazy evaluation - operations compose without materializing intermediate trees
- Struct-based nodes to minimize allocations; the flat family decodes via span/index arithmetic
  (no per-node child enumerators), which measured faster than the engine on replay/deserialize

## Build & Test Commands

```bash
# Build the solution
dotnet build src/Copse.sln

# Run all tests
dotnet test src/Copse.sln

# Run specific test project
dotnet test src/Copse.Tests
dotnet test src/Copse.Linq.Tests

# Run benchmarks
dotnet run --project src/Copse.Benchmarks
```

## Architecture

### Design Philosophy

`ITreenumerable<T>` is a **tree monad**, analogous to how `IEnumerable<T>` is a list monad. This means it should obey the monadic laws (left identity, right identity, associativity). Since these laws cannot be strictly enforced by the type system alone, care must be taken when implementing new operations to preserve monadic behavior.

The library **never performs node equality comparisons**. This is a deliberate design choice that frees consumers from implementing `IEquatable<T>`, `IEqualityComparer<T>`, or overriding `Equals`/`GetHashCode` on their node types. Any type can be used as a node value without additional boilerplate.

### Project Structure

- **Copse.Core** - The dependency root. Contracts (`ITreenumerable<T>` and its two
  single-dimension parents, `ITreenumerator<T>`), the enums, and the vocabulary they speak
  (`NodePosition`, `NodeVisit`, `TreeTraversalStrategy`). References nothing.
- **Copse.Primitives** - Building blocks with no traversal semantics: the chunked ref-access
  collections (`RefSemiDeque`, `RefAppendOnlyList`), the lifted `Copse.Disposables` algebra, and
  the node-context value types (`NodeContext`, `NodeAndSiblingIndex`). References Core.
- **Copse** - The concrete treenumerables, in **two families** (see below): the *hierarchical*
  engine (`Treenumerable<,,>` + the DFS/BFS treenumerators, driven via `IChildEnumerator`) and
  the *flat* family (`PreorderTreenumerable`/`LevelOrderTreenumerable` + their store/stream
  treenumerators, over flat preorder/level-order encodings). Also the tree-source factories
  (`Tree.Defer`/`Using`/`Empty`) and the wrapper bases (`TreenumeratorBase`/`Wrapper`).
- **Copse.Linq** - LINQ-style tree operators only (extensions over the abstract contract; the
  memoize machinery rides the flat family, not a private engine).
- **Copse.Linq.Experimental** - In-progress operators (`ExpandNodes`, `Graft`).
- **Copse.Trees** - Sample trees (Collatz, Triangle, etc.).
- **Copse.SimpleSerializer** - Header-free text serialization: layout-named methods over the flat
  family (lazy string deserialize → full tree; forward-only reader deserialize → narrow tree).
- **Copse.TestUtils** - Test utilities, plus `EngineTree`/`PreorderTree` — the engine-backed
  **conformance oracle** (deliberately out of the product so the suites can diff the flat family
  against the real engine).

### Key Abstractions

- **ITreenumerable<T>** - Factory interface for creating treenumerators (analogous to
  `IEnumerable<T>`). It is a **pure composite** of the two single-dimension interfaces below.
- **IDepthFirstTreenumerable<T> / IBreadthFirstTreenumerable<T>** - The traversal-dimension split
  (see below). Each exposes just one `Get…Treenumerator()`; `ITreenumerable` derives from both.
- **ITreenumerator<T>** - Stateful traversal engine (analogous to `IEnumerator<T>`), `IDisposable`.
- **NodeTraversalStrategies** - Flags enum controlling traversal: `SkipNode`, `SkipDescendants`, `SkipSiblings`
- **TreenumeratorMode** - `SchedulingNode` (pre-order) vs `VisitingNode` (post-order)
- **NodePosition** - Tracks (sibling index, depth) in tree. `NodePosition.ForestRoot` is the
  pre-enumeration convention (position `(0, -1)`, VisitCount 0, mode SchedulingNode) that every
  fresh treenumerator sits at before its first `MoveNext` — load-bearing; conformance-checked.
- **NodeContext<T>** - Bundles node value with its position
- **NodeVisit<T>** - Complete visit information including mode and visit count
- **ITreenumerableBuffer<T>** - A re-traversable, lazily-growing capture of another tree (what
  `Memoize`/`Materialize` return); the typed "upgrade" from a narrow source back to the composite.

### The traversal-dimension split

`ITreenumerable<T> : IDepthFirstTreenumerable<T>, IBreadthFirstTreenumerable<T>`. The split makes
"which traversal orders can this source afford" a **compile-time fact**: a source that can only
afford one dimension (a forward-only serialized stream) implements just the matching narrow
interface, so asking it for the other dimension is a compile error rather than a hidden O(n)
buffering. `Memoize`/`Materialize` are the explicit escalation back to the full composite. Most
operators are `ITreenumerable`-only; the streaming spine (~Select, Where, prune/take, the
aggregates, set ops) has narrow overloads that preserve the dimension. See
docs/TRAVERSAL_DIMENSION_SPLIT.md and docs/OPERATOR_DIMENSION_AUDIT.md.

### Two families of concrete treenumerables

Classified by the data's shape:
- **Hierarchical** (the engine): child-shaped data adapted via the `IChildEnumerator` protocol.
  `DepthFirstTreenumerator<TValue, TNode, TChildEnumerator>` (stack-based) and
  `BreadthFirstTreenumerator<…>` (queue+stack). This is the family for arbitrary live trees.
- **Flat** (linear encodings decoded back into visit streams): preorder arrays / level-order
  arrays / serialized text, behind the store SPIs `IPreorderStore`/`ILevelOrderStore`
  (random-access) and `IPreorderStream`/`ILevelOrderStream` (forward-only). Four store
  treenumerators (DFT/BFT × preorder/level-order) plus the forward-only stream pair synthesize the
  exact same visit-stream contract from span/index arithmetic — no child enumerators.
  `PreorderTreenumerable`/`LevelOrderTreenumerable` (full, over random-access stores) and
  `PreorderStreamTreenumerable`/`LevelOrderStreamTreenumerable` (narrow, forward-only) are the
  wrappers. A memo buffer is an in-memory flat store; a serialized file is a persisted one; the
  memo replays and the deserializer share this machinery.

`RefSemiDeque<T>` / `RefAppendOnlyList<T>` (Copse.Primitives) are the high-performance chunked
ref-access collections both families build their state on.

**Conformance:** every `ITreenumerable` implementation must produce the engine's exact visit
stream. `VisitStreamConformance` (Copse.Linq.Tests) is the shared battery — corpus × full
strategy matrix, lockstepped against `EngineTree` (the engine oracle) — that `ContractTree`, the
flat family, and the serializer round-trips all ride.

### DFT vs BFT Scheduling and Visiting Behavior

Understanding the difference between DFT and BFT scheduling/visiting is critical when implementing treenumerator wrappers (like `Where`, `Select`, etc.):

**Key Invariant:** DFT and BFT produce the exact same scheduling and visiting nodes, just in a different order. Both strategies visit a parent node between scheduling each of its children.

**Depth-First Traversal (DFT):**
- Schedules a child, **visits it completely** (including all its descendants), then returns to the parent before scheduling the next sibling
- Children are visited immediately after being scheduled
- Stack-based: uses a single stack to track the current path from root to the current node

**Breadth-First Traversal (BFT):**
- Schedules all children at the current level, with parent visits interleaved between each schedule
- Children are NOT visited until ALL nodes at the current level have been scheduled
- Queue+stack-based: stack holds nodes being scheduled, queue holds nodes awaiting visits

**Example Tree:**
```
    a
   / \
  b   c
```

**DFT MoveNext() sequence:**
```
S a (schedule a)
V a (visit a, about to enumerate children)
S b (schedule b)
V b (visit b, no children)
V a (visit a again, between children)
S c (schedule c)
V c (visit c, no children)
V a (visit a, done with children)
```

**BFT MoveNext() sequence:**
```
S a (schedule a)
V a (visit a, about to enumerate children)
S b (schedule b)
V a (visit a, between children)
S c (schedule c)
V a (visit a, done scheduling children)
V b (visit b)
V c (visit c)
```

Note that both produce: S a, S b, S c, V a (×3), V b (×1), V c (×1) — just in different orders.

**Why This Matters for Wrapper Implementations:**

When implementing operators that filter or transform nodes (like `Where`), the wrapper must account for these different patterns. For example, when a node is filtered and its children are "promoted" to become children of the grandparent:

- In DFT: The child is visited immediately after scheduling, so the wrapper naturally sees schedule→visit pairs
- In BFT: All promoted children are scheduled (with inner parent visits between them) before any are visited. The wrapper must manufacture grandparent visits and suppress the inner's redundant ones (see `WhereBreadthFirstTreenumerator._PendingParentVisit` and `_ConsumeNextInnerParentVisit`)

### The Where Operation

The `Where` operation is one of the most complex operations in this library. Unlike `IEnumerable.Where()` which simply filters elements from a flat sequence, tree filtering must handle the structural implications of removing nodes from a hierarchy.

#### Core Concept: Child Promotion

When a node is filtered out, its children are **promoted** to become children of the filtered node's parent:

```
Original tree:     a              Filtered tree (remove b):    a
                  / \                                         /|\
                 b   c                                       d e c
                / \
               d   e
```

Node `b` is removed, but its children `d` and `e` become direct children of `a`. This is fundamentally different from `SkipDescendants` (which would remove `d` and `e` too).

#### Position Recalculation

Filtered nodes require recalculating positions for all remaining nodes:

1. **Depth compression**: Each filtered ancestor reduces the effective depth by 1
2. **Sibling index reassignment**: Remaining siblings get new sequential indices (0, 1, 2, ...)

```
Original:  a(b[0], c[1], d[2])     After filtering c:  a(b[0], d[1])
                                   (d's sibling index changes from 2 to 1)
```

#### Implementation Files

- **Extension method**: `Copse.Linq/Treenumerable/Treenumerable.Where.cs`
- **DFT implementation**: `Copse.Linq/Treenumerators/Filter/WhereDepthFirstTreenumerator.cs`
- **BFT implementation**: `Copse.Linq/Treenumerators/Filter/WhereBreadthFirstTreenumerator.cs`

> **Design history:** An alternative *stream-rewrite ("extraction")* DFT `Where` was spiked as a
> potential simpler replacement and **rejected** (correct full drop-in, but ~par on size and a
> performance regression). The write-up is worth reading before reworking `Where` — it captures the
> `Where`-as-visit-stream-splicing model and why the DFT approach does **not** generalize to BFT
> (the Θ(N) reorder wall). See [docs/DFT_WHERE_EXTRACTION_SPIKE.md](docs/DFT_WHERE_EXTRACTION_SPIKE.md)
> (branch `spike-dft-extraction`).

---

### WhereDepthFirstTreenumerator

DFT Where uses two parallel stacks to track the path from root to current node:

```csharp
private readonly RefSemiDeque<InternalNodeVisit> _NodeVisits;        // Accepted nodes
private readonly RefSemiDeque<InternalNodeVisit> _SkippedNodeVisits;  // Filtered ancestors
```

#### Internal State Structure

```csharp
private struct InternalNodeVisit
{
    public TNode Node;
    public NodePosition OriginalPosition;    // Position in inner (unfiltered) tree
    public NodePosition Position;            // Position in filtered tree
    public int VisitCount;
    public int CurrentChildIndex;            // How many children have been scheduled
}
```

#### Sibling Index Calculation

The parent's `CurrentChildIndex` tracks how many filtered children have been accepted. Each accepted child:
1. Gets the current `CurrentChildIndex` as its sibling index
2. Increments `CurrentChildIndex` for the next sibling

This ensures promoted children get correct sequential indices:

```
Tree: a(b(c, d), e)    Filter: remove b
When scheduling c: parent is a (b is on skipped stack), a.CurrentChildIndex = 0 → c gets index 0
When scheduling d: a.CurrentChildIndex = 1 → d gets index 1
When scheduling e: a.CurrentChildIndex = 2 → e gets index 2
Result: a(c[0], d[1], e[2])
```

#### Effective Depth Calculation

```csharp
effectiveDepth = _NodeVisits.Count + _SkippedNodeVisits.Count - 1
```

The depth in the filtered tree equals the total stack depth (accepted + filtered nodes) minus the sentinel.

#### The Sentinel Node

DFT initializes with a sentinel node (visit count = 1) that acts as a virtual root above all actual roots. This simplifies stack management—there's always a "parent" to reference.

#### Key Algorithm: OnScheduling

When a node is scheduled by the inner treenumerator:

1. **Pop deeper nodes**: If current depth ≤ previous depth, pop nodes from both stacks that are deeper
2. **Increment parent visit count**: When backtracking, the parent gets another visit
3. **Check predicate**: If node fails, push to `_SkippedNodeVisits` and return false (skip)
4. **Calculate position**: Use effective depth and parent's `CurrentChildIndex`
5. **Push to accepted stack**: Track this node for its descendants

#### Key Algorithm: OnVisiting

1. **Pop deeper nodes**: Remove nodes deeper than current visiting depth
2. **Skip if not accepted**: If node has no visit count, it was filtered during scheduling
3. **Increment visit count**: Track how many times this node has been visited

---

### WhereBreadthFirstTreenumerator

BFT Where is significantly more complex than DFT due to the deferred visiting pattern (all children at a level are scheduled before any are visited). It keeps a queue of accepted nodes plus an **incrementally-maintained skipped-ancestor prefix** — there is no per-node ancestor stack and no path buffers (an earlier design used both; they were removed when the operator was made linear-time — see "Skipped-ancestor counting" below):

```csharp
private readonly RefSemiDeque<NodeTraversalStatus> _NodePositionAndVisitCounts; // Queue of accepted nodes
private readonly List<int> _PredSkipPrefix;                                     // Live skipped-ancestor prefix
private readonly List<int> _RemovedSkippedChildCounts;                          // Consumer-SkipNode bookkeeping
```

#### Two distinct kinds of "skip"

The wrapper must keep these separate; conflating them is a classic bug source:

- **Predicate skip** (a node filtered out by the `Where` predicate): the node is *swallowed* and its children *promoted*. This **compresses effective depth** and renumbers siblings. Counted by `_PredSkipPrefix`.
- **Consumer SkipNode** (a `NodeTraversalStrategies.SkipNode` the *consumer* passes into `MoveNext`): the node is predicate-*accepted* but the consumer drops it. This is handled by `_RemovedSkippedChildCounts` and does **not** contribute to depth compression.

#### Skipped-ancestor counting (the O(1) prefix carry)

Effective depth is `innerDepth − (number of predicate-skipped proper ancestors)`. Counting those ancestors by scanning a stack of skipped nodes is **O(N) per accepted node → O(N²) overall** (during BFT scheduling, inner depth is monotonically non-decreasing, so a skipped stack never shrinks and grows to the full breadth). Instead the count is maintained incrementally:

- `_PredSkipPrefix[d]` = number of predicate-skipped nodes among inner depths `0..d` on the **current path**.
- On every scheduled node (skipped or accepted) set `_PredSkipPrefix[innerDepth] = _PredSkipPrefix[innerDepth-1] + (skipped ? 1 : 0)` — the live update.
- `skippedAncestorCount = _PredSkipPrefix[D-1]`; `immediateParentIsSkipped = _PredSkipPrefix[D-1] > _PredSkipPrefix[D-2]` — both O(1).
- When the queue **front advances** (a node is fully visited and dequeued), re-anchor the prefix at the new front in O(1): an accepted front contributes 0, so `prefix[frontDepth] == prefix[frontDepth-1] == frontInnerDepth − frontEffectiveDepth`. Deeper slots are rewritten live as the inner schedules the front's descendants contiguously downward, so no full-path restore is needed.

This makes BFT Where **O(N)**, matching DFT. (The visit *stream* was always Θ(N); only the per-step cost was quadratic.)

#### The Core Challenge: Deferred Parent Visits

When a child is promoted (its inner parent was predicate-filtered), the wrapper must emit an extra parent visit for the grandparent — but the inner BFT may *also* later emit its own parent visit for the filtered parent, risking a duplicate.

**Example:**

```
Tree: a(b(c))    Filter: remove b
Inner BFT sequence:   S a, V a, S b, V a, V b, S c, V b, V c
What we must output:  S a, V a, S c, V a, V c
```

When we schedule `c` (promoted to a child of `a`) we must emit `V a` *before* `V c`, then suppress the inner's later `V b`.

#### Parent-visit machinery (current fields)

```csharp
private bool _PendingParentVisit;                  // emit a manufactured parent visit next
private bool _ConsumeNextInnerParentVisit;         // swallow the inner's redundant parent visit
private NodeTraversalStrategies? _DeferredNodeTraversalStrategies; // strategy held while a manufactured visit is emitted
private int _ConsumerSkippedParentEffectiveDepth;  // tracks a consumer-SkipNode'd subtree (DeferredSchedulePending sentinel)
private bool _ConsumerSkippedChildAfterLastAccepted;
```

- **Scheduling a promoted child** (`immediateParentIsSkipped && effectiveDepth > 0`): set `_PendingParentVisit = true`.
- **Next `MoveNext`**: if a pending parent visit is owed (and the node wasn't consumer-skipped), emit the grandparent `V` first and stash the consumer strategy in `_DeferredNodeTraversalStrategies`.
- **Visiting**: `_ConsumeNextInnerParentVisit` suppresses the inner's now-redundant between-children parent visit for a filtered parent.

#### Sibling Index Calculation

`GetEffectivePosition` computes `effectiveDepth = innerDepth − PrefixSkippedAncestorCount(...)`, then the sibling index:

- effective root (`effectiveDepth == 0`) → `_SeenRootNodesCount`;
- child of a consumer-SkipNode'd parent (`_RemovedSkippedChildCounts[parentDepth] >= 0`) → that running count;
- otherwise → the queue front's `AcceptedChildCount` (the front is the node's nearest accepted ancestor / effective parent).

#### Predicate Inversion

Note that BFT inverts the predicate in the extension method:

```csharp
// In Treenumerable.Where.cs:
() => new WhereBreadthFirstTreenumerator<TNode>(
    source.GetBreadthFirstTreenumerator,
    nodeContext => !predicate(nodeContext),  // Inverted!
    NodeTraversalStrategies.SkipNode)
```

This is an implementation detail—the BFT implementation treats "true" as "skip this node."

---

### DFT vs BFT Where: Comparison

| Aspect | DFT | BFT |
|--------|-----|-----|
| **Data structures** | Two stacks (accepted + skipped) | Queue (accepted) + incremental skipped-ancestor prefix |
| **Sibling index source** | Parent's `CurrentChildIndex` counter | Queue front's `AcceptedChildCount` (or `_RemovedSkippedChildCounts`) |
| **Parent visit handling** | Natural—visits interleave with scheduling | Complex—must manufacture/suppress parent visits |
| **Predicate** | Used directly | Inverted |
| **Depth calculation** | Sum of both stack counts - 1 | Inner depth − `_PredSkipPrefix[D-1]` |
| **Time complexity** | O(N) | O(N) (was O(N²) before the prefix carry) |

#### Why BFT Is Harder

In DFT, when you schedule a child and then visit it, the parent visit naturally occurs when you "return" to the parent level. The schedule→visit→parent-visit sequence is implicit in the stack unwinding.

In BFT, scheduling and visiting are decoupled. When a child is promoted:
1. You schedule it (need to emit parent visit for grandparent)
2. Other siblings may be scheduled (more parent visits interleaved)
3. Much later, you visit the child
4. The inner BFT emits parent visits for the filtered parent (which you must skip/suppress)

This temporal separation requires explicitly manufacturing the grandparent visit at the right moment (`_PendingParentVisit`) and suppressing the inner treenumerator's now-redundant parent visit (`_ConsumeNextInnerParentVisit`).

---

### Implementing New Filter Operations

When implementing a new filtering operation similar to Where, consider:

1. **Do you need separate DFT/BFT implementations?** Usually yes, due to the different scheduling/visiting patterns.

2. **Track filtered ancestors**: You need to know the depth compression to calculate effective positions.

3. **Sibling index bookkeeping**: Either track via parent state (DFT) or via previous sibling position (BFT).

4. **Parent visit management**: DFT handles this naturally; BFT requires explicit tracking of pending/emitted visits.

5. **Test thoroughly**: Include tests for:
   - Filtering root nodes
   - Filtering leaf nodes
   - Filtering internal nodes (child promotion)
   - Multiple consecutive filtered ancestors
   - Mixed filtering patterns

## Code Conventions

### Naming (from .editorconfig)

- **Interfaces**: Prefix with `I` (e.g., `ITreenumerable<T>`)
- **Types**: PascalCase (e.g., `DepthFirstTreenumerator`)
- **Private fields**: Leading underscore + PascalCase (e.g., `_RootsEnumerator`)
- **Parameters**: camelCase (e.g., `nodeTraversalStrategies`)
- **Public members**: PascalCase (e.g., `Position`, `VisitCount`)

### Patterns

- Heavy use of `ref` for performance optimization
- All treenumerators implement `IDisposable`
- Generic type parameters follow pattern: `TValue` (node value), `TNode` (node type), `TChildEnumerator` (child enumeration)

## Testing

- **Framework**: MSTest with `[DynamicData]` for data-driven tests
- **Test data**: `TreeTraversalTestData.cs` contains comprehensive test trees
- **Utilities**: `NodeVisitDiffer` for comparing expected vs actual results
- **Serialization**: Text-based tree format for test fixtures

## Target Frameworks

Multi-targets .NET Framework 4.8, .NET 8.0, and .NET Standard 2.0.
