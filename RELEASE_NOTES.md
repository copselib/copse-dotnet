# Release notes (draft)

> Working draft, accumulating since **v0.3.0-alpha.2** (2026-07-09). Entries are added when a
> breaking change lands; the draft is edited down into the real notes at the next tag. Commit
> SHAs are for pre-tag traceability and drop out of the published notes.

## Unreleased — breaking changes since 0.3.0-alpha.2

### Parameter order and names

- **Operator lambdas take the node, not a `NodeContext`** (the composition-era arity split).
  Every public operator that took `Func<NodeContext<TNode>, …>` now takes the value form
  `Func<TNode, …>` and, where the position matters, a positional overload
  `Func<TNode, NodePosition, …>`; `NodeContext` no longer appears in public operator
  signatures. *Migration: change `ctx => f(ctx.Node)` to `node => f(node)`, and
  `ctx => f(ctx.Node, ctx.Position)` to `(node, position) => f(node, position)`.*
- **The child-pull protocol speaks Option**: `IChildEnumerator<THandle>.MoveNext()` returns
  `Option<HandleAndSiblingIndex<THandle>>`; the `ChildResult<T>` carrier is gone, and the
  protocol's type parameter is the HANDLE (the navigable identity), not the surfaced node.
  *Migration: return `new Option<HandleAndSiblingIndex<THandle>>(child)` for a child and
  `default` past the last.*
- **The leaffix fold family reduces pairwise**: `LeaffixAggregate`/`LeaffixScan` no longer
  hand the node a completed `ChildAccumulations<T>` collection — the `ChildAccumulations<T>`
  type is gone. The edge accumulator reduces child accumulations pairwise
  (`(accumulate, childAccumulate) => …`) and the node accumulator folds the node in once.
  *Migration: split the old collection-consuming accumulator into the pairwise edge
  accumulator plus the node accumulator.*
- **Async `MaterializeAsync` → `Materialize`, and it no longer awaits** — capture
  construction is deferred to the first pull, so the async surface drops the suffix (nothing
  is awaited at the call). The declared-layout overload's parameter retyped
  `TreeTraversalStrategy` → `BufferLayout` (the layout is the deliverable, so the parameter
  speaks storage vocabulary). *Migration: drop the `await` and the `Async` suffix; pass
  `BufferLayout.Preorder`/`LevelOrder` where a strategy was passed.*
- **Fold-family parameter convention: accumulator first, boundary selector last** (`f23e89e`).
  `LeaffixScan` / `LeaffixAggregate` overloads changed from `(leafNodeSelector, accumulator)` to
  `(accumulator, leafNodeSelector)`; `LeaffixAggregate`'s parameter renamed
  `leafSelector` → `leafNodeSelector`; the `RootfixScan` / `RootfixAggregate` selector overloads
  flipped to `(accumulator, rootNodeSelector)` to match the `(accumulator, seed)` anchor.
  *Migration: swap the two arguments at fold call sites that used the selector overloads.*

- **Parameter names align to the LINQ register**: the serializer's extension receivers
  rename `treenumerable` → `source`, and `ToFormattedLines`/`ToFormattedString` rename
  `stringFormatter` → `selector`. Affects named-argument call sites only.
- **Operator machinery is no longer public surface**: the concrete engine, store, and stream
  treenumerators and the delegating treenumerables are `internal` — traversals are acquired
  through the treenumerable doors, which are unchanged. The store SPIs, the flat wrappers,
  and the array stores stay public (the provider extension points).

### Renamed types and members

- **Memo buffer family** (`9f4e80e` era):
  `ILazyTreenumerableBuffer` → `IMemoizeTreenumerableBuffer` (async twin likewise);
  `CompletedTreenumerableBuffer` → `TreenumerableBuffer`; the four `LazyBuilt*Store` types →
  `Lazy*Store`.
