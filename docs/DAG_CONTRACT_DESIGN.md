# DAG traversal contract (design sketch)

> **Status: DESIGN ONLY (2026-07-18, branch `experimental/dag`). Nothing here is built.**
> This sketches a first-class streaming DAG contract family — the `ITreenumerable` analog
> for graphs with sharing — to be ratified (or torn apart) before any code. The existing
> `Copse.Dags` spike (the mutable `Dag`/`DagNode` object model, 53 tests, the money-movement
> scenario suite) is NOT superseded: it becomes the family's **builder** and its
> **conformance oracle**, the role `EngineTree` plays for the tree families. Companion
> records: [OPERATOR_COMPOSITION_DESIGN.md](OPERATOR_COMPOSITION_DESIGN.md) (the operator
> architecture this family inherits), [LAZINESS_AND_BUFFERING_POLICY.md](LAZINESS_AND_BUFFERING_POLICY.md)
> (the promise this family extends), [TRAVERSAL_DIMENSION_SPLIT.md](TRAVERSAL_DIMENSION_SPLIT.md)
> (the pattern the dimension section instantiates).

> **The domain this serves:** legal-entity ownership structures — the library's origin
> problem. Nodes are entities, edge payloads are ownership fractions, flows run money down
> (allocation) and attribution up (lookthrough). The structures became DAG-shaped when
> entities gained multiple owners; the spike's `MoneyMovementScenarioTests` is the living
> statement of the workload.

## The one observation everything follows from

**Topological order is to a DAG what preorder is to a tree**: the canonical linear
presentation, the order a streaming pass wants, and the order a flat encoding stores.
Copse's architecture is, top to bottom, machinery for exploiting a canonical linear
presentation of a hierarchy — visit-stream contract, streaming operators over it, flat
stores that ARE the presentation, narrow sources that can only afford it forward. All of it
transfers once the presentation is topological order. What does not transfer is exactly
what topological order cannot express: global per-node coordinates. That trade is the
whole design.

## Why `ITreenumerable` itself cannot stretch (settled)

Recorded once so the question stays closed:

1. **`NodePosition` has no meaning.** Sibling index is per-parent; depth is per-path. A
   shared node has no coordinates — by theorem, not by implementation choice.
2. **The visit protocol assumes unique parentage.** Scheduling/visiting, sibling
   renumbering, promotion — all speak single-parent vocabulary.
3. **Sharing requires identity**, and the tree engine's defining virtue is adapting foreign
   data with NO node identity. Grafting DAGs onto it would force equality into a contract
   founded on never asking for it.

The precedent is the color rule: an axis the type system cannot abstract gets a parallel
contract family with shared vocabulary and philosophy — not a leaky generalization that
weakens the original's promises.

## The visit protocol: Discover / Enter

The tree's two-phase visit (scheduling, then visiting) generalizes to a DAG as:

- **Discover** — emitted once per IN-EDGE, when the edge's parent (already entered)
  dispatches its out-edges in child order. A discovery carries the per-edge context: the
  edge payload, the dispatching parent's ordinal, and the edge's index within that parent's
  out-edge list (the per-parent sibling index).
- **Enter** — emitted exactly once per node, after its LAST discovery. Topological order is
  precisely the guarantee that this is well-defined: every in-edge has been seen, so every
  inflow is complete at entry.

A tree degenerates exactly: one in-edge, so discover = schedule and enter = visit. The
spike's per-use vs shared-once ambiguity dissolves into the protocol itself: edge-grained
work rides discoveries, node-grained work rides entries — the consumer chooses by where it
listens, not by a per-call flag.

