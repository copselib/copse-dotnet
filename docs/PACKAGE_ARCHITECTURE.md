# Package architecture (target state & migration ledger)

> **Status: DIRECTION DECIDED 2026-07-04; partially executed.** Records the package
> layering worked out during the dependency-cleanup discussion, what has already moved,
> and what remains. Companion to [TRAVERSAL_DIMENSION_SPLIT.md](TRAVERSAL_DIMENSION_SPLIT.md)
> (whose breaking wave absorbs the deferred namespace alignment) and
> [TREE_CAPABILITY_INTERFACES.md](TREE_CAPABILITY_INTERFACES.md) (whose "unresolved
> Copse.Core / Copse / Copse.Linq boundary" question this resolves).

## Governing principles

1. **`Copse.Core` is the root of the dependency tree.** Everything references it; it
   references nothing. Core holds the contracts, the vocabulary they speak
   (`NodePosition`, `NodeVisit`, the enums).
2. **`Copse.Primitives` holds building blocks with no traversal semantics**: the chunked
   ref-access collections (`RefSemiDeque`, `RefAppendOnlyList`), the lifted
   `Copse.Disposables` algebra, and the node-context value types (`NodeContext`,
   `NodeAndSiblingIndex`). References Core (for `NodePosition`); nothing tree-semantic
   is admitted.
3. **`Copse.Linq` must depend only on Core + Primitives.** The load-bearing principle
   (Jason, 2026-07-04): *there is no guarantee other concrete `ITreenumerable`
   implementations even have child enumerators.* `IChildEnumerator` is the ENGINE's SPI,
   not the tree model's — operators must be compilable against the abstract contract, so
   Linq works over any implementation (database cursor, REST adapter, ...) with the
   compiler enforcing it.
4. **The engine is a legitimate dependency for engine-things, under an honest name.**
   Breaking Linq→engine is achieved by relocating engine-things out of Linq, not by
   reimplementing traversal.

## Target graph

```
Copse.Core          contracts, enums, NodePosition, NodeVisit,
                    ITreenumerableBuffer*, TreenumeratorBase/Wrapper**      (root)
Copse.Primitives    RefSemiDeque, RefAppendOnlyList, Disposables,
                    NodeContext, NodeAndSiblingIndex                        (→ Core)
Copse[.Engine]*     DFS/BFS engine, IChildEnumerator + NodeAndSiblingIndex-
                    consumers, Treenumerable<,,>, PreorderTree*,
                    Memoize machinery + Memoize()/Materialize() extensions* (→ Core, Primitives)
Copse.Linq          operators only                                          (→ Core, Primitives)
Copse.Trees         concrete/sample trees (eventual PreorderTree home*)     (→ engine)
```

Entries marked * are target-state, not yet true — see the ledger. Entries marked ** are
target-state AND undecided — see "Open: the wrapper-base home."

## Open: the wrapper-base home

`TreenumeratorBase`/`TreenumeratorWrapper` are verified pure contract plumbing (they
reference only Core types — no engine, no `IChildEnumerator`), and they are the hinge of
the whole end state: Where, PruneBefore/After, TakeNodesUntil, RootfixScan, and the
StructuralMerge set-operation family extend them, so **wherever the bases live is a
package Linq must reference**. If they don't get a home below Linq, `Linq → Core +
Primitives` is unreachable. A Core move was executed and immediately reverted on
2026-07-04 — Jason isn't sure he wants implementation scaffolding in Core yet. Options:
Core (contracts + how-to-implement-them, BCL-style); the engine package (keeps a Linq →
engine edge for the wrapper operators, defeating principle 3); Primitives (violates the
no-traversal-semantics criterion); a dedicated tiny package (surface cost). Decide
before or with step 1 below.

## Migration ledger

**Done (2026-07-04):**

- `Copse.Primitives` split out of `Copse` (collections, disposables, node-context
  types). Copse and Copse.Linq reference it explicitly.
- All moves kept namespaces intact (namespace ≠ assembly): zero churn in consuming code.

**Done (2026-07-04, discovered rather than built):**

- The enumerable adapters (`ToDegenerateTree`, `ToTrivialForest`) were **already
  engine-free** — `EnumerableAsTree*`/`EnumerableAsForest*` are hand-rolled
  `ITreenumerator` implementations over `RefSemiDeque` (Core + Primitives only). The
  engine-riding `EnumerableTreenumerable` + `SharedEnumerableChildEnumerator` was dead
  code (zero references) and has been deleted. Linq's entire remaining engine +
  `IChildEnumerator` surface is now exactly the memoize machinery (3 files).

**Remaining, in rough order:**

1. **De-engine the memoize replays via LAYOUT STREAMERS** (supersedes the earlier
   relocation plan — decided 2026-07-04 late). Replays stop riding the generic engine;
   instead, four storage-specialized treenumerators synthesize visit streams directly
   from the capture layouts:
   - *Native* DFT-over-preorder-store and BFT-over-level-order-store: pure playback
     (linear scan + `RefSemiDeque` path / front cursor), recreate nothing, likely
     FASTER than engine-over-child-enumerator (the replay-side twin of the bulk-Consume
     win); laziness preserved via the existing `Ensure*` pulls.
   - *Cross-order* DFT-over-level-order and BFT-over-preorder: honest caveat (Jason) —
     these re-implement DFS/BFS over index accessors, so framed as memoize internals
     they'd be a private engine fork. The reframe that legitimizes them: they are
     **layout streamers over stores**, exactly the traversal layer the streaming
     serializer needs (see TRAVERSAL_DIMENSION_SPLIT.md — same four combinations over
     text). Memo buffers, PreorderTree, and lazily-parsed serialized text are all
     stores with the same two access shapes; write the streamers once, parameterized
     over store access.
   - Payoffs: memoize machinery AND operators stay in Linq (no Ix-parity deviation, no
     public API relocation); the Linq→engine edge breaks anyway; PreorderTree
     eventually collapses into "completed store + streamers"; the engine's remaining
     jurisdiction is crisp — arbitrary live trees via the child-enumerator protocol
     (sources, not stores).
   - Guards: MemoizeTests' strategy matrix + replay-vs-source oracle diff each streamer
     against engine behavior before the engine path dies; the Memoize benchmarks
     (replay-over-capture, cross-order, laziness) referee perf.
   - Staging: native DFT first (hottest path), benchmark A/B, then native BFT, then the
     cross pair. The dependency breaks only when all four land; the engine serves
     un-rewritten combos in the interim.
   - `ITreenumerableBuffer` promotion to Core: still desirable, now decoupled — do it
     with the namespace wave.
2. **PreorderTree exits Linq** via already-planned work: `LeaffixScan` resolves with the
   dimension split; `Invert` resolves with the mirror-view rework. ⚠ The Invert-as-view
   design must target whatever abstraction the (engine-side) memoize machinery exposes.
   Afterward `PreorderTree` can leave the engine for `Copse.Trees`, making the engine
   package pure machinery.
3. **Rename the engine package and settle the `Copse` id.** OPEN: `Copse.Engine` vs
   `Copse.Traversal`; and metapackage (a codeless `Copse` package referencing
   Core + Primitives + Engine + Linq, preserving the flagship install name) vs retiring
   the id. Recommendation on record: rename + metapackage.
4. **Namespace alignment wave** (deferred, batched with the dimension split's breaking
   era): align `namespace Copse` types to their owning assemblies; revisit
   `IChildEnumerator`(+`NodeAndSiblingIndex`) promotion to Core if the capability
   lattice wants it; possible `NodeVisit`-composes-`NodeContext` cleanup (impossible
   under the old layering, trivial now).

## Housekeeping

- `publish-nuget.yml` must add `Copse.Primitives` (and the renamed engine + metapackage
  when they exist) to its publish set.
- `InternalsVisibleTo`: `Copse.Primitives` grants `Copse.Tests` (for `Snapshot()`
  debug/test accessors); re-audit grants after the memoize relocation.