- **`Consume()` / `ConsumeAsync()` members → `Complete()` / `CompleteAsync()`** on the memoize
  buffer interface. `Consume` remains only as the drain *extension*; the member finishes
  building. `Complete()` is now parameterless (the traversal-strategy parameter is gone —
  pinning happens at treenumerator acquisition), and `GetBufferedCount()` is parameterless too.
- **`MaterializeWalkable` → `Materialize`** (`5dba85b`): the walkable capture is the ordinary
  `Materialize(BufferLayout.Preorder)`.
- **Select-composition interfaces** (`2acd8e2` era, `a4420f16`): the internal-era
  `ISelectComposableTreenumerable` surface was replaced by the public composition interfaces
  (`ISelectTreenumerable`, `ISelectWhereTreenumerable`, …), and the buffer flavor renamed
  `ISelectComposableTreenumerableBuffer` → `ISelectTreenumerableBuffer` (async twin likewise).
- **`ToFormattedLines`**: sync now returns `IReadOnlyList<string>` (was deferred); async renamed
  `ToFormattedLinesAsync` returning `ValueTask<IReadOnlyList<string>>`.
- **`NodeAndSiblingIndex<TNode>` → `HandleAndSiblingIndex<THandle>`**, its field `Node` →
  `Handle`. The type always carried the navigable identity — the child pull's yield and the
  topology probes' answer — and now says so. *Migration: rename the type at implementer sites
  and read `.Handle` instead of `.Node`.*
- **`HandleAndValue<THandle, TValue>` → `HandleAndNode<THandle, TNode>`**, its field `Value` →
  `Node`. *Migration: rename the type and read `.Node`; `GetHandlesWithValues` keeps its
  name.*
- **`NodeContext<TNode>` → `NodeAndPosition<TNode>`** — the struct is exactly a node paired
  with its position (its own doc sentence), and every other pair in the vocabulary already
  says what it holds (`HandleAndNode`, `HandleAndSiblingIndex`); "context" was the one
  name describing a role instead of a shape. The treenumerator extension
  `ToNodeContext` follows as `ToNodeAndPosition`. Members unchanged (`Node`, `Position`).
  *Migration: rename the type at child-enumerator factories and positional callbacks.*
- **`NodeVisit<TNode>.Mode` is now derived** from `VisitCount` (scheduling at 0, visiting
  from 1), and the constructor drops its mode parameter. *Migration: remove the first
  argument at construction sites; reads of `.Mode` are unchanged.*
- **`SelectWhereResult<TNode>.Value` → `Node`** and **`PreorderRead<TNode>.Value` → `Node`**
  (constructor parameters likewise) — tree-domain types name the element for what it is;
  generic containers (`Option`, the walker step result) keep their own `Value` idiom. The
  completed array stores' constructors rename their `values` parameter to `nodes`.
- **`NodeTraversalStrategies` says prune where it prunes** — the enum used "skip" two ways:
  `SkipNode` literally skips (visits suppressed, descendants traversed), while the other
  flags remove structure outright. The members now say so (flag values unchanged):
  `SkipDescendants` → `PruneDescendants`, `SkipNodeAndDescendants` → `PruneSubtree`,
  `SkipSiblings` → `PruneSiblings`, `SkipNodeAndSiblings` → `SkipNodeAndPruneSiblings`,
  `SkipDescendantsAndSiblings` → `PruneDescendantsAndSiblings`, `SkipAll` →
  `PruneSubtreeAndSiblings`. `TraverseAll` and `SkipNode` are unchanged, as are the
  LINQ-idiom operators (`Where`, `SkipTrees`, `SkipLastTrees`). *Migration: rename the
  members at call sites; flag arithmetic and semantics are identical.*