**Node correlation is by ordinal, not identity.** The stream tags each node with its
topological ordinal — its index in the enumeration dimension's topological order, stable
for the enumeration. Consumers
correlate a shared node's appearances by ordinal; user values are never compared or hashed.
Identity exists only at the ADAPTER boundary — a DAG source must be able to say "this
child is that node again," which is the one place the no-identity principle bends
(quarantined exactly like the spike's import-adapter posture).

Roots (in-degree zero) are discovered by convention at the start of enumeration — the
ForestRoot-sentinel analog. **Dispatch is contiguous** (a stream contract clause, relied on
by the survey-shaped passes): a node's out-edge discoveries immediately follow its entry as
one block, in out-edge order — no other node's visits interleave. Wrappers preserve this
(they only remove visits).

## The dimension split: forward / backward

Trees split depth-first/breadth-first. DAGs split **forward topological / backward
topological** (the transpose's forward), and the operator families sort onto the dimensions
by the direction information flows:

- **Forward** — everything whose inputs are complete at entry: `Select`, the prunes,
  contraction (see below), `RootfixScan`/`RootfixDispatch`, `Do`, `OrderChildrenBy`
  (reorders out-edge dispatch). The spike's finding "rootfix cannot stream on a DAG" was
  true of its re-walk model and is FALSE in this presentation: at entry, all inflows have
  arrived, so the rootfix family **streams** with O(frontier) state.
- **Backward** — everything that needs children first: the `Leaffix` family, including the
  edge-aware `LeaffixDispatch` that closes the spike's deferred upward-diamond semantic
  (per-in-edge attribution up through a shared entity; the JV-lookthrough operator).

Contract shape mirrors the tree split: `I{Forward|Backward}…` narrow interfaces, the
composite deriving from both. A materialized store affords both dimensions; a forward-only
source (a serialized topological stream) affords forward only — asking it for backward is a
compile error, and `Memoize`/`Materialize` are the explicit escalation back to the
composite. **Transpose is the new dual**: on a composite store it is a free view (swap
which adjacency you read), and it swaps the dimensions — the `Invert` analog, structurally
lovelier than the original.

Note the tree's dimension split is *traversal strategy over the same information*; the DAG
split is *direction of information flow*. Same architecture, deeper reason.

## Streaming results that make the family worth building

1. **Rootfix streams** (above): inflows complete at entry.
2. **Prunes stream**: liveness is a forward fold. A node is live iff at least one live
   in-edge from a live parent reaches it — decidable at entry. `PruneBefore` kills node +
   out-edges; `PruneAfter` keeps the node, cuts its out-edges; downstream liveness
   propagates. The spike's recomputed-reachability (a full re-walk) becomes one pass.
3. **Contraction streams**: the entity-that-does-not-participate case (a node dissolves;
   flow continues). At entry the node holds all in-edges; composing them with its out-edges
   dispatches (parent, child) edges forward. The library owns the structure; the caller
   owns the edge algebra (`inEdge ∘ outEdge` — e.g. 60% × 50% = 30%): a required lambda,
   because payload composition is domain semantics. Parallel edges from contraction are
   permitted (the spike already permits them). This is `Where`'s child promotion translated
   to shared parentage — the domain even names it (disregarded / pass-through entities).
   Whether it is spelled `Where` or named for what it does (`Contract`? `Dissolve`?) is a
   naming question below; it is NOT day-one surface (the workload moves cash true to form;
   this is the rare case).
4. **Operator chains compose.** These are all arrows over the entry/discovery stream; the
   composition architecture (one wrapper, adjacent arrows composed, representation tiers)
   applies. Machinery deferred; the design constraint is only that nothing here prevents it.

Cost model: forward streaming state is O(max frontier) — discovered-but-unentered nodes
plus per-node pending in-degree counts — linear total work in nodes + edges. The
*unfolding* alternative (present the DAG as its tree of paths through the existing tree
machinery) stays available as a bridge view for genuinely per-path questions, but it is
not the family's basis: paths are exponential in the worst case; this contract never pays
them.

## The families

- **Builder / oracle**: the spike's `Dag<TValue,TEdge>`/`DagNode` object model. It already
  computes discovery-biased topological order and carries first-class edge payloads
  (`DagEdge`/`DagParentEdge`); it adapts to the composite contract directly, and its
  existing operations (`LeaffixAggregate`, `RootfixAllocate`, the operator clones) are the
  independent implementations the conformance battery diffs against — the `EngineTree`
  role. Cycle posture unchanged: construction-time acyclicity by wrapper-node linking,
  `DagCycleException` from the live walk. (Whether the family ever needs a posture toward
  cyclic inputs beyond refusing is a domain question — circular holdings exist in the wild
  — but it is out of scope for this contract; a cycle-tolerant condensation view would be
  a future adapter, not a protocol change.)
- **Flat family**: the topological array IS the flat DAG encoding — `values[]` in topo
  order plus per-node out-edge lists of `(childOrdinal, TEdge)` (CSR-style adjacency;
  sharing expressed by ordinal reference, exactly how the flat tree stores express
  containment by index arithmetic). A forward store affords forward; storing the transpose
  adjacency as well affords both. The serializer writes the topo stream; the forward-only
  streaming deserialize is a narrow forward source — the same round trip the tree family
  has, ending on the narrow tier.
- **Adapter boundary**: foreign DAG-shaped data (the work import) enters through an adapter
  that must supply stable node keys — the quarantined identity bend. The contract's stream
  itself speaks ordinals only.

## What carries, what changes, what dies

| Tree concept | DAG fate |
|---|---|
| Preorder / level-order presentation | topological order |
| Scheduling / Visiting | Discover (per in-edge) / Enter (once) |
| `NodePosition` | per-edge context (payload, parent ordinal, per-parent index) + topo ordinal — no global coordinates, by theorem |
| DFT / BFT dimension split | forward / backward (transpose) split; same narrow-tier + `Memoize` escalation architecture |
| `Invert` | `Transpose` — a free view on composite stores; swaps the dimensions |
| `Where` child promotion | contraction with caller edge-composition; not day-one |
| Prunes | forward liveness folds (streaming) |
| Rootfix family | forward, streaming; `RootfixDispatch` carries edges natively |
| Leaffix family | backward; edge-aware `LeaffixDispatch` = the upward-diamond dual |
| `Select` / `Do` / `OrderChildrenBy` | carry directly (`SelectEdges` joins as the edge dual) |
| Flat stores + serializer | topo array + CSR adjacency; ordinal-referencing text format |
| Set operations (`Union`, …) | DO NOT carry — they align by position; DAGs have none. Absent, not approximated |
| No node identity anywhere | bends ONCE, at the adapter boundary (sources must key their nodes); the stream uses ordinals |
| Two-phase strategies (`SkipNode`/`SkipDescendants`/`SkipSiblings`) | needs its own design — see open questions; skips become liveness votes, and per-EDGE skips (impossible on trees) want to exist |

## Open questions

1. ✅ **Naming RATIFIED (Jason, 2026-07-18)**: `IDagnumerable` / `IDagnumerator` — the pun
   carries the brand. Downstream: `DagnumeratorMode` (`DiscoveringNode` / `EnteringNode`),
   `IForwardDagnumerable` / `IBackwardDagnumerable`, `DagTraversalStrategies`.
2. **Strategy semantics — phase 1 ships a PROPOSAL, open for review.** The consumer can
   only shape the FUTURE: at the moment a visit is published, everything about it has
   already been witnessed. Hence: `SkipEdge` (at Discover — sever the just-discovered
   in-edge; per-edge expressive power trees never had) and `SkipOutEdges` (at Enter — keep
   the node, dispatch nothing). There is deliberately NO consumer `SkipNode`: an entry
   cannot be retracted, and removing a node from the logical dag is operator business
   (PruneBefore), not a consumer verdict. A node whose every potential discovery is severed
   or never emitted simply never enters — consumer skips compose with liveness. Passing a
   strategy in the wrong mode THROWS (strict ethos; rehearsal-tested at birth per the
   tree family's lesson). A SkipSiblings analog (dispatching parent's remaining out-edges)
   still needs a real case before it exists.
3. ✅ **`TEdge` RATIFIED (Jason, 2026-07-18)**: the contract always carries
   `<TNode, TEdge>`; an edge-less source is `<TNode, Unit>`-style sugar when needed.
4. ✅ **Color RATIFIED (Jason, 2026-07-18)**: sync-only until a consumer exists; contracts
   written so the async transcription stays mechanical later (no sync-only idioms).
5. **Contraction's spelling** (`Where` vs a named operator) — deferred with the operator
   itself.
6. **Where the family lives**: grow `Copse.Dags` into contract + families, or split
   `Copse.Dags.Core` etc. mirroring the tree layering. Proposal: single project until
   graduation forces the split (the spike's own no-new-projects rule).
7. ✅ **The scan/dispatch return shape RATIFIED (Jason, 2026-07-18): option (a).** The
   theorem: a rootfix result is an ENTRY-TIME fact (it needs every inflow), but the
   protocol publishes a node's value at its DISCOVERIES too, which precede entry — so
   `RootfixScan` cannot honestly return a streaming `IForwardDagnumerable<TResult>`.
   (Trees dodge this: one in-edge, and the parent's accumulation is known at scheduling.)
   Ruling: the PASS streams (one walk, each node computed once, at entry); the RESULT is a
   materialized composite — a fresh builder `Dag` — fully composable and affording both
   dimensions (the materialization is an upgrade, `Memoize`-like). The spike's own scans
   already returned this shape; the laziness policy's documented-when-not clause, with the
   theorem as the documentation. Covers `RootfixDispatch` (tree-side buffer precedent).
   Corollary recorded with it: identity for DAGs is irreducible (sharing IS an equality
   proposition); the design canonicalizes it rather than pretending it away — reference
   identity on the library-owned `DagNode` at the builder, ordinals in the stream, foreign
   keys quarantined at the import adapter. User values are never compared or hashed.

## Phases (proposed)

0. Ratify this document — naming, strategy set, `TEdge`, color posture.
1. Vocabulary + contracts + the spike's adapter to them + the conformance harness (spike
   as oracle; battery style copied from `VisitStreamConformance`).
2. Forward dimension end-to-end — the work workload's downward half.
   ✅ 2a (2026-07-18): the four honestly-streaming wrappers — `Select`, `Do`,
   `PruneBefore`, `PruneAfter` — as protocol passthroughs over
   `IForwardDagnumerable`. The prunes ARE the strategy machinery (a wrapper answering
   `SkipEdge` / `SkipOutEdges` to its inner walk; the source's liveness fold does the
   rest), which is the strategy design validating itself. Operators preserve source
   ordinals (nothing relabels — ordinals are correlation keys, not coordinates; the
   contract wording was loosened accordingly: strictly increasing along entries, density
   not promised). Pinned: exact streams with deliberate ordinal gaps, chains,
   consumer-strategy passthrough, and content differentials against the builder's own
   operator clones — the oracle earning its keep.
   ✅ 2b (2026-07-18): `RootfixScan` — edge-paired inflows (`DagInflow` closes the spike's
   deferred pairing; empty at sources seeds the scan), one streaming pass into a
   materialized shape-isomorphic composite. `RootfixDispatch` — the survey-shaped
   allocation pass: nodes resolve as `DagDispatchNode` (value + edge-paired inflows;
   sources get the single seeded inflow), surveys receive the COMPLETE live out-edge list
   as exactly-once `DagDispatchTarget` slots (unwritten/double-written throw — the strict
   ethos), outflows land as the targets' inflows. Only LIVE edges are surveyed, so
   `PruneBefore(blockers).RootfixDispatch(...)` composes the blocker semantics into the
   allocation for free — the MoveMoney shape, streaming until the capture. Pinned:
   effective-ownership lookthrough through the diamond (60%x70% + 40%x30% = 54%), money
   movement with attribution (which amount arrived on which edge), prune composition,
   leaves-never-surveyed, slot strictness, and the builder-scan oracle differential.
3. ✅ (2026-07-18) The `Leaffix` family — with one refinement the build surfaced: the
   leaffix OPERATORS ride a FORWARD capture folded in reverse topological order, not the
   backward walk. Two reasons, both structural: a leaffix result is children-first by
   definition, so the whole graph precedes the first result regardless of walk (the tree
   family's capture-then-fold pattern); and the result dags must be shape-isomorphic, but
   the backward stream cannot carry a node's out-edge ORDER (it carries in-edge order — the
   transpose's dispatch lists) — original orientation is a forward-stream fact. The
   backward dimension keeps its honest job: direct upward consumption, and the transpose
   view. `LeaffixScan`: edge-paired child results in out-edge order, empty at sinks seeds,
   shared child computed once but appearing per-edge in each parent's list — the diamond
   roll-up choice stays the caller's, documented. `LeaffixDispatch` CLOSES the deferred
   upward-diamond semantic: each node decides what travels up EACH in-edge (exactly-once
   targets, discovery order), so what a child sent up an edge IS that parent's share, by
   construction — no double count. No seed, by duality: downward's money is external to
   the dag; upward's holdings live in the nodes, so sinks just see empty upflows. Sources
   are never surveyed; their resolved inflows ARE the attribution. Pinned: the JV
   lookthrough (venture's 1000 arriving at the apex as 120-via-40% + 420-via-60% = 540)
   and THE DUALITY — the downward ownership scan times the holding equals the upward
   attribution, 54% of 1000, both ways.
4. Flat store + serializer + `Memoize`/`Materialize` + `Transpose`.
5. Operator composition machinery (the tree family's architecture, transplanted).
6. The showcase: the ownership-structure scenario suite grows into the flagship sample —
   real workload, both dimensions, allocation down and lookthrough up.
