# The dag walker — the comonad over dag topology

> **Status: BUILT 2026-08-22 on `experimental/dag`, in one arc, to parity with the tree
> family's walker tier** (WALKER_FACTORY_DESIGN.md §1–§4 and §11; CATEGORY_THEORY_SURVEY.md
> §10 and §12). Every tree-side citizen has its dual below, the laws run over a five-citizen
> fan-out, and the five findings the build surfaced are recorded in §8. Names marked
> PROVISIONAL await Jason's ruling; nothing else is open.

## 1. The one sentence

**One comonad, over focused topology** — the foundation restatement is carrier-neutral, and
this document is its dag instance: a `DagWalker` is the focused pair (topology, handle); the
topology is the invariant subject; extend has schedules; the laws bind every representation
conditional on its validity predicate. What a dag adds over a tree is exactly one thing, seen
three ways: **sharing is representable**, so (i) the parent step is a *group* (the in-edge
group, indexed like the child group — the tree's single parent is its arity-one collapse),
(ii) steps are *edge-atomic* (every probe answers with the edge it crossed, payload included),
and (iii) the severed view needs *membership* (a node inside a cone may have in-edges from
outside it), which is the descendant-information law's price made concrete.

## 2. The dual, row by row

| Tree (main) | Dag (this branch) | Notes |
|---|---|---|
| `ITreeTopology<TValue, THandle>` — `GetValue`, `TryGetParent`, `TryGetChildAt`, `TryGetRootAt` | `IDagTopology<TValue, THandle, TEdge>` — `GetValue`, `TryGetParentAt(handle, inEdgeIndex)`, `TryGetChildAt(handle, outEdgeIndex)`, `TryGetSourceAt(sourceIndex)` | the SPI; all three probes indexed, never counted (fan-in is unbounded too); answers are `DagStep` |
| `Option<NodeAndSiblingIndex<THandle>>` | `DagStep<THandle, TEdge>` — `HasValue`, `Handle`, `Edge`, `EdgeIndex` | the project is self-contained (no `Option`); the step is the dag's own vocabulary — (edge, far node) — not a generic option wearing a dag hat |
| `IWalkableTreenumerable : ITreenumerable` — `GetTreeWalker()` | `IWalkableDagnumerable<TValue, THandle, TEdge> : IDagnumerable` — `GetDagWalker()` | one member, total; the door lands on the unfocused stance |
| `TreeWalker` — `Topology`, `HasFocus`, `Focus`, `GetValue`, `TryGetValue`, `At`, `MoveToParent()`, `MoveToChild(i)`, `MoveToRoot(i)` | `DagWalker` — same, with `MoveToParent(inEdgeIndex)`, `MoveToChild(outEdgeIndex)`, `MoveToSource(sourceIndex)` | `TryGetValue` is `bool TryGetValue(out TValue)` (no `Option`); two public mints (focused, unfocused); `default` is the one invalid inhabitant |
| `TreeWalkerResult` — flat 3-state | `DagWalkerResult` — flat 3-state **plus `Edge`** | the payload crossed rides the step answer; `default` on the miss and on the seed edge |
| `TreeTopology.Lazy(walkable)` | `DagTopology.Lazy(walkable)` | the deferral seam; knocked once |
| `Tree.FromTopology(topology)` | `Dag.FromTopology(topology)` | the Walk adapter: Kahn over probes (`TopologyWalkDagnumerator`) — the builder walk with `DagNode` lists replaced by the indexed out-edge probe; starvation throws `DagCycleException` naming the loop; ordinals minted at discovery |
| `ITreenumerableBuffer : IWalkableTreenumerable<TValue, int>` | `DagBuffer : IWalkableDagnumerable<TNode, int, TEdge>` | the buffer re-parent; handles = dense ordinals; `DagBufferTopology` over the CSR + the lazy in-adjacency |
| (builder not walkable) | `Dag<TValue, TEdge> : IWalkableDagnumerable<TValue, DagNode, TEdge>` | `DagNodeTopology`: the node is its own handle (self-sufficiency); membership, stray-parent filtering, and discovery-ordered in-groups memoized per knock (§8.1) |
| `Extend(observer)` on walkable / walker | `Extend` on walkable (`IDagTopology, THandle`) / walker (`DagWalker`) | `DagExtendWalkable`; self-feeds through `Dag.FromTopology(this)` |
| `Subtrees()` — the cofree duplicate | `Downstreams()` PROVISIONAL | labels = downstream cones (`DagDownstreamWalkable`), sharing kept inside, severed at the boundary |
| `Subtree()` — the inclusive hoist | `Downstream()` PROVISIONAL | at a node, the cone with the focus as sole source; at the unfocused stance, the whole dag (`DagTopologyWalkable`) |
| — (trees have no upward cone) | `Upstream()` PROVISIONAL | `Transpose ∘ Downstream ∘ Transpose` — no new lens type; ≡ `TakeUpstreamWhere` at one node (pinned) |
| — (tree transpose is an order-algebra operator) | `Transpose()` on the walker | the FREE lens (`DagTransposeTopology`): groups trade places, involutive by unwrapping; the transpose's source group = the sinks, one sweep memoized |
| `Duplicate()` | `Duplicate()` | `Extend(focus => focus)` |
| `GetHandles` / `GetHandlesWithValues` | same | stance walks with a visited set — dedup by handle equality (set semantics, the dag axis default) |
| `GetTreeWalkerAt(handle)` / `TryGetTreeWalkerAtRootIndex(k)` | `GetDagWalkerAt(handle)` / `TryGetDagWalkerAtSourceIndex(k)` | door + jump / door + one step |
| `PruneAfter` lens | `PruneAfter` lens | `DagPruneAfterWalkable`: matched node's out-group empty AND its edges gone from the targets' in-groups (transpose-consistent on everything handed out) |
| `HandleAndValue` | `DagHandleAndValue` | self-containment twin |
| `PreorderSkeletonValidity` (TestUtils) | `DagSkeletonValidity` (Copse.Dags.Tests) | three legs: CSR well-formedness, topological targets, transpose consistency as multiset equality |
| `SpanningSubtree(targets)` | **no citizen** | the tree's uniqueness (laminar closures) does not transfer: "what lies between" on a dag is a region intersection (UC-20), and a minimal connecting sub-dag is not unique. Deliberately absent, not approximated |
| `ForeignWalkableProviderTests` | `ForeignWalkableDagProviderTests` + `FamilyFreeDag` | `Copse.Dags` grants no `InternalsVisibleTo` at all, so the foreign citizen compiling in the test project is the ecosystem proof |

**Orientation, stated once (correcting WALKER_DESIGN.md §2's note):** out-edges point
DOWNSTREAM — from sources toward sinks, the direction the ownership flows and the visit
protocol dispatches. `MoveToChild` follows an out-edge; `MoveToParent` follows an in-edge;
`TakeUpstreamWhere` and `Upstream()` follow in-edges back toward the sources.

## 3. The unfocused stance is the virtual source

The dag family had built the sentinel completion before the tree reversed into it: the
**virtual source** — no value, authors every source's arrival, the seed's origin — IS the
unfocused stance. The door lands there; the sources are its child group (`MoveToSource`
is literally its `MoveToChild`); a source's `MoveToParent(0)` answers there (its one upward
step, the seed edge, `default` payload); stepping up from it is the algebra's one upward
miss; `Focus`/`GetValue` throw there and `TryGetValue` misses. The empty dag is the
unfocused walker alone. A non-source's index past its in-edge group is a PLAIN miss, never
the virtual source — only an EMPTY group's index 0 reaches it (pinned).

The transpose's unfocused stance is the **virtual sink**: `walker.Transpose()` keeps the
stance, and its child group is the sinks. Two hoists, as on the tree: inclusive is the
surface (`Downstream()`), exclusive stays in the derivation layer. The completed extend is
derived, never an operator: interiors by `Extend`, the unfocused row by direct application
(pinned with a cone-size observer: 4 at the unfocused stance of the diamond).

## 4. Lenses are topology transformers; the reshapings are pair citizens

Every lens is an `IDagTopology` over another, worn as a walkable through `Dag.FromTopology`
for its stream half. Cost classes, legible in the API:

| Lens | Rewrites | Price |
|---|---|---|
| `DagTransposeTopology` | both groups swap; source group = sinks | free per step; one sweep memoized for the source group |
| `DagExtendWalkable` | `GetValue` only | the observer's price |
| `DagDownstreamWalkable` | root's in-group empty; other in-groups filtered to members; source group = the root | membership = one sweep memoized at the first parent probe (O(reached), one allocation per view) |
| `DagPruneAfterWalkable` | matched out-groups empty; in-groups filtered by the predicate on the parent | one predicate evaluation per in-edge scanned |
| `DagNodeTopology` (the builder) | in-groups filtered to members, ordered by entry | membership sweep + one drain of the builder walk, memoized per knock |

The pair-citizen rule as tests (`DagLensTests`): `PruneAfter` lens ≡ streaming `PruneAfter`
(both halves); `Downstream()` ≡ `TakeDownstreamWhere` at one node; `Upstream()` ≡
`TakeUpstreamWhere`; walker `Transpose()` ≡ buffer `Transpose()`; `Upstream = Transpose ∘
Downstream ∘ Transpose`; lenses stack (a cone of a pruned view ≡ the streaming chain). These
are the dag's naturality squares — region-restricted view ≡ prune-the-complement sweep.

## 5. Extend's schedules and the class restriction, as tests

`DagWalkerComonadLawTests` pins the Store laws — `Extend(extract) ≡ id`, `extract ∘
Extend(f) = f`, co-associativity with a genuinely neighborhood-dependent second observer —
and the two coherence theorems:

- **`SourcefixScan ≡ Extend(the all-in-paths fold)`** — ownership lookthrough: the extend
  observer climbs every in-path (path-priced, exponential in general); the scan is the
  O(V+E) schedule the fold's semiring shape admits. 0.54 at the venture, both ways.
- **`SinkfixScan ≡ Extend(the all-out-paths fold)`** — path-counted size: the shared venture
  counts once per path (5 over 4 nodes at the apex — the scan's per-edge roll-up, the
  caller's documented choice), both ways.

Same answer, two prices: the foundation's "schedules are class-restricted on dags" is an
equation here, not a remark. A general closure observer has only the path-priced schedule.

## 6. The provider fan-out and the validity predicate

`DagWalkerLawProviders.IntHandled`: the buffer (CSR), the buffer transposed twice (a
distinct object whose ordinals were permuted and restored), and **skeleton-direct** — a
test-owned `CsrDagTopology` over raw arrays read off the public stream, validity-checked on
the way in (`DagSkeletonValidity.AssertValid` + `AssertTransposeConsistent`), streamed
through `Dag.FromTopology`. Citizens with other handle types join through the generic law
bodies: the builder (`DagNode` handles) and `FamilyFreeDag` (string handles, the foreign
provider). Every law runs over all five; the adjacency-oracle battery
(`DagAdjacencyConformanceTests`) checks every citizen's three groups against an oracle
rebuilt from the visit stream — sources in order, out-groups in dispatch order, in-groups in
`InEdgeIndex` order, payloads exact.

## 7. What did not carry, and why

- **`SpanningSubtree`** — see the table: uniqueness fails; the honest dag citizen is a
  region intersection, a different lens class (UC-20), not built until a workload asks.
- **Sibling steps** — a dag node's "next sibling" is parent-relative and a node has many
  parents; the edge-group index IS the sibling coordinate. No step minted.
- **Positional flavors** — depth is path-dependent on a dag; nothing to carry.

## 8. Findings from the build (each one a pin now)

1. **The builder's in-edge group is insertion-ordered; the stream's is discovery-ordered.**
   `DagNode.ParentEdges` follows `AddChild` calls; the arrival group's structural promise is
   the parents' entry order. `DagNodeTopology` drains the builder's own walk once (at the
   first parent probe) to learn entry order and presents in-groups sorted by it, stable for
   parallel edges. Caught by the adjacency oracle on the shared leaf (alpha, middle, beta —
   not alpha, beta, middle).
2. **Source ORDER is a presentation fact, not a structural one.** The materialized
   `Transpose()` presents the sinks reversed; `TakeUpstreamWhere` presents ordinal order; the
   transpose lens presents discovery order. Content pins read the source group as a set;
   per-citizen conformance still pins the order each citizen promises.
3. **Transpose consistency is multiset equality**, not existence: an in-group listing one
   parent twice when two parents each have one edge passed the first predicate. Tightened.
4. **The degenerate-tower pin holds up to relabeling of the correlation key.** A topology
   walk mints ordinals at discovery; the buffer presents its dense handles as ordinals. The
   visit SEQUENCE (modes, nodes, dispatching parents, edge indices, payloads) agrees exactly
   — that is the pin; ordinal identity was never promised by the contract.
5. **`At` keeps its DOOR's topology, not "the" topology.** The builder mints a fresh topology
   per knock, by design (mutable builder; a walker sees the graph as it was when knocked).
   The law is stated on the walker, not on the walkable.

## 9. Open for Jason's ruling (names only; semantics are pinned)

- `Downstreams()` / `Downstream()` / `Upstream()` — the cone vocabulary (vs `Subdags`,
  `Cone`, `Reach`). `Downstream()` is the hoist; the house flow grammar (sources, sinks,
  Sourcefix, Sinkfix, TakeDownstreamWhere) argued for it.
- `DagStep` for the probe answer (vs `DagNeighbor`, `DagAdjacency`).
- `MoveToParent(int inEdgeIndex)` — the tree's `MoveToParent()` grown an index, same name;
  alternatively `MoveToParentAt`. Kept nameless-of-`At` to mirror `MoveToChild(int)`.
- The self-containment twins (`DagStep` instead of `Option`, `DagHandleAndValue`,
  `bool TryGetValue(out)`): forced by the no-references rule; a Vocabulary reference would be
  a mechanical swap if ever wanted.
- `DagWalkerResult` is four leaf fields (topology, handle, edge, outcome) where the tree's
  is three — the tree measured a promotion cliff at four. No dag benchmark family exists to
  measure it; flagged, not acted on.
