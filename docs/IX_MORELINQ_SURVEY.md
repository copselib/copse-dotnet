# Ix.NET / MoreLINQ Operator Survey (tree-analog candidates)

> Surveyed 2026-07-04 against `Copse.Linq` (45 operators) + `Copse.Linq.Experimental`
> (then: Collapse, ExpandNodes, Graft, InOrderTraversal, RepeatTrees; the 2026-07-05
> stub purge kept only ExpandNodes and Graft). Sources: MoreLINQ README operator list
> (~110 operators), System.Interactive operator sources (~40 operators). The
> classification lens is the tree monad plus the traversal-dimension cost analysis
> from [TRAVERSAL_DIMENSION_SPLIT.md](TRAVERSAL_DIMENSION_SPLIT.md) — for each candidate,
> what is the emission-vs-arrival gap per dimension?
>
> **Refreshed 2026-07-06**, after the traversal-dimension split / flat family /
> header-free serializer merge (611df11): the split-dependent candidates below are now
> typeable as designed, and the `With*` operators the coverage table leaned on have
> been deleted — rows reconciled in place, each marked with its date.

## Headline findings

1. **`SelectMany` — the monad's bind — is missing.** CLAUDE.md's overview advertises it,
   but no `Treenumerable.SelectMany` exists; only test code uses (IEnumerable) SelectMany.
   For a library whose design philosophy is "ITreenumerable is a tree monad," the bind
   operator is the load-bearing absence. `ExpandNodes` (Experimental) is the precursor:
   `ExpandNodes(source, predicate, nodeContext => tree)` is bind restricted to selected
   nodes. Promoting a full-fidelity `SelectMany` out of Experimental is the single
   highest-leverage item in this survey — it is also the operator that would let the
   monad laws be property-tested.
2. **Construction is the thin story; transformation is the rich one.** Both libraries'
   most tree-relevant operators are *factories* (Ix `Expand`/`Generate`/`Defer`/`Using`,
   MoreLINQ `Unfold`/`Generate`/`Sequence`/`Return`/`From`). Copse today requires
   implementing child-enumerator machinery to create a new tree source (or, since the
   flat family landed, a preorder/level-order store — still machinery); a first-class
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
| Index | native `NodePosition` on every visit (`WithLevelIndex` deleted 2026-07-05; positions were always the real coverage) |
| TakeLast / SkipLast | `TakeLastTrees` / `SkipLastTrees` |
| TakeUntil / SkipUntil | `TakeNodesUntil` / `TakeNodesWhile` (see gap: no `SkipNodes*`) |
| TraverseDepthFirst / TraverseBreadthFirst / Expand-as-flatten | `ToDepthFirstTreeTokenizer` / `ToBreadthFirstTreeTokenizer` |
| ToDelimitedString | `ToFormattedString`, TreeSerializer |
| IsEmpty / ForEach | `AnyNodes` / `Consume`+`Do` |
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

  *Both since promoted out of Copse.Linq onto the `Copse.Treenumerables.Tree` facade
  (factories are sources, not operators; 2026-07-05), joined by `Tree.Empty` and the
  per-dimension variants (`DeferDepthFirst`/`DeferBreadthFirst`,
  `UsingDepthFirst`/`UsingBreadthFirst`) so narrow pipelines can start narrow.*

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

### Unblocked by the split (2026-07-06: the single-dimension homes now exist; awaiting demand)

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
  restart), so these build on `Defer`/`Using`. The serializer's stream feeds now exist
  (the reader-factory tier, 2026-07-05), so these are concrete whenever demand appears —
  still parked until it does. `DisposeActionTreenumerator` already provides the Finally
  mechanism.

## Recipes, not operators

`FillForward`-along-paths ≈ `RootfixScan` with carry; `CountDown` ≈ `LeaffixScan`
subtree-size; `TakeEvery(k)`-by-depth ≈ `Where` on `Depth % k` (child promotion does the
contraction); `Choose` ≈ `Select`+`Where`; `Pairwise`/`Lag(1)`-along-paths ≈
`RootfixScan` with a one-generation carry (`WithParent` was exactly this as an operator
— deleted 2026-07-05 as not pulling its weight). Document as examples someday; do not
add.

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

Demoted after the fact (2026-07-05 stub purge):

- **`RepeatTrees`** (was Experimental, the `Repeat` coverage): deleted as purposeless.
  Ix `Repeat` becomes a one-liner once `Concat` lands (n-fold forest concatenation);
  no standalone operator wanted. Its treenumerator file survived the purge as an
  orphan (zero references) — delete on sight.

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
which deliberately followed the Ix lineage). Refreshed 2026-07-06 against the merged
split/flat-family/serializer work: coverage rows reconciled with the `With*` and
`RepeatTrees` deletions, Defer/Using's promotion to the Tree facade recorded, the
split-dependent section unblocked, feed combinators re-anchored to the now-real stream
feeds. Next actions when desired: promote `SelectMany`, add `Unfold`; both are also the
two operators that would most exercise the monad laws in tests.*