- **The prune operators say what they prune**: `PruneBefore` → `PruneSubtreesWhere`
  (removes each matched node with its whole subtree — the exact dual of
  `TakeSubtreesWhere`) and `PruneAfter` → `PruneDescendantsWhere` (keeps the matched node,
  removes everything below). The Before/After suffixes only existed because the verb's
  object was implicit; each operator is now named for its strategy plus the established
  predicate suffix. The walkable lens `PruneAfter` follows to `PruneDescendantsWhere`.
  *Migration: rename at call sites; predicates and semantics are identical.* New in the
  same family: **`PruneSiblingsWhere`** — the matched node stays (visits, descendants, and
  position untouched) and its later siblings are removed; the strategy family's completion,
  and the one prune that never moves a surviving label.
- **The node accessors say node**: `ITreeTopology.GetValue` → `GetNode` (async
  `GetValueAsync` → `GetNodeAsync`), the walker's `GetValue`/`TryGetValue` →
  `GetNode`/`TryGetNode` (async likewise), the store SPIs' `GetValue(index)` →
  `GetNode(index)`, and `GetHandlesWithValues` → `GetHandlesWithNodes` (aligning with its
  `HandleAndNode` element type). Every layer of the tree surface speaks tree — the store
  SPIs already said subtree/root/child everywhere else. `Option<TValue>.Value`/`TryGetValue`
  and the walker step result's `Value`/`TryGetValue` are unchanged: those are containers
  (the result holds a walker, not a node). *Migration: rename at call sites; signatures are
  otherwise identical.*

### Moved types

- **Three packages dissolve into the stack** (the one-directional color rule: async now
  depends on sync, never the reverse). `Copse.Vocabulary` merges into `Copse.Core` (the
  vocabulary, `Option`, and `HandleAndSiblingIndex` — namespace `Copse.Core`;
  `HandleAndNode` moves to the `Copse` package, namespace `Copse`); `Copse.Traversal`
  merges into `Copse`; `Copse.Linq.Traversal` merges into `Copse.Linq`. The retired NuGet
  ids will be deprecated. *Migration: remove any direct references to the three retired
  packages — their types arrive through `Copse.Core`/`Copse`/`Copse.Linq`; `Option` and
  `HandleAndSiblingIndex` move from namespace `Copse` to `Copse.Core`.*
- **Every concrete treenumerable is internal; every entry is a door** — `Tree.Create` gains
  the hierarchical family's overloads (`(childEnumeratorFactory, roots)` and the
  handle form `(childEnumeratorFactory, handleToNodeMap, roots)`), and the flat family
  gains `Tree.FromPreorderStore` / `FromLevelOrderStore` / `FromPreorderStream` /
  `FromLevelOrderStream` (`AsyncTree` twins likewise). `HierarchicalTreenumerable` and the
  four flat wrappers are `internal`. *Migration: replace `new XTreenumerable(...)` with the
  matching `Tree.*` door; the store/stream SPIs and the sample-tree classes are
  unchanged.*
- **The hierarchical family says hierarchical**: the engine class `Treenumerable` →
  `HierarchicalTreenumerable` (both arities; async twin `AsyncHierarchicalTreenumerable`),
  completing the family triple beside `PreorderTreenumerable` and
  `LevelOrderTreenumerable` — each concrete treenumerable is named for the shape of the
  data it decodes, and the bare name no longer collides with the `Copse.Linq.Treenumerable`
  operator class. *Migration: rename at construction sites; `Tree.Create`/`Tree.Defer`
  call sites are unchanged.*
- **The chunked collections move to namespace `Copse.Collections`** — `RefSemiDeque<T>` and
  `RefAppendOnlyList<T>` leave the bare `Copse` namespace (which is now purely tree-domain),
  mirroring `System.Collections.Generic`'s shape; the `Copse.Primitives` package is
  unchanged. *Migration: `using Copse.Collections;` where the collections are named.*
- **The walker tier moves to namespace `Copse.Core`** — `TreeWalker`, `TreeWalkerResult`,
  `ITreeTopology`, `IWalkableTreenumerable` (and their `Async` twins) leave the bare
  `Copse` namespace: they are core contracts, and their namespace now agrees with the
  package that ships them. *Migration: add `using Copse.Core;` where these types are
  named.*
