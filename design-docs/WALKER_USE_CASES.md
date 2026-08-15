# Walker Use-Case Catalog

**Status:** LIVING — every use case in the SHIPPED spelling where one exists, or an
explicit gap status where one doesn't. This document is the reference for what the walker
tier can do today and the spec for what it grows next (the axis wave, section A's pending
rows). The original strawman catalog (2026-08-10, the evidence-gathering step that
pressure-tested the contract before it existed) is design history, preserved in git
through commit `e02a31f`; the analysis it produced — layers, costs, provenance — survives
here, re-spelled.
**Branch:** `experimental/walker` · **Living contract:**
[WALKER_FACTORY_DESIGN.md](WALKER_FACTORY_DESIGN.md) (the door-only charter, Stage C
2026-08-15; [WALKABLE_CONTRACT_DESIGN.md](WALKABLE_CONTRACT_DESIGN.md) is its pre-cut
predecessor)

**Classification axes** (per entry): **Layer** — which floor of the region → walk →
sequence tower the call consumes at. **Re-enters?** — whether the code wants to jump back
into the live structure afterward (the provenance evidence). **Cost** — the price column
the call pays.

**Verdict legend:** **SHIPPED** (the spelling exists) · **COMPOSED** (green today by
composing shipped pieces) · **HAND** (green today via a short loop over shipped probes —
the sugar belongs to the axis wave) · **AXES** (awaits the axis wave proper) · **PRICED**
(awaits a navigation citizen per WALKER_DESIGN's navigation-price spectrum) · **SAMPLE**
(the contract supports it; the sample provider is unbuilt) · **DAG** (dag-branch
territory, unbuilt there).

**The headline, from the 2026-08-14 sweep:** the walk floor shipped with **zero new
operators** — "Copse algebra applied mid-structure" turned out to be `walker.Subtree()`
composed with the existing streaming algebra (section C is green by composition alone).
The sequence floor's primitives all shipped; its gap is exactly the 2016 library's sugar,
for which section A's pending rows are the spec. The region floor has one lens (the
severed subtree view); its set algebra and the DAG closures remain design.

---

## A. Tree axes — the sequence floor (shipped primitives, pending sugar)

The 2016 library's 29 `TreeWalkerExtensions`, each with ten years of validation. Every
pending row is implementable as a short extension over the shipped probes or a
`Subtree()` composition — this section is the axis wave's checklist.

### UC-01 Breadcrumb trail (`GetAncestors`) — HAND

Layer: sequence · Re-enters: no · Cost: O(depth)

```csharp
// Today, by hand — the climb. The axis wave's GetAncestors makes this an extension.
var stance = walkable.GetTreeWalkerAt(handle).MoveToParent();
while (stance.HasWalker)
{
  breadcrumbs.Add(stance.Walker.GetValue());
  stance = stance.Walker.MoveToParent();
}
```

`AndSelf` variants: start from `GetTreeWalkerAt(handle)` itself. (The 2016 grammar had both
`AndSelf` twins and an `ExcludeOption` enum — the wave picks ONE idiom.)

### UC-02 Find the root (`GetRoot`) — HAND

Layer: sequence · Re-enters: no · Cost: O(depth)

The climb to the last stance: loop `MoveToParent()` until `HasWalker` is false; the
previous walker was the root. Sugar pending.

### UC-03 Single-step navigation — SHIPPED

Layer: sequence · Re-enters: yes (continuously) · Cost: O(1) per step

The walker is the one consumer spelling (Stage B): `MoveToParent()`/`MoveToChild(k)` steps,
`At(handle)` re-entry; the probes are provider SPI (`ITreeTopology`), behind the door. The `Has*` questions are the result structs (`ParentResult.HasParent`,
`TreeWalkerResult.HasWalker`, `ChildResult.HasChild`) — the Try is built into the shape.

### UC-04 Indexed and keyed child access — SHIPPED / consumer-side

Layer: sequence · Re-enters: yes · Cost: O(1) (indexed); O(n) scan (keyed)

`walker.MoveToChild(k)` — the consumer spelling since Stage C (the strawman's
`GetChildAt` survives as `TryGetChildAt` on the `ITreeTopology` SPI, provider-side).
Keyed access
(`GetChildrenByKey`) is deliberately NOT a library member: value search is consumer code
(the no-node-equality pledge), spelled as consumer LINQ over `GetHandlesWithValues` —
the search law (OPERATOR_SURFACE_MAP.md §0; the brief `FindHandles`/`FindHandle` sugar
was retired the day it was reviewed). A search's honest miss is the empty sequence —
and never `FirstOrDefault` over ordinal handles: the miss masquerades as the root.

### UC-05 Sibling navigation — PRICED

