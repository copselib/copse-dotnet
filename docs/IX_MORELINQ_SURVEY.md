# Ix.NET / MoreLINQ Operator Survey (tree-analog candidates)

> Surveyed 2026-07-04 against `Copse.Linq` (45 operators) + `Copse.Linq.Experimental`
> (Collapse, ExpandNode, Graft, InOrderTraversal, RepeatTrees). Sources: MoreLINQ README
> operator list (~110 operators), System.Interactive operator sources (~40 operators).
> The classification lens is the tree monad plus the traversal-dimension cost analysis
> from [TRAVERSAL_DIMENSION_SPLIT.md](TRAVERSAL_DIMENSION_SPLIT.md) — for each candidate,
> what is the emission-vs-arrival gap per dimension?

## Headline findings

1. **`SelectMany` — the monad's bind — is missing.** CLAUDE.md's overview advertises it,
   but no `Treenumerable.SelectMany` exists; only test code uses (IEnumerable) SelectMany.
   For a library whose design philosophy is "ITreenumerable is a tree monad," the bind
   operator is the load-bearing absence. `ExpandNode` (Experimental) is the precursor:
   `ExpandNode(source, predicate, nodeContext => tree)` is bind restricted to selected
   nodes. Promoting a full-fidelity `SelectMany` out of Experimental is the single
   highest-leverage item in this survey — it is also the operator that would let the
   monad laws be property-tested.
2. **Construction is the thin story; transformation is the rich one.** Both libraries'
   most tree-relevant operators are *factories* (Ix `Expand`/`Generate`/`Defer`/`Using`,
   MoreLINQ `Unfold`/`Generate`/`Sequence`/`Return`/`From`). Copse today requires
   implementing child-enumerator machinery to create a new tree source; a first-class
   **`Unfold(seeds, childSelector)`** (the tree anamorphism — exactly what Ix `Expand`
   does before flattening) would democratize that. Note `Copse.Trees`' generators
   (Collatz etc.) are hand-rolled unfolds already.
3. **Where the catalogs overlap, Copse is already richer.** Ix `Memoize` is
   single-dimension and hole-free-by-eagerness; Copse's is two-dimensional and lazy.
   `Scan`/`PreScan`/`ScanRight` collapse into Rootfix/LeaffixScan; `Aggregate`/
   `AggregateRight` into Rootfix/LeaffixAggregate. MoreLINQ's `TraverseDepthFirst`/
   `TraverseBreadthFirst` and Ix's `Expand` — their only "tree" operators — flatten trees
   *into sequences*; the whole of Copse is what they are missing.
4. **The no-node-equality principle cleanly excludes a whole class** (see below), which
   is confirmation the principle is real, not incidental.

## Admission policy

The governing rule for whether something deserves a *tree* operator (Jason, 2026-07-04):

> If the operation is achievable by projecting to a sequence (`PreorderTraversal` /
> `LevelOrderTraversal` / `To*TreeTokenizer`) and applying sequence LINQ, favor that —
> with a handful of exceptions.

A tree operator earns its place only when at least one of these holds:

- **The result is a tree** (shape transforms: `Where`, `Select`, `SelectMany`, `Invert`).
- **The computation consumes structure** the flattened view destroys (Rootfix/Leaffix
  directionality, positions, sibling groups).
- **Laziness/pruning interplay matters** (`SkipDescendants` has no sequence analog).

Corollary: value-level queries (`Distinct`, `MinBy`, `CountBy`, conversions) are
flatten-then-LINQ recipes, even when an explicit `IEqualityComparer` would keep them
principle-compatible. And a *tree-shaped* `Distinct` is excluded for a second, harder
reason than equality: which duplicate survives is traversal-order-dependent, so DFT and
BFT would produce **different trees from the same source** — breaking the
both-dimensions-see-one-shape invariant that Memoize and the dimension split rest on.
Any candidate whose semantics depend on visit order (stateful predicates included) fails
this test.

