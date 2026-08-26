# Release notes (draft)

> Working draft, accumulating since **v0.3.0-alpha.2** (2026-07-09). Entries are added when a
> breaking change lands; the draft is edited down into the real notes at the next tag. Commit
> SHAs are for pre-tag traceability and drop out of the published notes.

## Unreleased — breaking changes since 0.3.0-alpha.2

### Parameter order and names

- **Fold-family parameter convention: accumulator first, boundary selector last** (`f23e89e`).
  `LeaffixScan` / `LeaffixAggregate` overloads changed from `(leafNodeSelector, accumulator)` to
  `(accumulator, leafNodeSelector)`; `LeaffixAggregate`'s parameter renamed
  `leafSelector` → `leafNodeSelector`; the `RootfixScan` / `RootfixAggregate` selector overloads
  flipped to `(accumulator, rootNodeSelector)` to match the `(accumulator, seed)` anchor.
  *Migration: swap the two arguments at fold call sites that used the selector overloads.*

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

### Moved types

- **Store SPIs and completed array stores moved per-color** (`7328c77`): sync store types moved
  from `Copse.Primitives` to the `Copse` package under namespace `Copse.Stores`; async store
  SPIs moved from namespace `Copse.Async` to `Copse.Async.Stores`. `Copse.Primitives` is now
  collections + disposables only.
  *Migration: update `using Copse` → `using Copse.Stores` (sync) / `using Copse.Async` →
  `using Copse.Async.Stores` (async) at store-SPI call sites.*

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
