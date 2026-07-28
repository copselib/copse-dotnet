# DAG traversal contract (design sketch)

> **Status: BUILT through phases 1–3 + the scenario seed (2026-07-18) and the surface
> cleanup (2026-07-27), branch `experimental/dag`; work integration ("DIG") in progress.
> Structural re-founding RATIFIED 2026-08-02 (block below) — one contract + `Transpose`,
> `DagBuffer` as the materialized shape — implementation pending; ✅ marks predate it.**
> Written 2026-07-18 as the design sketch; the ✅ marks below track what has since shipped.
> The `Copse.Dags` spike (the mutable `Dag`/`DagNode` object model) is NOT superseded: it
> is the family's **builder**, and its spike-era operators serve as the **conformance
> oracle**, the role `EngineTree` plays for the tree families.
>
> **Vocabulary ratified 2026-07-27 (Jason):** node sets are **sources/sinks** — graph
> theory's own terms for in-degree/out-degree zero; category theory's initial/terminal
> objects were considered and REJECTED for accuracy (they require uniqueness — a
> multi-source DAG has no initial object), and roots/leaves dropped as tree-flavored. The
> operator prefixes follow: **Sourcefix/Sinkfix** — coined here, deliberately ("as long as
> we are blazing trails, we might as well be the ones to name the things"); the TREE family
> keeps Blelloch's rootfix/leaffix, so each family's prefix names the fixed end in its own
> true vocabulary. Edge endpoints keep quiver speak (source/target) where they appear.
> Product surface principle, same date: everything an operation can be, it is — an
> extension on the contract (`Dagnumerable.*`, including `GetTopologicalOrder`'s value view
> and `GetEdges`); the builder keeps only construction, mutation (sorts), acquisition, and
> the owned-node `GetTopologicalOrder` view.
>
> **Dispatch provenance (2026-07-27, work-integration-driven):** dispatch inflows are
> `DagDispatchInflow` and carry their **Dispatcher** — the node that wrote the slot (parent
> downward, child upward; the seeded inflow's is default, the seed being external to the
> dag). Provenance comes from the API, never smuggled inside `TDispatch`: the machinery
> always knows who dispatched, and withholding it forces callers to pollute their payloads
> with identity fields. The library never compares Dispatcher values; caller-side identity
> joins are the caller's business, and index-based joins remain `InEdgeIndex`'s job. The
> scans keep the lean `DagInflow` — accumulations carry no provenance until a consumer
> needs it. Companion
> records: [OPERATOR_COMPOSITION_DESIGN.md](OPERATOR_COMPOSITION_DESIGN.md) (the operator
> architecture this family inherits), [LAZINESS_AND_BUFFERING_POLICY.md](LAZINESS_AND_BUFFERING_POLICY.md)
> (the promise this family extends), [TRAVERSAL_DIMENSION_SPLIT.md](TRAVERSAL_DIMENSION_SPLIT.md)
> (the pattern the dimension section instantiates).
>
> **The structural re-founding (ratified 2026-08-02, Jason; from the full-branch design
> review):** the operator tier — the scans, the dispatch twins, the prunes, the edge dual,
> and both scenario suites — is right and does not move. The structural layers around it
> do. Three decisions:
>
> 1. **One contract.** The forward/backward dimension split is RETIRED. The tell was in
>    this document's own table: two tree concepts (`Invert` and the DFT/BFT split) mapped
>    onto one DAG concept, orientation-flipping — and the tree family already ruled on
>    which homolog is real: `Invert` is an OPERATOR returning a buffer, not a dimension.
>    BFT is a dimension because it is irreducible — not expressible as DFT-of-anything;
>    the backward walk is *definitionally* forward-of-the-transpose (the implementation
>    hands the reversed order to the same walk class), which makes it an operator. After
>    phase 3's sinkfix defection to the forward capture, the backward dimension had ZERO
>    operator consumers. `Transpose()` replaces it and strictly gains: the view inherits
>    the entire forward operator family pointed upward (prune ancestors, scan the
>    transpose, …) where the dimension offered a raw stream and nothing else. The
>    affordance story survives as a store-capability fact: `Transpose` exists on the
>    buffer (free — swap which adjacency you read) and the builder (cheap), not on a
>    narrow forward-only stream; `Memoize`/`Materialize` escalate, as ever. The
>    narrow/composite interface trio collapses to one `IDagnumerable`. (If a true DFT/BFT
>    homolog is ever wanted it is depth-biased vs level-biased topological order — two
>    forward presentations of one orientation — not backward; deferred until a layered
>    workload asks.)
> 2. **`DagBuffer`, the capture tier.** The materialized composite the folds return is an
>    owned, immutable CSR capture — `values[]` in entry order (dense index IS the
>    ordinal), out-adjacency as offsets/targets/payloads parallel arrays preserving
>    per-parent out-edge order, `sources[]`, a `sourceOrdinals[]` back-map when captured
>    from a gapped stream, transpose adjacency built lazily on first `Transpose()` — NOT
>    a fresh builder `Dag`. The builder had accreted five roles (construction API, only
>    concrete source, oracle substrate, capture, every fold's return shape); it keeps
>    construction and shrinks back to the engine's relationship with its buffers. Two
>    forcing facts. First, fold results were UNADDRESSABLE: correlating a result back to
>    its input required smuggling names into payloads — precisely the failure mode the
>    dispatch-provenance ruling above condemns — and preserved ordinals plus dense
>    indexing fix it. Second, there is no index-arithmetic-only DAG structure to hold out
>    for: a DAG's structure is M edges of information (the M-integer floor is arithmetic,
>    not a design gap), so CSR is the answer, not a compromise. One type, four roles that
>    genuinely belong together: `Materialize`'s return, the fold results, the
>    serializer's in-memory target, the flat store — phase 4's store IS this buffer. The
>    one-pass fill in stream order falls out of the protocol's own clauses (entries
>    arrive in layout order; dispatch contiguity makes each adjacency block contiguous)
>    — the edge-grained stream paying for itself.
> 3. **The laziness ledger, stated once.** The boundary is theorem-fixed, not
>    buffer-caused: the streaming tier (`Select`, `Do`, the prunes, edge tier 1) stays
>    lazy wrappers; the folds materialize, per open question 7's theorem. The one honest
>    loss against the tree family is `SourcefixScan` — tree `RootfixScan` streams because
>    single parentage makes the accumulation a scheduling-time fact; multiple parents
>    make it an entry-time fact — the price of admission to DAGs, paid at exactly one
>    operator. The buffer composes through the fluent surface (it IS an `IDagnumerable`),
>    so materialization breaks laziness, never fluency.
>
> Sequencing, same date: this ratification first; the refactor second (collapse + buffer
> + retarget the folds + `Transpose` — the scenario suites should move only mechanically,
> which is the verification); the work-import adapter THIRD, before any further operator
> growth: the contract's stream still has one implementation and no raw consumers, and
> the adapter — the origin problem's actual shape — is what pressure-tests it.