Layer: sequence · Re-enters: yes · Cost: see the spectrum

Needs the focus's slot — the navigation-price spectrum's ladder (WALKER_DESIGN.md): lean
walkers recompute by scan, a slot-carrying stance or address labels make it O(1). HAND
meanwhile: `MoveToParent()` then sweep `MoveToChild(k)` comparing handles.

### UC-06 Subtree search (`GetDescendants`) — COMPOSED

Layer: region → sequence · Re-enters: sometimes · Cost: O(subtree)

```csharp
foreach (var descendant in walker.Subtree().GetHandles()) { ... }   // or any traversal
```

Of-type/filtered flavors: consumer `Where` over the severed view's streams.

### UC-07 Leaves under a node — COMPOSED

Layer: sequence · Re-enters: no · Cost: O(subtree)

```csharp
var leaves = walker.Subtree().GetLeaves();
```

### UC-08 Root-to-leaf paths (`GetBranches`) — AXES, the path-semantics canary

Layer: sequence · Re-enters: no · Cost: Σ path lengths (superlinear!)

Still owed its LOUD name when it ships (`EnumeratePaths…` family) — path enumeration is
the operation whose cost explodes on DAGs, and the tree spelling must not normalize the
verb the DAG side has to shout.

### UC-09 Measures (depth / height / degree) — HAND / COMPOSED

Layer: sequence (folds) · Re-enters: no · Cost: O(depth) / O(subtree) / O(k)

Depth = the climb, counted. Height/size = `Subtree()` + a scan or `CountNodes`. Degree =
step `MoveToChild(k)` to the first miss. The catalog's original asymmetry note stands:
depth is O(depth), height is O(subtree) — no design can make both cheap.

### UC-10 The classic traversals from a node — COMPOSED

Layer: walk · Re-enters: no · Cost: O(subtree)

```csharp
walker.Subtree().GetPreorderTraversal();     // or GetLevelOrderTraversal, or the visit stream
```

## B. Relations between nodes — the walker-only classics

