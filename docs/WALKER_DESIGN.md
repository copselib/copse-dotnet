# Walker Design: The Adjacency Half

**Status:** Experimental — design conversation captured, no code yet.
**Branch:** `experimental/walker`
**Date:** 2026-08-10
**Prior art:** [jasonmcboyd/Treenumerable](https://github.com/jasonmcboyd/Treenumerable) (2016), the precursor library whose `ITreeWalker<T>` is the direct ancestor of this design. See [issue #16](https://github.com/jasonmcboyd/Treenumerable/issues/16) (September 2016) for the moment the streaming model that became Copse split off from it.

---

## 1. Origin: what the DAG branch uncovered

The `experimental/dag` work implemented `TakeUpstreamWhere` — start at a node, follow
out-edges, return the reachable sub-DAG — and the operation felt wrong inside the
streaming model. Chasing that discomfort led to a diagnosis that applies to trees as
much as DAGs.

**The streaming model works when the answer is a slice or a scan of one linear
order.** On trees this is true surprisingly often, but by structural accident: the
descendants of any node form a contiguous interval of the preorder stream, so
"query the subtree" collapses to "slice the stream." Ancestors were never
contiguous — which is why anything ancestor-flavored has always needed Rootfix
accumulation: an O(whole-tree) sweep with path state to answer what is really an
O(depth) navigation question. The tree hid half of this wart. The DAG exposes all
of it: **no topological order makes reachable sets contiguous in either
direction.** Query ≠ stream slice, structurally. There is no clever order to find;
the property does not exist.

The test that predicts which model serves an operation: **does the answer's size
scale with the structure, or with a neighborhood?**

- *Sweep-shaped* (answer scales with the structure): scans, dispatch,
  whole-structure rewrites, conservation checks. These stream beautifully — the
  DAG branch's successes (Sourcefix/Sinkfix, dispatch, the ownership showcase's
  conservation proofs) are all in this class.
- *Neighborhood-shaped* (answer scales with a neighborhood): upstream closure,
  ancestors, reachability membership, lowest common ancestor. Natural cost is
  O(|answer|); any single-sweep implementation costs O(|structure|) per query.

The streaming operator algebra stays exactly as it is. The walker is the second
half, not a replacement: **Copse is the algebra of orders (the structure visits
you); the walker is the algebra of adjacency (you visit the structure).** Flat
LINQ never needed the distinction because in a sequence the stream and the
adjacency coincide. Hierarchies are where they come apart.

The 2016 library turns out to have been the query half, written first: two
primitives (`GetChildren`, `GetAncestors`) plus dozens of derived extensions —
axes, measures, traversals. It was never replaced by Copse; the two are halves of
one whole.

## 2. Contract

### Primitives: single-step adjacency

| | Downward | Upward |
|---|---|---|
| Tree walker | `GetChildren` | `GetParent` |
| DAG walker | `GetInEdges` | `GetOutEdges` |

(Edge orientation follows the DAG branch's convention: out-edges point upstream,
so `GetParent` corresponds to `GetOutEdges`.)

**The collapse law.** The tree and DAG rows differ in type — nodes vs.
(edge, far-node) pairs — and this is a collapse, not a symmetry break: *on a tree,
every node has exactly one out-edge, so the node is its own out-edge identity.*
`GetParent` returning a bare node IS the degenerate form of `GetOutEdges`, the
same way the tree side never grew `TEdge` and should not. The adapter between the
twins maps edge ↔ (node, direction).

**Closures are derived, not primitive.** The old library took `GetAncestors` as a
primitive — a minor optimization for sources that naturally materialize the whole
ancestor chain. On a DAG, "ancestors" is a derived closure with dedup semantics
and must not be primitive. The optimization survives as a *capability*: an
optional interface the derived `GetAncestors` extension probes for
(`TryGetNonEnumeratedCount` pattern) — minimal primitives, opportunistic fast
paths.

### The indexed child axis (ruled 2026-08-10, built — supersedes the enumerator pull)