- **`NodeVisit<TNode>` moves to the `Copse.Linq` package, namespace `Copse.Linq`** — no
  core contract names it; it is the traversal-projection row the operator tier deals in
  (`GetTraversal`/`Do` and the scan machinery), so it lives beside that algebra.
  *Migration: add `using Copse.Linq;` where `NodeVisit` is named.*
- **`TreeTopology` moves to the `Copse.Linq` package, namespace `Copse.Linq`** — the
  `TreeTopology.Lazy`/`AsyncTreeTopology.Lazy` factories (and their internal `LazyTopology`
  engines) leave the engine package for the operator package, where their only consumers —
  the walkable view operators — live; the `Copse.Topologies` namespace is retired.
  *Migration: add `using Copse.Linq;` where `TreeTopology` is named.*
- **The async namespaces drop their `Async` segment** — async types now live in the same
  namespaces as their sync twins, exactly as `IAsyncEnumerable<T>` sits in
  `System.Collections.Generic` and `AsyncEnumerable` in `System.Linq`: `Copse.Core.Async` →
  `Copse.Core`, `Copse.Async` → `Copse`, `Copse.Async.Stores` → `Copse.Stores` (and so on
  for `Treenumerables`/`Treenumerators`); `Copse.Linq.Async` → `Copse.Linq`. Assemblies and
  packages are unchanged — only namespaces merge; every async type name carries its `Async`
  prefix, so nothing collides. The async read struct renames `PreorderRead` →
  `AsyncPreorderRead` (it was the one unprefixed async type). *Migration: delete the
  `.Async` namespace segment from `using` directives; the sync using set now serves both
  colors.*
- **Store SPIs and completed array stores moved per-color** (`7328c77`): sync store types moved
  from `Copse.Primitives` to the `Copse` package under namespace `Copse.Stores`; async store
  SPIs now share that namespace (see the entry above). `Copse.Primitives` is now
  collections + disposables only.
  *Migration: update `using Copse` → `using Copse.Stores` at store-SPI call sites, both
  colors.*

### Changed behavior

- **Every serializer `Deserialize…` overload now has Defer semantics** (`168ad1c`). The string
  tier previously parsed once into a hidden shared store, so re-enumerating a deserialized tree
  replayed without re-parsing and shared node instances across enumerations; the
  `Func<TextReader>` overloads already deferred. Now every overload constructs a fresh store per
  treenumerator acquisition: the value map runs per traversal and instances are not shared
  across enumerations. *Migration: for parse-once replay, add an explicit `.Materialize()` or
  `.Memoize()` after deserializing.*
- **`TakeSubtreesWhere` streams instead of capturing** (composite and breadth-first-narrow
  overloads; through 2026-08-17 they returned `ITreenumerableBuffer` /
  `IAsyncTreenumerableBuffer`). The predicate now re-fires per drain under the re-enumeration
  contract. *Migration: consumers who relied on the capture add `.Materialize()`.*
- **The engine's handle-to-node map runs once per node** (at scheduling) instead of once per
  visit event. Observable only for impure maps.
- **`buffer.Select(...)` returns a buffer** — on an `ITreenumerableBuffer` receiver, `Select`
  now returns `ITreenumerableBuffer` (a deferred capture of projected values) instead of a lazy
  `ITreenumerable` stream. The buffer-receiver overload wins overload resolution for existing
  callers. The positional `Select` overloads still return the lazy stream.

### Removed

- **Tree tokenizers** (`To*TreeTokenizer`) left the packages: the sync operators moved to the
  unpackaged `Copse.Linq.Experimental` project; the async operators were removed.
- **`NodePosition`'s `+` and `-` operators** — component-wise arithmetic no caller ever used.
- **`Option<TValue>.Map`** — its one internal consumer was optimized away and no caller
  remained.