All HAND today: expressible with climbs and handle sets (handle equality is the
provider's-own-terms clause — never value equality). All become one-liners under the axis
wave; the address-walker (spectrum) would make them arithmetic.

### UC-11 Is-descendant-of / is-ancestor-of — HAND

Layer: sequence · Cost: O(depth). Climb from the candidate descendant comparing handles
against the candidate ancestor.

### UC-12 Lowest common ancestor — HAND

Layer: sequence · Cost: O(depth₁ + depth₂)

```csharp
// Climb one path into a set, climb the other until the first membership hit.
var seen = new HashSet<int>();
for (var s = walkable.GetTreeWalkerAt(a); ; )
{
  seen.Add(s.Focus);
  var up = s.MoveToParent();
  if (!up.HasWalker) break;
  s = up.Walker;
}
var lca = walkable.GetTreeWalkerAt(b);
while (!seen.Contains(lca.Focus))
  lca = lca.MoveToParent().Walker;        // guaranteed to land: roots are in `seen`… same tree
```

(On preorder-ordinal handles the constants collapse: is-ancestor is span containment —
the future fast path.)

### UC-13 Distance and path between two nodes — HAND

Layer: sequence · Cost: O(depth₁ + depth₂). Two climbs to the LCA; distance = the climb
counts; the path = one climb reversed plus the other.

## C. Mid-structure sweeps — the walk floor, shipped by composition

The floor's entire design promise ("Copse algebra applied mid-structure") is
`walker.Subtree()` — the severed re-rooted view IS a treenumerable, so every operator
already works on it. No walk-floor operator was ever minted.

### UC-14 Aggregate one subtree — COMPOSED (the floor's thesis)

Layer: walk · Re-enters: no · Cost: O(subtree)

```csharp
var accumulations = walker.Subtree().LeaffixScan(seed, edgeAccumulator, nodeAccumulator);
```

### UC-15 Rootfix from the middle — COMPOSED

Layer: walk · Re-enters: no · Cost: O(depth) prefix + O(subtree) sweep

```csharp
// Fold the ancestor prefix by climbing (the part above the subtree)…
var prefixSeed = seed;
foreach (var ancestorValue in AncestorsRootFirst(walkable, handle))   // UC-01's climb, reversed
  prefixSeed = fold(prefixSeed, ancestorValue);

// …then stream the subtree with the prefix as seed — the designed shape, verbatim.
var scan = walker.Subtree().RootfixScan(prefixSeed, fold);
```

### UC-16 Prune and process within a region — COMPOSED

Layer: walk · Re-enters: sometimes · Cost: O(kept)

```csharp
walker.Subtree().PruneBefore(context => ...).Where(context => ...)   // anything composes
```

## D. DAG closures — the ownership showcase (UC-17 – UC-24) — DAG

All eight await the dag branch's walkable contract; the tree-side machinery they were
designed by analogy from is now proven. The analysis that survives as design input there:
**Upstream/Downstream closures** (UC-17/18) are the dag's `Subtree()` analogs, edge-atomic;
**reachability membership** (UC-19) is the memoized lens class, priced by the
descendant-information law; **region intersection** (UC-20, "what lies between") is a new
lens class — regions are EDGE sets, and ∩/∪ over two memos is O(1)/step;
**`TakeUpstreamWhere` decomposed** (UC-21) was this catalog's origin story — closure,
filter, and reify as separate citizens instead of one fused sweep; **effective stake**
(UC-22) is path semantics and must stay LOUD (the multiplicities canary); **NAV as a
region-restricted sweep** (UC-23) carries the oracle equivalence (region-restricted scan ≡
prune-the-complement sweep — free conformance tests); **attribution as a transpose**
(UC-24) is the free lens (swap in/out-edges — trivial in adjacency, hard in order algebra).

## E. Reification, interchange, and provenance — SHIPPED

The buffer re-parent ("captures are never address-poor") shipped this whole section, in
places better than designed.

### UC-25 Stream → store → walker — SHIPPED, better

Layer: region acquisition · Re-enters: n/a · Cost: O(n) once, deferred

```csharp
var walkable = pipeline.Materialize(BufferLayout.Preorder);   // the capture IS the walkable
```

The strawman had a dedicated escalation step; the re-parent dissolved it — walkability is
a property of capture itself. Declared layout = the axis-cost choice (`Preorder` buys
subtree spans and cheap ancestry; `LevelOrder` buys sibling runs and levels); organic
`Materialize()` lets the first consumer pin. Lazy either way — the pin at the call, the
O(n) at the first pull or probe.

### UC-26 The degenerate tower — SHIPPED

Layer: walk · Cost: zero over the store treenumerators

The everything-region from the virtual root, streamed, must equal the native streams —
pinned executable as `Extend(extract) ≡ id` (the Walk adapter's conformance certificate).

### UC-27 Analyze frozen, jump back live — SHIPPED

Layer: region → sequence → back · Re-enters: THE POINT · Cost: O(1) per re-entry

```csharp
var label = walkable.Subtrees().GetTreeWalkerAt(handle).GetValue();   // analyze the severed view…
var backHome = walkable.GetTreeWalkerAt(handle);           // …and stand in the source: same handles
```

Views never re-address — `Extend` and `Subtree` delegate handles untouched. The
per-capture clause bounds the promise: handles travel between a capture and its views,
never between captures.

### UC-28 Detached reification — SHIPPED

A fresh `Materialize` IS the detachment: new capture, new handle space, no tie to the
source. The deliberate-snapshot case that wants NO provenance.

## F. Native and calculated adjacency — SAMPLE

No materialization anywhere: the contract's infinity permission (a walkable makes no
capture claim). Both samples are unbuilt; the contract supports them.

### UC-29 The Collatz walker — SAMPLE

`IAsyncWalkableTreenumerable<long, long>` where the value is its own handle: parent =
the Collatz step (computable), children = {2n} ∪ {(n−1)/3 where valid}, root = 1. The
2016 library's showpiece; a `Copse.Trees` sample away.

### UC-30 The file system walker — SAMPLE

External object adjacency (directories answer their own probes) — the DOM/VisualTreeHelper
family. An adapter, not a capture; re-enters continuously.

### UC-31 The stateful cursor — SHIPPED (better than designed)

Layer: sequence · Re-enters: continuously · Cost: O(1) per step

```csharp
var walker = walkable.TryGetTreeWalkerAtRootIndex().Walker;
var child = walker.MoveToChild(0);
if (child.HasWalker)
  Process(child.Walker.GetValue());   // walker itself is unmoved
```

The strawman imagined a mutable cursor; what shipped is an immutable STANCE — steps
return new walkers, the comonad is pure, and "no unfocused cursor" is a constructor
invariant (the doors are result-typed; the empty forest grants no walker).

## G. Capstone — the spanning subtree of k nodes (UC-32) — **SHIPPED** (`SpanningSubtree`, 2026-08-14)

The capstone is an operation. The consumer's whole arc is one call:

```csharp
var walkable = sourceTree
  .Where(context => context.Node.IsRelevant)     // the streaming algebra upstream, untouched
  .Materialize();                                 // the escalation: adjacency lives on the capture

var spanning = walkable.SpanningSubtree(
  walkable.GetHandlesWithValues()                  // the rowid idiom: rows in, value
    .Where(row => interesting.Contains(row.Value.Key))  // predicate, handles out (the
    .Select(row => row.Handle));                   // search law -- searches are consumer LINQ)

if (spanning.HasWalker)
  Render(spanning.Walker.Subtree());              // the walker stands at the spanning root
```

```csharp
TreeWalkerResult<TValue, int> SpanningSubtree<TValue, THandle>(
  this IWalkableTreenumerable<TValue, THandle> source, IEnumerable<THandle> targets)
```

Result-typed because the operation is partial exactly twice, and each miss is a fact:
**no targets** (the spanning subtree of ∅ is ∅ — guarded where the semantics live, never a
seedless fold's vocabulary-free exception) and **disjoint trees** (targets in different
trees of a forest have no common ancestor). One target is not a miss: the node alone. The
result stands on a **new O(kept) capture** — its handles are that capture's own ordinals,
not the source's (the per-capture clause, pinned by test: the spanning root is ordinal
zero *there*). Violations stay loud on the exception channel — result types are for
misses, never for faults (the two-channel doctrine).