## Already covered (Copse equal or richer)

| Ix / MoreLINQ | Copse |
| --- | --- |
| Memoize (Ix, MoreLINQ) | `Memoize` (two-dimensional, lazy) + `Materialize` |
| Consume | `Consume` (dimension-aware) |
| Scan / PreScan / ScanRight | `RootfixScan` / `LeaffixScan` |
| Aggregate / AggregateRight / Fold | `RootfixAggregate` / `LeaffixAggregate` |
| Do / Pipe / Trace | `Do` |
| Hide | `Hide` |
| Batch / Buffer | `GetLevels` |
| Index | native `NodePosition`, `WithLevelIndex` |
| Pairwise / Lag(1) | `WithParent` (the tree's natural "previous") |
| TakeLast / SkipLast | `TakeLastTrees` / `SkipLastTrees` |
| TakeUntil / SkipUntil | `TakeNodesUntil` / `TakeNodesWhile` (see gap: no `SkipNodes*`) |
| TraverseDepthFirst / TraverseBreadthFirst / Expand-as-flatten | `ToDepthFirstTreeTokenizer` / `ToBreadthFirstTreeTokenizer` |
| ToDelimitedString | `ToFormattedString`, TreeSerializer |
| IsEmpty / ForEach | `AnyNodes` / `Consume`+`Do` |
| Repeat | `RepeatTrees` (Experimental) |
| Insert / Backsert / Move | `Graft` (Experimental) |
| MinBy/MaxBy/Maxima/Minima, ToDictionary/ToLookup/etc. | via `To*TreeTokenizer` + LINQ |

## Excluded by principle (node equality)

`Distinct`, `DistinctUntilChanged`, `Duplicates`, `StartsWith`/`EndsWith` (structural
prefix tests), `RunLengthEncode`. All require comparing node values — the library never
does. Consumer-supplied *key* selectors would technically preserve the principle
(`DistinctBy`, `CountBy`, `GroupAdjacent`), but shape-changing dedup is order-dependent
and low-value on trees; deferred indefinitely. Structural (shape-only) analogues of
`StartsWith` need no equality and could exist, but no demand yet.

## Candidates (by status, as of the 2026-07-04 review)

### Implemented

- **`Defer(factory)`** (Ix): lazy tree factory, invoked per treenumerator acquisition.
  Impure factories inherit the standard impure-source contract (Memoize pins a shape).
- **`Using(resourceFactory, tree)`** (Ix): resource-owning tree factory. The ownership
  rule — each treenumerator acquisition acquires its own resource, released on that
  treenumerator's Dispose (or on construction failure) — is the serializer
  reader-factory idiom piloted as a first-class operator; `DisposeActionTreenumerator`
  (Ix's `Finally` in wrapper form) is the reusable lifecycle hook. Memoizing a Using
  tree releases the resource the moment the capture completes, with no special-casing.

### Ready when wanted (small, self-contained)

- **`Unfold(seeds, childSelector)`** (Ix `Expand`, MoreLINQ `Unfold`/`Generate`; the
  anamorphism). Cheap to implement (the child selector *is* a child enumerator), native
  in both dimensions, infinite-tree friendly, and the capability doc's
  `IChildVisitableTree` falls out of it for free — the ideal incremental-memoize source.
  `Return` = `Unfold` of one childless seed.
- **`Concat`** (forest concatenation; the forest monoid, `Empty` its identity). `Union`
  is structural overlay; nothing appends root sequences. DFT drains sources in order;
  BFT holds all sources' frontiers per level — windowed both ways.
- **Symmetry/utility fills**: `SkipNodesWhile`/`SkipNodesUntil` (Take* exist;
  path-prefix-stateful predicates are order-free, hence dimension-safe),
  `FallbackIfEmpty`.

### Designed, implementation deferred (real work)

- **`SelectMany`** (bind; MoreLINQ `Flatten` is the join). **Semantics decided — see
  [SELECTMANY_DESIGN.md](SELECTMANY_DESIGN.md)**: root-graft substitution over
  forest-valued selectors, expansion-children-first, k = 0 promotes (making `Where` the
  bind restricted to {Return, Empty} by definition). Where-class effort; the payoff is
  the monad story becoming true, law-tested.

### Split-dependent (natural home is a single-dimension interface)

- **`OrderSiblingsBy(key)`**: stable per-sibling-group sort, consumer key. Inherits
  mirror's cost analysis exactly (windowed per group under BFT, total under DFT) — a
  natural `IBreadthFirstTreenumerable`-only operator, with `Invert` documented as the
  family's reverse case and the buffer-aware view fast path.
- **`WithNodeFlags`** (TagFirstLast analog: first/last-sibling, leaf, root). Cost
  profile is *inverted* from the library's usual pain: `IsLastSibling` is one-event
  lookahead in BFT but a whole-subtree lag in DFT (the next sibling only schedules after
  this child's entire subtree); `IsLeaf` is one-event in both. BFT-flavored, or free
  over memoized sources.
- **`ExpandDeep`** (leaf-graft bind — see SELECTMANY_DESIGN.md): lawful, DFT-streams,
  BFT-hostile; `IDepthFirstTreenumerable`-typed.

### Parked

- **Feed combinators** (Ix `Catch`/`Retry`/`Finally`/`OnErrorResumeNext`): error
  handling around sources; `Retry` only makes sense over factories (re-acquire and
  restart), so these build on `Defer`/`Using`. Revisit with the serializer's stream
  feeds. `DisposeActionTreenumerator` already provides the Finally mechanism.

## Recipes, not operators

`FillForward`-along-paths ≈ `RootfixScan` with carry; `CountDown` ≈ `LeaffixScan`
subtree-size; `TakeEvery(k)`-by-depth ≈ `Where` on `Depth % k` (child promotion does the
contraction); `Choose` ≈ `Select`+`Where`. Document as examples someday; do not add.

Demoted from candidacy during the 2026-07-04 review:

- **`ZipTrees`**: already exists — `Intersection` returns `MergeNode<TLeft, TRight>`
  (both sides paired), so ZipShortest ≈ `a.Intersection(b).Select(m => f(m.Left,
  m.Right))` and ZipLongest ≈ the same over `Union`'s optional sides. The `MergeNode`
  spelling is arguably clearer than a dedicated operator.
- **`Split(predicate)`** (pair-returning bisection): fights the API (two lazy views over
  one source). The actual gap, if demand appears, is the complement of pruning — a
  `Subtrees(predicate)` forest of matching subtrees (outermost-only) — after which Split
  ≈ `(t.PruneBefore(p), t.Subtrees(p))` with the caller deciding whether to memoize for
  the double pass.

## Not applicable

Combinatorics and randomness (Cartesian, Subsets, Permutations, Shuffle, Random*),
keyed joins (Full/Left/RightJoin, FullGroupJoin), ordering over the whole sequence
(PartialSort, OrderedMerge, SortedMerge), Transpose, Pad/PadStart, imperative
combinators (Case/If/While/DoWhile/For), Throw, count assertions (Assert*, AtLeast,
AtMost, Exactly, CountBetween, CompareCount), Acquire, TrySingle, Publish/Share
(single-pass multi-consumer sharing across two dimensions is a research problem;
`Memoize` covers the practical need), Await/Merge (async — an `IAsyncTreenumerable`
is a separate frontier, noted and not pursued).

---

*Backlog item closed 2026-07-04 (tracked since the ITreenumerableBuffer/Consume work,
which deliberately followed the Ix lineage). Next actions when desired: promote
`SelectMany`, add `Unfold`; both are also the two operators that would most exercise
the monad laws in tests.*