The child axis is INDEXED, not enumerated — the Silverlight/VisualTreeHelper
shape (`GetChildAt(node, childIndex)` / `GetRootAt(rootIndex)`, probes returning
`ChildResult` by value; 2016's `GetChildAt` name resurrected), minus that
shape's finiteness assumption: there is deliberately **no `GetChildCount`** on
the contract, because a probe is finite work per call whatever the fan-out while
a count diverges on a generator-backed provider with an unbounded child group
(counting is a derived extension under LINQ `Count()`'s divergence contract;
finite providers keep cheap counts as concrete members). Consequences: **no
adjacency call can allocate** — the measured lesson that put `TChildEnumerator`
on the engine (interface-typed child enumerators heap-allocate per node and tank
sweeps) is satisfied by construction; the third type parameter leaves the
contract (`IWalkableTreenumerable<TValue, TNode>`), so both layout walkables
share one interface, the organic escalation types cleanly with no facade, and
the swap-up probe has a nameable target. Receipts that the shape was latent: the
level-order store SPI already is it (`EnsureChildAvailable` +
`GetFirstChildIndex`); the preorder walkable rides a lazy CSR child index (~2n
ints, built in the same pass as its parent index — the honest-O(1)-indexer
precedent, since span-hopping child k is the O(k) indexer the codebase already
rejected as dishonest); and the address provider's native child operation is
append-an-index. The engine's `IChildEnumerator` pull protocol is untouched —
that is the hierarchical family's source adapter, a different job.

### The finiteness law, in the type system (ruled 2026-08-10, built)

*The walker's true requirement is adjacency, not materialization* — and that law
bakes into the types as the ORTHOGONALITY of two capabilities the library
already had. `IWalkableTreenumerable` says **adjacency** (you can navigate; no
finiteness claim — a native-adjacency walker may serve an infinite structure).
`ITreenumerableBuffer` says **capture** (owned, in-memory, finite, effect-free
replay). Their named intersection, `IWalkableTreenumerableBuffer`, is the
interchange citizen — and it is what the finite-izing escalation
(`MaterializeWalkable`, deferred per the lazy-Materialize law, preorder per the
adjacency-first rider) returns, so the signature itself says "adjacency was
manufactured AND the structure was captured." Native-adjacency providers
(calculated trees, object graphs, the address-walker) implement the walkable
interface alone: **the type's silence about buffer-ness is the infinity
permission.** Termination-hungry operations (a height, a whole-structure reify)
may constrain on the intersection, making "diverges on infinite trees" a compile
error — the traversal-dimension split's discipline, extended to the finiteness
axis. As with `ITreenumerableBuffer`'s effect-free clause, the capability is
declared, not proven: an implementation over an infinite structure is out of
contract, not merely exotic. (Mechanical note: the intersection lives in
Copse.Linq and its concrete citizen is a wrapper, for the same
dependency-direction reason `TreenumerableBuffer` is one. The capability
lattice — {dimensions} × {adjacency} × {capture} — mints interfaces only for
cells with citizens, per the dimension split's own restraint.)

### Positions and steps

The DAG branch ratified (edge, far-node) as the traversal atom because a stream
has no "you are here" — everything must be an event carrying both transition and
destination. A walker *has* a current position, so its ontology splits in two:

- **Positions are nodes.** The walker's state.
- **Steps are (edge, node) pairs.** The walker's transitions.

A labeled transition system. The starting position is not an arrival mysteriously
missing its via-edge; it is a state, and states never had edges. Starting "above
all sources" or "below all sinks" uses virtual positions — the ForestRoot
precedent on the tree side; a flow-network-style super-source/super-sink on the
DAG side. A virtual position can be occupied and stepped *from*, but never appears
as the far end of a real step.

### Node handles: minimum constraints, zero allocation

The old library used `T` as both node identity and node value — correct for its
best sources (calculated trees, object graphs) where adjacency is a function of
the value. Store-backed walkers break the conflation: the handle is an ordinal,
the value lives elsewhere.

- The contract is generic over an **opaque node type**; the adapter decides
  whether it is the value itself (calculated trees, object graphs) or a handle
  (stores, built DAGs).
- **No dedicated node object.** A `LinkedListNode`-style type forces consumers to
  build our representation and allocates per node by construction.
- **Providers are structs consumed through generic constraints** — the same
  struct-composed pattern the engines use — so a store-backed walker is
  (store, ordinal) with zero heap traffic and devirtualized adjacency calls. The
  delegate-taking form survives as one convenience provider: a struct wrapping
  the `Func`s.

## 3. The tower: region → walk → sequence

The central structural insight. An axis result is not one thing; it is three lazy
layers, each an *erasure* of the one above, each feeding an algebra that already
exists.

| Layer | What it is | Structure | Algebra that consumes it |
|---|---|---|---|
| **Region** | Lens-restricted adjacency universe — where you *may* go | Adjacency, no order | Lenses (the walker's own, and the only new machinery) |
| **Walk** | Region + committed order, streamed as steps | Order | **The Copse operator algebra** — a walk IS a treenumerable/dagnumerable |
| **Sequence** | Walk with structure erased | Flat | LINQ |

- `Upstream(n)` is a **region**: a sub-DAG-shaped restriction, composable with
  further lenses (`Upstream(n).Where(p)`), nothing enumerated yet.
- *Walking* a region commits an order (depth-first, level-order, topological) and
  streams (edge, node) steps — which is literally the streaming tier's atom,
  produced lazily. **A walk is a deferred streaming structure**, so the entire
  operator algebra applies mid-structure: `Rootfix` over `Descendants(n)` is a
  neighborhood-priced scan neither half could express alone.
- Flattening erases structure and yields `IEnumerable` for LINQ.

Consequences:

- **One walker type, closed under lenses.** "Are views walkers?" and "can walkers
  be views?" have the same answer: there are no two types. A walker is a provider
  plus a lens stack; the base walker is the identity lens. Every treenumerable is
  already a view over another treenumerable — this is the third instance of the
  pattern after LINQ and the streaming tier.
- **The walker needs no operator algebra of its own.** Regions compose with
  lenses; walks compose with Copse; sequences compose with LINQ. Its job is to
  produce inputs for the two algebras that already exist.
- **The old library skipped the middle layer because it had to.**
  `PreOrderTraversal(walker, node)` went region → `IEnumerable` in one step;
  in 2016 no algebra could consume an ordered-but-structured stream. Copse is
  that algebra. The port inserts Copse between the walker and LINQ.
- **The streaming tier is the degenerate tower**: the *everything* region, walked
  from the virtual root, in an engine-chosen order.
- `TakeUpstreamWhere` as currently implemented on the DAG branch is
  region → reify with the query and the reification coupled — the walker-tier
  equivalent of a LINQ operator that secretly calls `ToList`. It decomposes into
  `walker.Upstream(n).Where(p)` (region) + `.ToDag()` (explicit reification at
  the boundary).

## 4. Cost model

With one walker type, the view/structure distinction relocates from the type
system to the cost model — where it belongs. Costs must be legible in the API,
not hidden.

### Lens cost classes

| Class | Examples | Cost |
|---|---|---|
| Free | Transpose | Swap `GetInEdges`/`GetOutEdges` — two method references trade places. (Contrast: Transpose in the order algebra is a hard pending problem. Same duality signature as ancestors: hard in one algebra, trivial in the other.) |
| O(1) per step | Value projection; edge-predicate restriction | Struct lens, no state |
| Memoized | Closure membership (`Upstream(n)`) | O(reached) monotonic memo, amortized; one allocation per view, never per step |
| Lookthrough | Contraction ("skip filtered nodes, connect survivors transitively") | Lazy is possible but per-step cost compounds as lenses stack — the buffer's territory |

### Order-commit costs

Committing an order is not always free: a depth-first walk is O(1)-state-per-step
over the lens, but a *topological* walk of a region needs in-degree accounting
within the region, which drags in the membership memo. Order choice has a price
column, same as lenses.

### The buffer is the walker tier's Memoize

The laziness policy — *compose without materialization when possible, documented
when not* — is tier-generic. Lens composition is the default; when a stack goes
lookthrough-heavy or a view will be queried repeatedly, reify once and get
flat-store step costs thereafter. The resulting buffer is the **interchange
citizen**: it carries both surfaces (walkable and streamable), so materializing a
view is simultaneously how an adjacency query re-enters the streaming algebra.

**How the escalation chooses its layout** (ruled 2026-08-10): the walker mints
no new knob — it inherits `Materialize`'s pair. The organic form
(`MaterializeWalkable()`) wraps whatever layout the source's story produced; a
completed buffer keeps its `NativeLayout`. Both layouts having walkable
citizens means the organic form always succeeds without transposing, promising
walkability but not a particular axis-cost profile.

**Materialize goes lazy** (ruled 2026-08-10, and LANDED on main the same day —
see the operator surface map's Materialize row and MaterializeTests): both forms
defer construction to first pull, under one law — *construction is uniformly
lazy; the pin is a commitment made at the earliest moment it is free.*

- `Materialize()`: pin AND construction at first pull — first pull is the
  earliest moment the pin is knowable. The organic rule becomes one sentence
  everywhere, plain pipelines and live memos alike: **the first consumer pins
  the layout.** (Today's silent preorder guess, and its wart — a BFT-first
  consumer served cross-order forever — both disappear.)
- `Materialize(layout)`: construction at first pull, pin at call — the pin
  is free at call time (the `Pin` helper acquires and disposes, pulling zero
  nodes), and pinning early preserves native-capture odds on a shared live
  memo that another consumer might pin differently before first pull. "The
  argument is never ignored" lands at call; the O(n) lands at first use.
- Walkable rider: treenumerator acquisitions pin their dimension; an
  adjacency-first use (`GetParent`, `GetRootEnumerator`, …) names no dimension
  and pins the walker default, preorder — the ancestry-cheap layout, the axis
  the walker uniquely adds. In practice the realistic first act is the
  handle-acquisition sweep, which pins its own dimension organically.
- Accompanying moves: `ITreenumerableBuffer`'s non-disposability survives with
  reworded justification ("holds only managed arrays once consumed; until
  then, a pinned deferral" — an unconsumed lazy buffer is exactly as leaky as
  the unconsumed pipeline the caller already had, since nothing opens until
  first pull); timing of the at-most-once source enumeration moves to first
  pull (release-notes flag); benchmark setups that materialize-then-measure
  need auditing before the flip (capture cost moves into the first measured
  iteration; warmup absorbs it at steady state). Memoize stays a distinct
  mechanism: incremental growth with a live disposable feed, against
  Materialize's bulk capture at one deferred moment. The deliberate form
(`MaterializeWalkable(BufferLayout)`) is the escape hatch for callers
who know their query shape, riding `Materialize(layout)`'s never-ignored
guarantee with its transpose-from-the-buffer fallback. The parameter speaks
STORAGE vocabulary — `BufferLayout`, retyped from `TreeTraversalStrategy` on
main 2026-08-10 after the operator's opening conversion line gave the wrong
vocabulary away — which suits the walker doubly: the layout is the deliverable,
and the layout is what the walker caller actually reasons about (the axis-cost
table is indexed by encoding, not by traversal dimension). The choice is
cost-only (the walkable twins are conformance-pinned to present the identical
tree) and revisable at O(n) from the buffer. Near-term surface note: the two
walkables have different child-enumerator types, so the concrete API is
layout-named methods (the SimpleSerializer precedent) with the layout-argument
form as the convenience over them.

### Adapter asymmetry

- **Walker → Treenumerable is free.** Any walker can emit a walk; adjacency is
  strictly the stronger capability.
- **Treenumerable → Walker costs a materialization**, explicitly. `ITreenumerable`
  / `ITreenumerator` deliberately have no node identity and no edge access; that
  absence is what buys O(frontier) state and composition without materialization.
  The core contracts do not change. The deferred surface simply does not offer
  navigation — you ask for a store first.
- **A buffer is not automatically a walker backing.** A traversal buffer is a
  *recording of a traversal* (cheap re-enumeration of one linear order); a walker
  needs an *index of the structure* (random access by node plus both-direction
  adjacency). Both are O(n) memory; they are different artifacts. Stores are the
  natural providers — node identity by ordinal, parent index one O(n) pass at
  materialization. The DAG contract already grew its walker organs under DIG
  pressure (`GetEdges`, `InEdgeIndex`, the topological-order value view); the
  walker gives them a principled home.
- The walker's true requirement is **adjacency, not materialization** — the 2016
  library's calculated trees (Collatz) walked with no materialization anywhere,
  because adjacency was a function. Materialization is just how you manufacture
  adjacency when all you have is a stream. Providers: stores and built DAGs,
  native object graphs with parent pointers, calculated structures.

## 5. Semantics and invariants

### Read-only, precisely

> Given `var walker = graph.GetWalker()`, the walker never mutates the topology
> of `graph`, even if `graph` is a buffer that could be mutated.

That is the weaker half. The full invariant pairs it with its converse:

1. **The walker never writes topology.**
2. **The walker assumes topology is stable for its lifetime.** Memoized closure
   views, snapshot-relative adjacency, and cached reachable sets are silently
   corrupted by a topology change from *anyone*. Enforced the cheap .NET way:
   fail-fast version stamps (the `List<T>` enumerator pattern) on any genuinely
   mutable backing.

Everything else sorts by one distinction — *the graph* vs. *the walk*:

- **Mutating node values** through a traversed axis is the consumer's business.
  The walker hands out references; LINQ does not stop a setter inside `foreach`
  either. Values are not topology.
- **Producing new graphs** is not mutation. Reification is pure; the source is
  untouched.
- **Skipping nodes** is a filtered view — it changes the itinerary, not the
  graph.

So: *non-mutating and stability-assuming with respect to source topology;
agnostic about values; free to produce new structures; free to shape its own
itinerary.*

### Set semantics vs. path semantics

On a tree, "the ancestors of n" and "the path from n to the root" are the same
enumeration. On a DAG they diverge violently: upstream-*set* is O(reached);
upstream-*paths* is exponential (the old library's `GetBranches` ported naively
to a DAG is a bomb). Rule: **axes default to set semantics** with per-node dedup
(the DAG branch's grouped-arrival model answers exactly this); path-flavored
operations get loud, distinct names with documented cost. Trees let these
coincide silently for ten years; the DAG contract must choose, and the choice
must be legible in the API surface.

### Provenance (open decision)

Does reification remember where it came from? A buffer built from
`Upstream(n).Where(p)` can be *detached* (fresh ordinals, no history) or carry
*provenance* (each node keeps its source handle), making the materialized
structure a view in the informational sense — walk the cheap frozen copy, re-enter
the full live graph at any node. The ownership showcase wants this ("materialize
the blocker subgraph, analyze it, jump back in at the offending node"). Handles
are ordinals, so provenance is nearly free to carry and impossible to reconstruct
if dropped. Leaning: **provenance by default, detached as the variant** — the
fail-fast staleness rule gives detached buffers their reason to exist, since
provenance into a mutated source is exactly the corruption we refuse.

## 6. Naming

- Resurrect **`ITreeWalker`** — the 2016 name, our own heritage, and honest about
  what it is. The DAG twin follows the same stem.
- **Not "Visitor."** GoF Visitor is double-dispatch over heterogeneous node
  types; this is a navigator with axes. Prior art to steal vocabulary from:
  XPath axes (`ancestor`, `descendant`, `following-sibling` — proof that tree
  querying is axes + predicates + composition) and Gremlin steps (`out()`,
  `in()`, `both()`, `repeat().until()`) — steps that consult edge direction as
  data rather than being driven by it.

## 7. Next step: the use-case catalog

Before any contract is written: **identify dozens of use cases and mock up the
calling code.** Sources:

- Every extension method in the 2016 library — each one a use case with ten
  years of validation (axes, measures, traversals, `GetBranches`).
- The ownership showcase's DAG-native queries: upstream blocker discovery, NAV
  attribution back-tracing.
- XPath's axis list as a completeness checklist.
- Walker-only classics no stream can serve: lowest common ancestor,
  is-descendant-of, distance between nodes.

Each mocked call site gets classified by **which layer of the tower it consumes
at** — region (keep navigating), walk (scan/prune/dispatch from here), or
sequence (LINQ and done). If call sites cluster at all three layers, the tower is
right. If one layer sits empty, the design invented a floor nobody lives on.
Each call site also answers: *would this code want to re-enter the source graph
afterward?* — turning the provenance instinct into evidence.

### Other open questions

- Does the walker share the prune/verdict vocabulary with the streaming tier
  literally, or get its own lens vocabulary? (Leaning: share — the temporal
  clauses on prunes transfer.)
- Return-type story per bucket: axes → sequences, closures → regions, measures →
  scalars — to be validated by the catalog, not assumed.
- Which order-commit choices the walk layer offers (depth-first, level-order,
  topological), and how their price columns are surfaced.