### The decomposition — every floor, kept executable

`SpanningSubtreeScenarioTests` keeps the floor-by-floor arc running as its own test, so
"the operation is a composition of shipped pieces" remains a claim the suite proves:

```csharp
// 1. Streaming algebra feeds the walker a derived tree; 2. organic Materialize (the
//    first act pins the layout; the declared form is an axis-cost ELECTION, never a
//    requirement); 3. acquisition — the rowid idiom, consumer LINQ per the search law:
var walkable = relevant.Materialize();
var targets = walkable.GetHandlesWithValues()
  .Where(row => interesting.Contains(row.Value))
  .Select(row => row.Handle)
  .ToList();

// 4. The LCA fold, WALKER-FIRST and RESULT-TYPED: one lift at the boundary, the whole
//    fold lives in the comonad, and the disjoint-trees miss is a fact — an int-returning
//    LCA has no honest miss at all (throw, -1, or 0-which-is-the-root). The helper is
//    hand-rolled today (private in the operation, mirrored in the test) — the axis
//    wave's first promotion candidate, with span arithmetic as the preorder fast path:
var lca = targets
  .Select(handle => walkable.GetTreeWalkerAt(handle))
  .Aggregate((left, right) => LowestCommonAncestor(left, right).Walker);  // hand helper; same tree by construction

// 4½. The kept-set: the climbs RECORD the paths (each stops at the first already-kept
//    ancestor — shared segments walked once). Coordinates, because a SET is storage:
var keptHandles = new HashSet<int> { lca.Focus };
foreach (var target in targets)
  foreach (var pathHandle in PathToAncestor(walkable.GetTreeWalkerAt(target), lca.Focus))
    keptHandles.Add(pathHandle);

// 5. Re-root at the LCA — the severed lens; never left the comonad:
var spanning = lca.Subtree();

// 6. The membership clamp — THE HANDLE-DECORATED STREAM: Extend stamps every node with
//    its own (handle, value) pair, PruneBefore cuts off-path subtrees in HANDLE-space
//    (membership is downward-closed, so prune semantics are exactly right), Select
//    projects back. The future membership LENS makes this adjacency-side and zero-copy;
//    the semantics are fixed here:
var clamped = spanning
  .Extend((topology, handle) => new HandleAndValue<int, string>(handle, topology.GetValue(handle)))
  .PruneBefore(pair => !keptHandles.Contains(pair.Handle))
  .Select(pair => pair.Value);
```

All six steps are green. What remains of the capstone is convenience, not capability: the
LCA's promotion to a public axis (ergonomics) and the membership lens (performance). The
free upcast at the end never went away: the view is walkable is treenumerable — "a
bottled walk."

---

## Tally

| Floor | Status |
|---|---|
| Sequence | Primitives SHIPPED (probes, walkers, `GetHandles`); sugar = the axis wave (A/B's HAND rows are its spec) |
| Walk | **SHIPPED by composition — zero operators minted** (`Subtree()` ∘ the existing algebra); the tower's strongest validation |
| Region | One lens shipped (the severed view — duplicate's label type); set algebra + membership + DAG closures remain design |

Provenance-by-default: shipped as the handle-sharing discipline (views never re-address).
Every reification case except the deliberate snapshot (UC-28) wants its way back in, and
gets it for free. The capstone RUNS through all six steps
(`SpanningSubtreeScenarioTests` — three targets spanning the root, and a mid-tree
cluster): the membership clamp turned out to be expressible by composition (the
handle-decorated stream), so both former gaps re-scope from capability to convenience —
the LCA arithmetic is the axis wave's ergonomics, the membership lens is the region
algebra's performance sugar (adjacency-side pruning without the decorate/project round
trip). Each wave's spec sits in this file; neither blocks anything.