> **The constitution alignment (ratified 2026-08-05, Jason; the tree-constitution review
> session):** the tree family's aggregation constitution (docs/SCANRESULT_DESIGN.md on
> main — ScanResult, the seat rule, the two instruments, the north star, full
> participation, the readiness clause, type-fixer-first) re-derives this family's
> operator surface. The 2026-08-02 re-founding stands; these rulings compose with it and
> the refactor executes both as ONE re-derivation per operator, not two rewrites. The
> project stays **100% self-contained** — no references outside `Copse.Dags`; the
> constitution transfers by philosophy, never by shared types (the color-rule posture),
> so shared vocabulary gets DAG twins (`DagScanResult`).
>
> 1. **Results are pairings, from the API.** The folds return `DagBuffer` captures of
>    `DagScanResult<TNode, TResult>` — the value-replacing shape-isomorphic result and
>    the name-smuggling it forced (`ByName`, tuple-carried names) die. The dispatches
>    return a DISTINCT record type pairing the node with its arrival GROUP as a
>    buffer-backed view (indices into the capture's parallel arrays, O(1)) — the
>    recording rule made type-level, sidestepping the tree's `.Accumulate`
>    double-meaning from birth. `DagDispatchNode` retires as a result shape.
> 2. **Surveys keep their SUBJECT, both directions — forced by n-ary in-flow.** The
>    tree's subject-drop rested on single parentage: one dispatch site holds everything,
>    so the target-node fold-encoding works. With n parents no authoring site holds a
>    node's whole arrival, the only fold-encoding is same-node, and it needs the subject
>    — every DAG node is input-side leaffix-like, and the tree's leaffix keeps its
>    subject for exactly this reason ("each survey keeps the seats its flow direction
>    cannot derive"). Seats are DESTRUCTURED — `(self, arrivals-view, targets-view)` —
>    the bundled `DagDispatchNode` callback parameter was a pairing-in-callback, which
>    the seat rule outlaws.
> 3. **Full participation via the VIRTUAL SOURCE FAMILY** (matching main: the boundary
>    is an invocation, not a callback). The protocol already discovers all sources as a
>    block at enumeration start, so the same survey fires first for
>    `(default subject, seed, sources-as-targets)` — the virtual source has no value,
>    and that one invocation's empty subject is semantically true, not manufactured.
>    Each source's arrival is thereby AUTHORED with the source in hand; the manufactured
>    `DagDispatchInflow(default, seed, default)` and the `IsSource` flag both die; a
>    dispatcher-less arrival is the in-band arrived-from-outside test; and a budget
>    allocates ACROSS co-investing sources (the ownership workload's own shape) with
>    the same callback that allocates everywhere else.
> 4. **The Dispatcher seat SPLITS HOMES** (re-argued under the seat rule; reverses the
>    2026-07-27 in-band ruling, whose derivability facts the buffer changed). It stays
>    in the callback views — immediate context, consumed in place, mid-pass no buffer
>    exists to consult — and leaves the traveling results, where "who wrote in-edge i of
>    node n" is index arithmetic over the buffer's transpose adjacency. The provenance
>    principle survives intact; its implementation moves from an in-band field into the
>    buffer's structure.
> 5. **The scans keep the fused callback** (A/B-tested 2026-08-05, ruled "close enough —
>    keep and move forward"): the four-seat dual fold (seed | selector, edgeMap,
>    edgeAccumulator, nodeAccumulator — the tree leaffix dual with the edge payload's
>    justified seat) produced identical results at every ported call site, won inference
>    (zero explicit type args vs required-everywhere) and allocations (−14%: no per-node
>    inflow list), and tied on time — LOGGED for possible future adoption. Accepted
>    consequences: the scans stay fixer-less and boundary-fused (`Count == 0` in-band).
>    NOTED SEAM: the scan's boundary instrument (in-band empty) differs from the
>    dispatch's (virtual-family seed), so the scan≡dispatch coherence pin encodes
>    through the dispatcher-less filter (fold over authored arrivals only) rather than
>    boundary-for-boundary.
> 6. **The sinkfix family is DERIVED**: `Transpose ∘ Sourcefix* ∘ Transpose` — minimal
>    effort, reversible free. Build order follows: `DagBuffer` + `Transpose()` land
>    before any sinkfix operator; the `DispatchEdges` twins collapse to one
>    implementation; the coherence battery gains the transpose law and must VERIFY (not
>    assume) per-group edge order under transpose. Sinkfix seed flavors DEFERRED — ruled
>    lawful in principle, a genuine departure from the tree: the DAG's upward dispatch
>    is family-shaped with a real arrival seat (the virtual sink family is the virtual
>    source family of the transpose), where the tree's leaffix survey has no seat for a
>    seed to participate through — but not MVP. Consequences accepted for MVP:
>    `SinkfixDispatch` and the `DispatchEdges` twins stay fixer-less (explicit type
>    args); the `sourceNodeSelector` bypass flavor is likewise deferred (the grid closes
>    later at zero cost).
> 7. **The readiness clause** (main's, adopted): a survey fires when its data is ready —
>    arrivals complete (sourcefix), children's writes complete (sinkfix). The PARTIAL
>    order is the operator's meaning and is guaranteed, as is per-group order — which on
>    a DAG is a PRESENTATION fact (in-edge arrival order varies across valid topological
>    presentations), stable within an enumeration and frozen by capture. The total
>    cross-node order is deliberately unspecified: topo order is already non-unique, and
>    pinning the walk's accidental linearization would foreclose parallel builds. The
>    operator docs' current total-order promises reword accordingly.
> 8. **Internals follow the measured lessons**: the dictionary-spined passes rebuild as
>    sequential flat-array CSR passes (capture → child-index → fold) — the tree family's
>    evidence-settled structure (fused walks measured out, twice); streaming-state
>    claims carry honest O(max frontier) statements.
>
> Verification bar for the refactor: the scenario suites move only mechanically (the
> re-founding's bar — `ByName` gets simpler, per pairing results), PLUS the coherence
> battery: scan ≡ fold-encoded dispatch (via the noted seam's encoding) and
> sinkfix ≡ transpose-derived, every flavor pinned, every future flavor joining.

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

Sources (in-degree zero) are discovered by convention at the start of enumeration — the
ForestRoot-sentinel analog. **Dispatch is contiguous** (a stream contract clause, relied on
by the survey-shaped passes): a node's out-edge discoveries immediately follow its entry as
one block, in out-edge order — no other node's visits interleave. Wrappers preserve this
(they only remove visits).

## The dimension split: forward / backward

> **RETIRED 2026-08-02 (the re-founding, above): one contract + `Transpose()` the
> operator.** The section below is the original design, kept as the record of why the
> split was tried and what it got right — the affordance reasoning survives intact,
> relocated onto the store tier (`Transpose` exists on buffer/builder, not on narrow
> forward-only streams; `Memoize`/`Materialize` escalate).

Trees split depth-first/breadth-first. DAGs split **forward topological / backward
topological** (the transpose's forward), and the operator families sort onto the dimensions
by the direction information flows:

- **Forward** — everything whose inputs are complete at entry: `Select`, the prunes,
  contraction (see below), `SourcefixScan`/`SourcefixDispatch`, `Do`, `OrderChildrenBy`
  (reorders out-edge dispatch). The spike's finding "sourcefix cannot stream on a DAG" was
  true of its re-walk model and is FALSE in this presentation: at entry, all inflows have
  arrived, so the sourcefix family **streams** with O(frontier) state.
- **Backward** — everything that needs children first: the `Sinkfix` family, including the
  edge-aware `SinkfixDispatch` that closes the spike's deferred upward-diamond semantic
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

**Composition order is data-flow order (clause, 2026-07-28).** `SourcefixScan(f).SinkfixScan(g)`
means g's inputs ARE f's outputs — fixed by the shape of the expression, like function
composition; no traversal choice at the consumption end can reorder a pipeline, because each
stage's output is a value the next stage consumed. The scans do not commute. Companion law:
**laziness across a scan ends at the first direction reversal** — a sink's sourcefix value
depends on every path from every source, so nothing is consumable at the sinks until the
entire forward pass has run; a hypothetically-lazy backward read of a sourcefixed dag would
stall its first pull while the whole pass ran underneath. Ruling (a) (scans materialize) is
this law made explicit: cross-direction composition forces full buffering anyway, so the
materialization boundary is honest where lazy pretense would only hide the cost (the
tree family shows the same law in miniature: Rootfix streams with DFT, Leaffix buffers;
`SinkfixScan` is implemented as forward-capture + reverse fold — the reversal's buffer made
visible).

## Streaming results that make the family worth building

1. **Sourcefix streams** (above): inflows complete at entry.
2. **Prunes stream**: liveness is a forward fold. A node is live iff at least one live
   in-edge from a live parent reaches it — decidable at entry. `PruneBefore` kills node +
   out-edges; `PruneAfter` keeps the node, cuts its out-edges; downstream liveness
   propagates. The spike's recomputed-reachability (a full re-walk) becomes one pass.

   **Prune is TEMPORAL, not spatial (clause ratified 2026-07-28).** "Before"/"after" mean
   before/after the node in TRAVERSAL order, so the prunes are dimension-relative: on the
   chain `A→B→C`, the 2×2 is forward-before(B) → `{A}`, forward-after(B) → `{A→B}` (B
   becomes a sink), backward-before(B) → `{C}`, backward-after(B) → `{B→C}` (B becomes a
   source) — same predicate, opposite halves removed, no spatial vocabulary needed
   (conformance-pinned, `DagPruneDirectionTests`). Off the chain the liveness caveat holds:
   only the matched node's EXCLUSIVE reach in the traversal direction dies. Surface
   corollary: the prunes therefore CANNOT get a composite (`IDagnumerable`) overload — a
   composite prune would present a DIFFERENT dag per dimension, violating the
   one-dag-two-presentations invariant. Dimension-relative operators either name their
   direction (the Sourcefix/Sinkfix pattern) or stay narrow; the backward prune flavors
   arrive as `Transpose().Prune…()` when Transpose lands (phase 4) — backward IS the
   transpose's forward, so one spelling suffices and the direction is explicit at the call
   site. Dimension-AGNOSTIC operators (`Select`, `Do`, `SelectEdges`, `PruneEdges` — an
   edge dies in both dimensions or neither) remain composite-eligible.

   **Prune may DISCONNECT the dag, and that is a legal outcome, not a hazard** (noted
   2026-07-28): multi-source, multi-component dags are first-class from birth (the
   two-islands corpus fixture), and the liveness fold guarantees the strong invariant that
   matters — survivors are exactly the live reach from surviving sources, so a forward
   prune can never orphan a node into an accidental NEW source (a node that loses its last
   live in-edge dies with it); new sinks (forward-after) and, in the backward dimension,
   new sources (backward-after) are deliberate outcomes, and component SPLITS (pruning a
   shared middle node under two sources) leave every stream/assembler invariant intact —
   topological order restricts to a valid order, ordinals persist as correlation keys with
   gaps, dispatch contiguity is untouched. Where disconnection carries meaning
   (conservation holds per component, not across them) it is domain semantics — the
   caller's business, per the general-purpose rule.
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
  (`DagEdge`/`DagParentEdge`); it adapts to the composite contract directly. Its spike-era
  operations are the independent implementations the conformance battery diffs against —
  the `EngineTree` role — and, like `EngineTree`, they live OUT of the product (relocated
  to the test project as the `Oracle*` extensions, 2026-07-27), so the product carries ONE
  spelling of every operator: the contract's. Cycle posture unchanged: construction-time acyclicity by wrapper-node linking,
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
  has, ending on the narrow tier. (Re-founding 2026-08-02: this store IS `DagBuffer` —
  the capture tier and the flat family are one type; "affords both" is spelled
  `Transpose()` rather than a second acquisition method.)
- **Adapter boundary**: foreign DAG-shaped data (the work import) enters through an adapter
  that must supply stable node keys — the quarantined identity bend. The contract's stream
  itself speaks ordinals only.

## What carries, what changes, what dies

| Tree concept | DAG fate |
|---|---|
| Preorder / level-order presentation | topological order |
| Scheduling / Visiting | Discover (per in-edge) / Enter (once) |
| `NodePosition` | per-edge context (payload, parent ordinal, per-parent index) + topo ordinal — no global coordinates, by theorem |
| DFT / BFT dimension split | no homolog shipped (re-founded 2026-08-02: backward = forward-of-transpose, an operator not a dimension; the true analog would be depth- vs level-biased topo order, deferred) |
| `Invert` | `Transpose()` — an OPERATOR/view (re-founded 2026-08-02): free on the buffer, cheap on the builder, absent on narrow forward-only streams |
| `Where` child promotion | contraction with caller edge-composition; not day-one |
| Prunes | forward liveness folds (streaming) |
| Rootfix family | forward, streaming; `SourcefixDispatch` carries edges natively |
| Leaffix family | backward; edge-aware `SinkfixDispatch` = the upward-diamond dual |
| `Select` / `Do` / `OrderChildrenBy` | carry directly (`SelectEdges` joins as the edge dual) |
| Flat stores + serializer | topo array + CSR adjacency; ordinal-referencing text format |
| Pretty print (`ToFormattedLines`) | ✅ carries (2026-07-28, + `ToFormattedString`): DFS expansion from sources, edge payloads on branch lines; SHARING is the new problem and ordinals the answer — in-degree ≥ 2 nodes tagged `#ordinal`, expanded once, `↺` references after |
| Set operations (`Union`, …) | DO NOT carry — they align by position; DAGs have none. Absent, not approximated |
| No node identity anywhere | bends ONCE, at the adapter boundary (sources must key their nodes); the stream uses ordinals |
| Two-phase strategies (`SkipNode`/`SkipDescendants`/`SkipSiblings`) | needs its own design — see open questions; skips become liveness votes, and per-EDGE skips (impossible on trees) want to exist |

## The edge dual (ratified 2026-07-28; tier 1 built)

The library stays GENERAL-PURPOSE -- no domain policy bakes in -- and the operator surface
completes symmetrically instead: **for every node operation, an edge operation where one is
meaningful.** The protocol was always ready (a Discover IS an edge visit; `SkipEdge` is the
per-edge strategy); this fills the operator column above it. The predicate/selector input is
the full relationship context (`DagEdgeContext`: parent, child, payload, in-edge index),
tracked by the shared `DagRelationshipTracker` (dispatch contiguity makes the parent the
last-entered node; O(1) state).

- ✅ **`SelectEdges`** -- payload map, streaming (a payload is a discovery-time fact); node
  values, structure, ordinals forwarded unchanged; conventional source discoveries carry no
  edge and publish default.
- ✅ **`PruneEdges`** -- ONE operator, no Before/After pair: that split distinguishes what
  happens to a matched NODE's dependents, and an edge has none -- removal is removal.
  Streaming via `SkipEdge` + the liveness fold; both endpoints untouched except through
  liveness. CONSTRAINT CAVEAT, stated on the operator too: pruning does not rebalance
  siblings. Where payloads form a constrained group (fractions summing to one -- a per-node
  in-edge group read as a DISTRIBUTION), weight-normalizing flow passes stay correct, but
  absolute-fact consumers see the broken group. Rebalancing (conditioning: drop an outcome,
  renormalize -- P(o|not GP) = P(o)/(1-P(GP))) is caller algebra over the GROUP, which is
  tier 2's business.
- ✅ **`SinkfixDispatchEdges`** (tier 2's first member, 2026-07-28) -- the group-scoped
  edge WRITER: what each survey dispatches BECOMES the result's edge payloads. Every
  non-source node is surveyed once in reverse topological order with its complete in-edge
  group as exactly-once targets (parent value + old payload) and its out-edges'
  already-rewritten payloads visible as inflows (the cascade; empty at sinks). Sources are
  never surveyed, yet every edge is written exactly once -- each edge is precisely one
  non-source node's in-edge. ZERO new types: the dispatch decoration trio is reused with
  `TDispatch = TEdgeResult`. This is where distribution algebra lives -- conditioning
  (drop an outcome, renormalize; the GP case, pinned with the sliver-owner fixture:
  fractions rewritten in place, the group still sums to one, lookthrough still fully
  accounted, money follows the conditioned edges), rebalancing, normalization -- all
  caller lambdas; the earlier `RewriteInEdges` sketch is subsumed and retired. Parallel
  edges rewrite distinctly by slot (per-(parent, child) cursor -- never by payload
  comparison).
- ✅ **`SourcefixDispatchEdges`** (2026-07-28) -- the downward mirror: every non-sink node
  surveyed once in forward topological order, its OUT-edge group as exactly-once targets
  (child value + old payload, out-edge order), its IN-edges' already-rewritten payloads
  visible as inflows (ancestors' cascade; empty at sources). Sinks never surveyed; every
  edge is exactly one non-sink node's out-edge. Simpler rebuild than its twin -- payloads
  written per parent's out-edge group index directly, no cursor. Division of labor between
  the twins, pinned: the upward twin owns in-edge GROUP algebra (conditioning,
  rebalancing); the downward twin owns path-cumulative edge values (effective ownership
  carried TO each edge -- the diamond's in-edges rewrite to 0.42/0.12, the 54% landing on
  the edges) -- which also makes the sketched `ScanEdges` flavors largely redundant: a
  cumulative scan-onto-edges IS a SourcefixDispatchEdges survey.
- **Tier 2 remaining (sketched):** the builder's edge-payload setter (mutation-tier
  completeness). The `ScanEdges` flavors are subsumed by the dispatch twins unless a
  streaming variant earns its keep.

## The arrival protocol (the successor model — direction ratified 2026-07-28)

> **Status:** direction ratified (Jason, 2026-07-28): the grouped model is the DEFAULT
> presentation, "for all the other reasons" recorded here. Phase 1 BUILT the same day: the
> grouping layer as an adapter over the existing protocol (`Arrivals/`,
> `GetArrivalDagnumerator`), conformance-pinned. The Discover/Enter protocol remains the
> shipped contract — DIG rides it — and the full migration is THICKET's decision, made
> here while the reasoning is fresh. **Vocabulary PROVISIONAL, pending ratification:**
> *arrival* (in-edge + far node) / *departure* (out-edge + far node) / *node event*.

**The observation (Jason's):** in a DAG, the natural unit of traversal is not the node or
the edge but the ARRIVAL — a node together with the in-edge that brought you (in-edge
relative to the traversal dimension). The existing protocol secretly agrees: a Discover IS
an (edge, far-node) tuple; the conventional source discovery is a node arriving on a
synthetic boundary edge; the seeded dispatch inflow is a value arriving on a default edge.
The type census proves it — `DagInflow`, `DagDispatchInflow`, `DagEdgeContext`, the seeded
inflows, the conventional discoveries: five spellings of one concept. Trees never surface
it because the in-edge is unique and implicit, which is why Treenumerable iterates nodes.

**The grouped event.** One element kind, one event per node, in topological order:
**(in-arrival group, node, out-departure group)** — arrivals carrying (dispatcher, edge
payload), departures presented as write/verdict slots. This is what every operator built
so far actually CONSUMES (scans take node+inflows; dispatch surveys take resolved
node+targets; the walk internally buffers pending arrival groups); Discover/Enter is the
ungrouped presentation of information whose every consumer immediately regroups it. The
event can only fire when the group completes — exactly when Enter fires today — so
Discover/Enter survives as the walk's internal bookkeeping, not the public stream.

**The dialogue survives as verdicts.** The protocol is a conversation, not an enumeration
(the union-model experiment located exactly this as the property that cannot be given up:
any model reducing the walk to passive `IEnumerable` iteration loses dispatch, which is
group-in, verdicts-and-writes-out). Per event the consumer may **sever arrivals** and
**suppress departures** — per-edge granularity in BOTH directions, subsuming
`SkipEdge`/`SkipOutEdges` and the dispatch-contiguity clause (which existed so consumers
could reconstruct groupings from the interleaved stream). **An event cannot be retracted**
(consumer verdicts shape only the future — the tree family's law): severing ALL of a
node's arrivals does not un-witness its event; it voids the node's departures, and the
liveness fold does the rest. Liveness stays a single fold in the walk.

**The bijection (Jason's, 2026-07-28), and why grouped wins anyway.** The grouped and
flat models are interconvertible — the flat (edge, node)-element model is node-only
traversal of the dag's **edge subdivision** (every edge reified as a payload-bearing node:
parent-node → edge-node → child-node), so every edge operation is some node operation on
the subdivided dag (`SelectEdges` = `Select` restricted to edge-nodes; the edge-dual
matrix is the node operators' image under subdivision). The discriminated-union element
type is just the subdivision's node type — the parity bit falls out of the construction,
no bleeding-edge C# required. But the bijection preserves information, NOT cost: grouped →
flat is a free streaming explode; flat → grouped requires reassembly and buffering (the
regrouping tax the current protocol pays inside every operator). Defaults sit where the
expensive direction never runs. Hence three rulings:

1. **The grouped event is the core model** — one element kind per dimension; transpose
   swaps the groups; the bijection commutes with transpose (subdivision is self-dual).
2. **The flat presentations are OPERATORS, not protocol** — `GetEdges` already ships the
   forward half; the full mixed stream, if ever wanted, is a trivial explode.
3. **`Subdivide()` deserves to be a first-class operator someday** —
   `G⟨TNode,TEdge⟩ → G⟨node-or-edge, Unit⟩`: the "edge info lives on nodes" simpler model,
   recovered COMPOSITIONALLY instead of impoverishing the core. Not day-one surface.

**Correspondence with the flat encoding:** the event shape IS the CSR row — a serialized
topological stream writes a node's out-edges with the node — so the protocol and the flat
store (phase 4) become the same picture. Engineering note for the migration: grouped
events hold lists, and the family resists per-node allocation, so the arrival/departure
buffers want to be walk-owned and reused, valid until the next event (the flat family's
read-struct discipline applied to the protocol). Phase 1's adapter allocates per event —
reference-walk posture, correctness first.

**Sources and sinks fall out:** a source's event has an empty arrival group (no
conventional-discovery convention needed — the convention existed to make the two-phase
stream well-formed); a sink's has an empty departure group. Disconnection and
multi-component dags need nothing special: components interleave in topological order.

## Open questions

1. ✅ **Naming RATIFIED (Jason, 2026-07-18)**: `IDagnumerable` / `IDagnumerator` — the pun
   carries the brand. Downstream: `DagnumeratorMode` (`DiscoveringNode` / `EnteringNode`),
   `IForwardDagnumerable` / `IBackwardDagnumerable`, `DagTraversalStrategies`. (The
   narrow pair retired 2026-08-02 with the dimension split — the re-founding; the
   surviving contract is `IDagnumerable`, forward-topological semantics.)
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
5. ✅ **Contraction's spelling RESOLVED (2026-08-08, the substitution taxonomy sitting —
   docs/SUBSTITUTION_TAXONOMY.md)**: spelled `Where` — the family homolog (vertex bypass
   with caller edge composition, LINQ polarity, bypass-not-removal: filtered sources
   promote their children to sources). `Contract` was rejected for accuracy (graph
   theory's contraction merges endpoints — the sources/sinks ruling's standard). Built
   capture-shaped; the streaming blocker is DISPATCH CONTIGUITY (a manufactured
   through-edge cites a long-closed parent block), logged in the taxonomy doc's ledger.
6. **Where the family lives**: grow `Copse.Dags` into contract + families, or split
   `Copse.Dags.Core` etc. mirroring the tree layering. Proposal: single project until
   graduation forces the split (the spike's own no-new-projects rule).
7. ✅ **The scan/dispatch return shape RATIFIED (Jason, 2026-07-18): option (a).** The
   theorem: a sourcefix result is an ENTRY-TIME fact (it needs every inflow), but the
   protocol publishes a node's value at its DISCOVERIES too, which precede entry — so
   `SourcefixScan` cannot honestly return a streaming `IForwardDagnumerable<TResult>`.
   (Trees dodge this: one in-edge, and the parent's accumulation is known at scheduling.)
   Ruling: the PASS streams (one walk, each node computed once, at entry); the RESULT is a
   materialized composite — a fresh builder `Dag` — fully composable and affording both
   dimensions (the materialization is an upgrade, `Memoize`-like). The spike's own scans
   already returned this shape; the laziness policy's documented-when-not clause, with the
   theorem as the documentation. Covers `SourcefixDispatch` (tree-side buffer precedent).
   **RE-RULED 2026-08-02 (the re-founding): the theorem and the materialization stand;
   the composite is the `DagBuffer` capture, not a builder `Dag` — the builder as return
   shape was the five-roles conflation, and it made results unaddressable.**
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
   ✅ 2b (2026-07-18): `SourcefixScan` — edge-paired inflows (`DagInflow` closes the spike's
   deferred pairing; empty at sources seeds the scan), one streaming pass into a
   materialized shape-isomorphic composite. `SourcefixDispatch` — the survey-shaped
   allocation pass: nodes resolve as `DagDispatchNode` (value + edge-paired inflows;
   sources get the single seeded inflow), surveys receive the COMPLETE live out-edge list
   as exactly-once `DagDispatchTarget` slots (unwritten/double-written throw — the strict
   ethos), outflows land as the targets' inflows. Only LIVE edges are surveyed, so
   `PruneBefore(blockers).SourcefixDispatch(...)` composes the blocker semantics into the
   allocation for free — the MoveMoney shape, streaming until the capture. Pinned:
   effective-ownership lookthrough through the diamond (60%x70% + 40%x30% = 54%), money
   movement with attribution (which amount arrived on which edge), prune composition,
   leaves-never-surveyed, slot strictness, and the builder-scan oracle differential.
3. ✅ (2026-07-18) The `Sinkfix` family — with one refinement the build surfaced: the
   sinkfix OPERATORS ride a FORWARD capture folded in reverse topological order, not the
   backward walk. Two reasons, both structural: a sinkfix result is children-first by
   definition, so the whole graph precedes the first result regardless of walk (the tree
   family's capture-then-fold pattern); and the result dags must be shape-isomorphic, but
   the backward stream cannot carry a node's out-edge ORDER (it carries in-edge order — the
   transpose's dispatch lists) — original orientation is a forward-stream fact. The
   backward dimension keeps its honest job: direct upward consumption, and the transpose
   view. (That "honest job" turned out to be nobody's job — no operator consumer ever
   arrived, which is what forced the 2026-08-02 re-founding: the dimension is retired,
   `Transpose()` the operator serves upward consumption.) `SinkfixScan`: edge-paired child results in out-edge order, empty at sinks seeds,
   shared child computed once but appearing per-edge in each parent's list — the diamond
   roll-up choice stays the caller's, documented. `SinkfixDispatch` CLOSES the deferred
   upward-diamond semantic: each node decides what travels up EACH in-edge (exactly-once
   targets, discovery order), so what a child sent up an edge IS that parent's share, by
   construction — no double count. No seed, by duality: downward's money is external to
   the dag; upward's holdings live in the nodes, so sinks just see empty upflows. Sources
   are never surveyed; their resolved inflows ARE the attribution. Pinned: the JV
   lookthrough (venture's 1000 arriving at the apex as 120-via-40% + 420-via-60% = 540)
   and THE DUALITY — the downward ownership scan times the holding equals the upward
   attribution, 54% of 1000, both ways.
3½. **The re-founding refactor (ratified 2026-08-02; SCOPE GREW 2026-08-05 — the
   constitution alignment executes in the same pass, one re-derivation per operator)**:
   collapse the interface trio to `IDagnumerable`; introduce `DagBuffer` (one-pass CSR
   capture, preserved source ordinals, dense indexing, lazy transpose adjacency) and
   `Transpose()` on buffer and builder; retarget the folds at pairing results
   (`DagScanResult` / the dispatch record) with destructured subject-keeping surveys and
   the virtual source family; DERIVE the sinkfix family through the transpose; split the
   Dispatcher's homes; reword the order promises to the readiness clause. Verification
   bar: the scenario suites move only mechanically (`ByName` should get SIMPLER —
   ordinal addressing replaces name-smuggling) plus the coherence battery (scan ≡
   fold-encoded dispatch; sinkfix ≡ transpose-derived).
4. Flat store + serializer + `Memoize`/`Materialize` — largely subsumed by 3½: the store
   IS `DagBuffer`; what remains is the text format and the narrow streaming deserialize.
4½. **The work-import adapter** — deliberately BEFORE further operator growth: the
   stream contract still has one implementation and no raw consumers; the adapter (the
   origin problem's actual shape, stable foreign keys quarantined at the boundary) is
   its pressure test.
5. Operator composition machinery (the tree family's architecture, transplanted).
   5b. The arrival protocol migration (the Thicket candidate — see its section).
   ✅ Phase 1 (2026-07-28): the grouping layer built as an adapter over the shipped
   protocol (`ArrivalDagnumerator` via `GetArrivalDagnumerator()` on any forward source —
   the bijection's cheap direction, made constructive), with sever/suppress verdicts, the
   layered liveness fold, and the event stream conformance-pinned (`ArrivalProtocolTests`)
   including the GetEdges-flattening equivalence. Native walk, buffer reuse, and operator
   migration are Thicket-tier work.
6. The showcase: the ownership-structure scenario suite grows into the flagship sample —
   real workload, both dimensions, allocation down and lookthrough up.
   ✅ SEEDED (2026-07-18, `OwnershipStructureScenarioTests`): two funds co-investing
   through a shared JV, a blocker, whole-cent largest-remainder allocation. Pinned:
   lookthrough fully accounted (1.0 everywhere — ownership neither leaks nor multiplies
   through the JV), per-fund views by pruning the other source, money down under both
   blocker policies (pruned: renormalization over live edges falls out of the design;
   receive-and-hold: the trap is visible) with conservation asserted end-to-end AND at
   every intermediate, and NAV attribution up with the funds' NAVs summing to total
   holdings — the diamond never double-counts. Every business rule is a composed lambda;
   every traversal is the library's.

## THE SUBGRAPH SELECTION CLUSTER (ratified 2026-08-06 — the first real-world ask)

Driven by the project-graph workload (the first field consumer): the boundary drains and
the closure selector, three operators, one file each.

- **`GetSources` / `GetSinks`** — the boundary drains, `GetRoots`/`GetLeaves`' dag
  analogs, both in topological order. `GetSources` is O(1)-state with an EARLY EXIT: the
  protocol's sources-at-the-start convention makes the source discoveries a stream
  prefix, and no wrapper ever creates a mid-stream source (pruning severs edges; the
  liveness fold kills what loses its last path), so the drain reads the prefix and
  stops. `GetSinks` consumes the whole walk (a sink is a whole-stream fact) but dispatch
  contiguity collapses the state to one pending node — O(1), the transpose's GetSources
  without the transpose.
- **`TakeDownstreamWhere(predicate)`** — the closure selector (`TakeSubtreesWhere`' dag
  analog; named `TakeSubgraphsWhere` at ratification — renamed 2026-08-09, see the
  flow-direction naming block below): every match, everything reachable from a match, and
  the edges among them — ONE result dag, the matches re-rooted. The tree operator's
  no-nested-matches flag is EMERGENT here, not a rule: a match reachable from another
  match keeps an in-closure in-edge and comes out interior; the result's sources are
  exactly the matches nothing else swept in (induced in-degree zero). Shared descendants
  are shared, never duplicated — a second path into included structure is an edge, not a
  copy. Outside edges die with their excluded parents; because inclusion is a downward
  closure, every included node's out-block survives whole (the compaction copies blocks,
  no per-edge test). Per-match separate closures are the caller's loop; ancestry
  selection is `TakeUpstreamWhere` (below; the conjugate spelling
  `Transpose().TakeDownstreamWhere(p).Transpose()` survives as the pinned law).
- **Capture-shaped BY CONTRACT, and the logged streaming amendment**: `TakeDownstreamWhere`
  returns a `DagBuffer` not for convenience but because the protocol discovers a
  stream's sources at the start of enumeration, and this operator's result-sources are
  found by the predicate mid-walk — a lazy wrapper cannot honestly present them.
  Membership itself IS online-decidable (entries strictly after their last discovery
  means every parent settles first — the topological presentation is the
  anti-re-entrancy invariant, and `PruneBefore`'s twenty-line wrapper is the proof the
  tier streams). The streaming variant therefore waits on ONE contract amendment,
  logged, not taken: restate the source convention as "a source's discovery precedes
  its entry; the BUILDER additionally presents all its sources first," licensing
  wrappers to surface sources mid-stream. Decide it on a workload, in its own sitting.
- **The tree analog (`TakeSubtreesWhere`), follow-on for main**: matched subtrees re-rooted
  as a result forest, outermost-match-wins as a RULE (the in-subtree flag — trees
  cannot share, so nested matches must be suppressed, not absorbed); D-narrow streams
  O(depth) (a matched subtree is contiguous in preorder), B captures (matches start at
  different source depths), F dimension-dispatches — Invert's disclosure pattern.
  Deferred to the tree family's own branch; the dag operator is the general form.

### Flow-direction naming + the upstream mirror (ratified 2026-08-09 — the viewer's closure sitting)

Driven by the ownership-viewer workload (the second field consumer): the service's three
closure questions — Above(x), Below(x), Structures(x) = one ancestor cone per sink Below(x)
reaches — made ancestry selection the hot path, and the transpose sandwich pays two full
buffer materializations around one mark-and-compact.

- **Naming RULED (Jason): the selectors say flow direction, not shape.** "A subgraph could
  be anything" — the word names a subset, not a direction, so `TakeSubgraphsWhere` leaned on
  a homology that doesn't transfer (on trees, *subtree* IS the descendant closure; *subgraph*
  carries nothing). The pair is now **`TakeDownstreamWhere`** / **`TakeUpstreamWhere`** —
  the house grammar's axis (sources, sinks, Sourcefix, Sinkfix: direction along the flow)
  and the practitioner vocabulary of build/dataflow systems. Ancestor/descendant was
  rejected (names the neighbors, not the flow; tree-flavored in a family that speaks
  source/sink); reachable/reaching was rejected (a near-invisible distinction carrying the
  entire meaning). The tree analog keeps `TakeSubtreesWhere` — *subtree* earns its name.
- **`TakeUpstreamWhere(predicate)`** — every match, everything that REACHES a match, and
  the edges among them; the matches come out the result's outlets. The emergence mirrors:
  a match that reaches another match keeps an in-closure out-edge and comes out interior;
  the result's SINKS are exactly the matches reaching no further match. Edges to outside
  die with their excluded children; every included node's in-edges survive whole (upward
  closure), but out-blocks do NOT close, so the compaction pays the per-edge test the
  downstream operator skips.
- **Implementation is the mirror sweep, not the sandwich**: one REVERSE-ordinal pass over
  the same out-CSR — dense ordinals are a topological order, so every child settles before
  its parent, and a node is included iff it matches or any out-target is included. Zero
  transposes, zero in-adjacency, same capture-in/capture-out contract (result sources are
  unknowable until the mark completes — the cluster's streaming argument verbatim). The
  law `TakeUpstreamWhere(p) ≡ Transpose().TakeDownstreamWhere(p).Transpose()` is pinned
  content-canonically in the battery.
- **The between-graph falls out as a composition**: `TakeDownstreamWhere(n == x)
  .TakeUpstreamWhere(n == sink)` — every path from x down to that sink and nothing else
  ("downstream of x, upstream of the sink"). Pinned in the battery as a composition test;
  no operator warranted.

## THE LAZY BUILDER RULING (ratified 2026-08-06 — eager validation drops; Materialize is the certificate)

**BUILT same day** (`BuilderDagnumerator`; the eager CSR acquisition deleted). Two
implementation facts refined the ruling as written:

1. **Acquisition keeps ONE light counting pass** (membership + member-in-degree, a
   visited-set walk over child edges — no ordering, no validation). The ruling's
   "AddChild maintains counts" didn't survive contact with the STRAY-PARENT affordance:
   a member may have a parent outside the dag whose edges are not the dag's, so
   in-degree is a REACHABILITY fact, uncomputable at construction. Everything after the
   counting pass is O(consumed).
2. **Ordinals are assigned at FIRST DISCOVERY** (dense in discovery order at the
   builder), amending the protocol's "strictly increasing along entries" clause: a lazy
   walk cannot cite a node's future entry index at discovery time. The contract's real
   promise is narrowed to what it always meant — a stable per-enumeration correlation
   key, entries in topological order; entry-indexed ordinals are the BUFFER's
   presentation (its dense index is its entry order). Entry discipline is depth-biased
   (each phase's newly ready nodes push in reverse) to match the eager walk's
   discovery-order bias — every pinned stream in the battery came through unchanged.

- **The builder goes lazy.** Builder acquisition previously precomputed the full CSR
  arrays — a smuggled buffer inside what the type presents as a stream, the exact tier
  violation the tree family's disclosure rule outlaws. The replacement is Kahn ON
  DEMAND: the visit protocol IS Kahn's trace (pop ready node = entry; dispatch
  out-edges = discoveries, decrementing children's remaining in-degrees; a child
  hitting zero joins the ready set), a hand-rolled state machine — no coroutine
  mystery. The walk-source set is the members nothing points to, so
  sources-at-the-start survives and `GetSources`' early exit works on CYCLIC graphs
  (it never reaches the starvation point).
- **Eager cycle validation drops, by the finiteness symmetry.** Eager cycle validation
  of a dag is the twin of eagerly validating that a lazy tree is finite — both are "can
  this stream complete" facts, discoverable only by full drain, and tree-side nobody
  demands the proof at acquisition. The symmetric posture: eager-validate NEITHER
  family; each contract fails where it actually fails. Trees never fail (the tree
  contract is unfalsifiable by content — a shared-reference stream is a lawful
  unfolding); a cyclic dag drain fails at STARVATION (queue empty, entered < built),
  throwing `DagCycleException` at exhaustion after publishing the maximal acyclic
  downward-closed prefix, deterministically per drain. The dag's node count is what
  converts the tree's divergence into detection — the one place this family improves on
  the tree posture instead of mirroring it.
- **Materialize IS the validator; the buffer is the certificate — and it certifies
  ITSELF.** A completed capture drained the walk and the walk did not starve; that value
  is acyclic permanently, regardless of what the builder did before, after, or during.
  Certificates attach to values, not to objects with identity through time — which is
  why no `ValidateNoCycles` operator ships (extensionally `Materialize`; the seed
  lesson: no existing instrument under a second name). Drain-without-residency
  validation is `Consume`'s seat (dag `Consume` does not exist yet; it arrives with the
  lazy builder — full drain, keep nothing, throw on starvation). Three postures, zero
  new vocabulary: drain validates, `Materialize` validates and keeps the certificate,
  `Consume` validates and discards.
- **The three-tier stability story.** The concrete builder is mutable, List-style, and
  keeps implementing `IDagnumerable` (the `List : IEnumerable` precedent; an explicit
  build boundary already exists under a better name — `Materialize`). The INTERFACE
  promises re-enumeration, not stability — a Defer-style dag source lawfully differs
  per drain, so "is acyclic" is a predicate of a DRAIN, never of a source. `Hide` gets
  its dag seat (the tree/Ix precedent): it launders identity — the consumer cannot cast
  back to the builder — but does NOT make the source stable; the owner still can mutate
  behind it. The buffer is the only tier where immutability is a promise. Builder
  guarantees nothing; Hide guarantees the consumer can't mutate; the buffer guarantees
  nobody can.
- **DEFERRED — the mutation guard (noted for future development, usefulness agreed
  2026-08-06).** Guards follow claims: a family guards exactly the falsifiable claims it
  makes. The dag family uniquely claims "starvation means cycle," and mid-drain
  `AddChild` can falsify it (an in-degree bumped under a walk's decrements can starve an
  acyclic graph — a FALSE `DagCycleException`; torn drains are merely odd, and the
  buffer's self-certificate is out of the blast radius entirely). The guard: builder
  version stamp, snapshotted at walk acquisition, checked per MoveNext, throwing
  `InvalidOperationException` on mid-drain mutation — one int compare, protecting the
  meaning of one word (`DagCycleException` ⇒ cycle, not cycle-or-mutation). No tree-side
  symmetry debt: trees make no falsifiable claims (a mid-mutation tree drain is a
  coherent walk of a Frankenstein tree, never a lie; BCL child collections self-guard).
  The tolerate-and-document alternative (exception reworded to "the drain starved")
  stays on record as the rejected-for-now road.
- **Parked separately: value-distinctness lint.** Edge validation (acyclicity) checks
  structure the library AUTHORED — validatable by its own machinery. Node validation
  (duplicate values at distinct positions/ordinals) checks identity the library NEVER
  READS — only a consumer-supplied comparer can lend it; duplicates never confuse the
  machinery in either family (positions/ordinals are the identity), so it is a lint on
  consumer intent, family-symmetric if ever built, waiting on a workload.

## ONE OPERATOR, ONE GRAIN (noted 2026-08-06 — Select / SelectEdges stay separate)

Revisited after the fused visit model (a discovery IS an in-edge and its target node,
published together) raised the question of whether separate node and edge projections
still earn their seats. Ruling: they do — the model fused the STREAM, not the grains.

- `Select` projects a NODE fact (one value per node); `SelectEdges` projects an EDGE
  fact (one payload per edge). A unified visit-level projection would sit at the visit
  grain, which is neither: the edge half fits (discoveries are edge-grained), but the
  node half becomes incoherent — a node with n in-edges would have its value projected
  per discovery with nothing obliging the projections to agree, an arrival-dependent
  fact wearing a node's seat. The same smuggling the recording rule and the seat rule
  exist to forbid, closed the same way: one operator per grain.
- The observable trace of the grain difference is CADENCE: `SelectEdges` evaluates
  ON-grain (exactly once per discovery, the edge's natural site), while `Select`
  evaluates OFF-grain by design (a node-grained projection run at visit cadence,
  n+1 times per node — the stateless wrapper's price, covered by the house purity
  contract's unspecified-counts clause). Cadence is the difference the fused model
  makes visible; the grains are why it is not a defect.
- The two-selector convenience overload `Select(nodeSelector, edgeSelector)` — now
  trivially one wrapper over the fused stream — is HELD at the admission gate: it is
  extensionally the `.Select(f).SelectEdges(g)` chain (an alias-shaped temptation), and
  the phase-5 composition machinery will fuse the chain mechanically anyway. `Select`
  keeps its name (node-value projection is what `Select` means family-wide); no
  `SelectNodes` rename for local symmetry.

## THE EDGE-PAIRING AMENDMENT (ratified and built 2026-08-06 — aggregation pairs, projection replaces)

Jason's challenge — why don't edges get the node treatment? — was correct by the
family's own rule, and the rule is now stated once: **projection replaces; aggregation
pairs.** `Select`/`SelectEdges` replace their channel's values lawfully (the consumer
authored the mapping; each output is derivable from its own input; keeping the old is
one closure away). The scans and dispatches pair, because their values are
FLOW-COMPUTED — path-cumulative, cascade-dependent — so the association between subject
and computed value must come from the API, assembled. The `DispatchEdges` twins were the
one aggregation in either family whose result replaced instead of paired — and the
machinery itself was the witness: the arrival seat pairs new value with old payload at
every survey (`DagDispatchInflow`), then the old result boundary discarded the pairing.

Built: both twins now return `DagBuffer<TNode, DagEdgeResult<TEdge, TDispatch>>` — each
edge's original payload with the value the survey dispatched along it, paired by the
machinery at the result boundary. Surveys are UNCHANGED (they still dispatch bare
`TDispatch`; pairing is the API's job, never the caller's). ONE pairing shape suffices:
an edge is 1-in-1-out, so its value is simultaneously the tail's outflow and the head's
arrival — `Accumulate` covers both readings, with no input/output split (contrast the
node side, whose 1-in-n-out asymmetry forced `DagScanResult` and `DagDispatchResult`
apart). Values traveling on project the pairing away — `.SelectEdges(e =>
e.Edge.Accumulate)` — the idiom the operator docs teach. `SelectEdges`/`PruneEdges` are
untouched: projection and filtering are not aggregations. The ownership workload gets
the pairing's payoff directly: a conditioned or flow-labeled edge carries its original
stake beside the computed value — nothing reconstructed, nothing smuggled.

## EDGE REPLACEMENT (ratified and built 2026-08-07 — ReplaceEdges; SelectMany stays reserved)

Born from the PoC's SIP diagnosis (path-dependent queries mean the model is missing
nodes; reify the anchors) and generalized by Jason from the drafted `ExpandEdgesWhere`:
the bespoke expander was the general operation plus a caller-side branch — the general
form is the operator, the special case is a lambda (the seed lesson, in reverse gear).

**The naming ruling (Jason, same day; briefly shipped as `SelectManyEdges`, renamed
before push):** this is NOT LINQ's bind and must not wear its name. `SelectMany` names
the collection monad's bind — element → collection, flattened by concatenation — and
consumers fluent in LINQ will predict those semantics; ours is endpoint-constrained
substitution, in situ. (The pedant's defense — paths over a quiver form the free
category, and edge→path substitution is Kleisli-flavored for THAT monad — is exactly
the kind of technically-true-but-misleading vocabulary the sources/sinks ruling
rejected.) More binding still: `SelectMany` is RESERVED in this codebase for the true
node-channel bind — `ITreenumerable` is constitutionally a tree monad, root-graft
substitution (SELECTMANY_DESIGN.md) is its designed bind, and a future dag node→subdag
substitution would inherit the name. Graph rewriting supplies the honest term of art:
EDGE REPLACEMENT. The operator family reads `SelectEdges` / `PruneEdges` /
`ReplaceEdges` / `ExpandEdgesWhere`.

- **`ReplaceEdges(selector)`**: every edge becomes the `DagEdgePath` the selector
  returns — an implicit-endpoint path: first payload, then one `(node, payload)` link
  per fresh interior node. `Keep` is identity (or a payload rewrite), `Through` is
  subdivision (the reify move), `Chain` generalizes, `Drop` (the default value) deletes.
- **One edge-removal semantics, not two**: `Drop` follows the family's liveness rule —
  a node losing its last inbound path dies unless it was an original source — so
  `PruneEdges` is exactly the replacement's streaming special case, and `SelectEdges`
  its streaming pure-rewrite special case. All three keep their seats: different cost
  classes, not aliases. A dead parent's edges are never consulted (pinned).
- **Cycle-safe by construction**: interior nodes are always FRESH (no value comparison,
  so no existing node can be referenced), and fresh nodes subdividing existing edges
  cannot close a cycle — the result buffer inherits its source's acyclicity certificate
  with no revalidation.
- **Stake placement matters for attribution** (the reify test's lesson): for per-anchor
  attribution the stake rides the leg BELOW the anchor — `Through(1.0, anchor, stake)` —
  the owner wholly owns its position, the position owns the stake. Total lookthrough is
  placement-invariant; per-anchor attribution is not (pinned).
- **Born-here ordinals**: interior nodes have no twin in the captured source, so their
  `SourceOrdinal` is −1 — synthesized-ness as an in-band queryable fact. (Chosen here
  provisionally; the convention is on the sitting's agenda with the ordinal-range
  amendment.)
- **Buffer by CONVENTION, not theorem** (Jason's catch, and the doc says so): streaming
  subdivision is visit-protocol-legal — contiguity and readiness both hold for an
  interposed entry-and-dispatch — and only ordinal minting blocks a wrapper form (a
  stream cannot know which ordinals are free). The reserved synthesized-ordinal-range
  amendment, bundled with the streaming-sources sitting, would make it streamable.
- **Family map**: tree `SelectMany` (SELECTMANY_DESIGN.md, root-graft) remains the
  designed node-channel bind, name reserved; `ReplaceEdges` is the rewriting-tier
  operation, dag-side first because the field workload asked. Placement of interior
  nodes is topological by construction (immediately after the parent), so the result's
  dense order needs no re-sort.
