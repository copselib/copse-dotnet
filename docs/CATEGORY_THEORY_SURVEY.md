# Category-Theoretic Survey

**Status:** Phase 1 of the categorical audit — the inventory. Shapes assigned, laws owed,
pin status recorded; no code changes. Operator facts verified against
[OPERATOR_SURFACE_MAP.md](OPERATOR_SURFACE_MAP.md) (2026-08-12).
**Branch:** `experimental/walker` (movable; the sweep spans both tiers).
**Companions:** [WALKER_DESIGN.md](WALKER_DESIGN.md) (the walker tier),
[SELECTMANY_DESIGN.md](SELECTMANY_DESIGN.md) (the monad's missing bind),
[OPERATOR_COMPOSITION_DESIGN.md](OPERATOR_COMPOSITION_DESIGN.md) (the collapse lattice this
survey licenses).

---

## 1. Method: the two axes, and what equality means

**The doctrine (compass, not law).** Every operation has a *semantic shape* (constructing /
observing / neither — category theory has more shapes than two, and a healthy library uses
several) and a *cost shape* (sweep-priced / neighborhood-priced). The tiers are the cost
axis; the shapes below are the semantic axis. Friction historically appeared exactly where
an operation's home fought one of the axes (the founding example: `TakeUpstreamWhere` on
the DAG branch — observation-shaped AND neighborhood-priced, jammed into a streaming
surface). The audit's job is to make every operation's shape explicit, so tenancy is
visible and admission of new operations starts with "name the shape."

**The representation, and the quotient.** `ITreenumerable<T>` is the treenumerator-factory
monad — the carrier is (a pair of) `() → ITreenumerator<T>` (the founding insight; the
carrier intro is `DelegatingTreenumerable(bftFactory, dftFactory)`). This factors every
operator into a categorically boring *thunk layer* (factories wrapping factories —
reader/thunk plumbing) over an interesting *stream-level transformer*. **All laws in this
survey are stated modulo visit-stream equivalence** — the thunk layer quotiented out —
which is precisely the equality `VisitStreamConformance` has always used. In coalgebraic
terms that equivalence is **bisimulation** (Rutten), and the conformance batteries are
bisimulation tests. The house chose the theoretically correct equality before the theory
arrived.

**The purity condition.** The thunk layer's laws hold only for reproduction-stable
factories — which is why coldness is a *contract* (`Tree.Defer`'s per-acquisition
semantics, the serializer's re-parse disclosure) and why effectful sources (`Tree.Using`,
readers) carry special documentation: they are the managed strain points of the
representation's assumption.

**The identity axiom (named 2026-08-12, during review).** The no-node-equality pledge is
the *negative* half of a principle whose positive half organizes this whole survey: **in
Copse, identity is positional, never valuational** — values are labels, position is who
you are. Who leans on it: the set ops hardest (alignment IS identity there), the
positional operator flavors, the conformance quotient itself, and the walker's handles
(positional identity made portable — ordinals are positions in an encoding, addresses are
position-chains). Where it pays: every failure this audit found is the same collision —
positional identity meeting a reshaping that moves positions (Δ-associativity, the
SelectMany forest attachment, positional-Where's non-composition, the walk incoherence).
The audit is, in effect, a map of where the axiom holds for free and where it charges.

## 2. The shape taxonomy

| Shape | Definition (working level) | Laws owed |
|---|---|---|
| **Functor map** | relabel values, structure untouched | identity (`Select(x => x)` ≡ id); composition (`Select(f).Select(g)` ≡ `Select(g∘f)`) |
| **Monad** (return/bind) | graft generated structure per value | left/right identity, associativity (CLAUDE.md already demands these) |
| **Monad zero** | `Tree.Empty` as the zero of the monad-with-zero | `bind(Empty, f) ≡ Empty`; the Empty-graft rule (§6) |
| **Fold / catamorphism** | consume the structure to a value/sequence | uniqueness-style: fold respects the structure map; composition-with-map laws |
| **Scan-extend** | relabel every node by a *neighborhood observation* that factors through a fold along the traversal order — comonadic semantics, sweep-housed (deliberate cross-tenant) | coherence across tiers; agreement with true `extend` when it exists |
| **Zip / monoidal** | lockstep co-traversal of two sources | associativity; identity (`Empty`); commutativity where claimed |
| **Natural transformation** | value-independent structure map | naturality: commutes with `Select` |
| **Reshaping (local)** | structure change driven by per-value decisions | bind-derivability under a chosen Empty/graft rule (§6); interchange with `Select` |
| **Reshaping (non-local / order-sensitive)** | structure change driven by identity, position, or traversal order. **The litmus test (2026-08-12): can the operation be defined as before-drawing → after-drawing without mentioning encounter order?** Yes → a tree transformation (order-words referring to the drawing's own structure — sibling order, root order — are fine: both traversals agree on them). No — the definition needs "until"/"while"/"encounter" → a *walk* transformation: each dimension modifies its own itinerary, the result's DFT and BFT can disagree about which nodes exist, and no single tree produces both answers | outside bind by nature (bind's raw materials are the drawing; everything it builds is a drawing); laws are per-operation |
| **Co-Kleisli query** | a function *from* a focus (walkable + handle) | composition via the Store comonad |
| **Representation morphism** | change how the same tree is carried (capture, encode, decode) | **identity modulo the quotient** — the strongest law on the surface |
| **Machine / coalgebra** | the treenumerator itself: a stateful dialogue | bisimulation as equality; precise home §7 |

## 3. Inventory — the streaming tier

Pin status: **PINNED** (a test asserts the law), *implicit* (the implementation relies on
it; no direct semantic pin), UNPINNED (owed, untested).

| Operator | Shape | Laws & status |
|---|---|---|
| `Select` | functor map | identity AND composition: **PINNED** (`CategoricalLawTests`, 2026-08-12 — composition pinned semantically with the stacked side forced via `Hide`, separately from the collapse behavior `CompositionTests` pins) |
| `Where` (value flavor) | local reshaping; bind-candidate under the *promote* Empty rule | predicate merge `Where(p).Where(q)` ≡ `Where(p∧q)`: **PINNED** (`CategoricalLawTests`, Hide-forced); interchange with Select (licenses `SelectThenWhere`): **PINNED** (same); bind-derivability: pending §6 |
| `Where` (positional flavor) | reshaping over the position-decorated tree | Two claims, opposite statuses (review 2026-08-12): the SEMANTIC merge law is impossible at the signature — the intermediate position is a NON-LOCAL fact (depends on what the first filter removed), and a `(value, position)` predicate is local; positions are extend-flavored observations of the CURRENT shape, so reshaping invalidates them ("each layer sees its input's labels" is the theorem, LINQ's indexed-Where made the same call). The MECHANICAL collapse is possible and on the table under the do-the-hard-thing rule: a single-pass driver maintaining k position channels for k stacked layers (the existing Where already carries two labelings per node — `OriginalPosition` + `Position` — this generalizes it), pinned ≡ the stacked pair. Admission bar: a profiled workload where the stack is hot — the tier-seal lesson says collapses must earn their complexity, and this one buys performance, not algebra (nothing in the monad/comonad/lattice completeness waits on it) |
| `PruneBefore` / `PruneAfter` | local reshapings; bind-candidates (*vanish* rule / *slotless-leaf* rule) | prune-over-prune merge (OR-disjunction), both operators: **PINNED** (`CategoricalLawTests`, Hide-forced — the in-tier composition of TIER SEAL 2026-08-04 now has its licensing law named); bind-derivability: pending §6 |
| `TakeNodesUntil/While` | **order-sensitive truncation** — an operation on the *walk*, not the tree: with O(1) state each dimension truncates its own order, so the result's DFT and BFT streams can disagree about which nodes exist — not a coherent quotient citizen at all, a pair of truncated walks. Walk-floor citizens named before the walk floor existed | per-op semantics; no monad laws owed — and not for lack of trying: bind is per-value and order-blind (LINQ's own `TakeWhile` is likewise not bind-derivable; truncation is fold-shaped). *(Row split 2026-08-12 — the takes were over-lumped; Jason's review caught it.)* **FLAGGED 2026-08-12 (surface map flag 7, ruling pending): the composite F overload is the incoherence-manufacturer — narrow D/B forms are coherent one-walk citizens; options are chop / narrow-only / re-home as a walker-tier verb / keep with a pinned incoherence detector** |
| `TakeTrees` / `SkipTrees` / `TakeLast/SkipLastTrees` | **positional reshaping at the root level** — a forest prefix/suffix is `siblingIndex`-driven, and sibling order is *intrinsic tree structure* shared by both dimensions: coherent, quotient-respecting, dimension-agreeing (reclassified out of the walk family 2026-08-12) | per-op semantics; the surface map documents them as sugar over take/prune, which is the classification made literal — `TakeTrees(k)` is a root-level positional prune |
| `TakeSubtreesWhere` (main; landed post-fork, so this branch lacks it and the inventory missed it until 2026-08-12 — Jason's observation supplied the row) | **filtered duplicate, poured** — the comonad's `duplicate` (every node ↦ the subtree at it) with the labels filtered by the predicate and the survivors poured out as the result. A cross-tenant citizen in the scans' mold: on the drawing/reading litmus it passes as a coherent tree transformation (erase all but the matched subtrees, re-root them in document order — no encounter-order words needed), yet its content is comonadic (the emitted things are vantages, not values). The ratified OUTERMOST-MATCH-WINS rule (2026-08-06) is **pour policy, not comonadic core**: pure filtered duplicate emits ALL matching vantages, nested included — but a tree buffer cannot hold overlapping labels (a tree cannot share substructure), so the tree carrier must suppress nested matches *by rule*. The dag analog `TakeDownstreamWhere` needs no rule (its own doc: outermost is EMERGENT — closure union makes a nested match an interior node; sharing absorbs overlap). The walker carrier faces no question at all: `GetHandles().Where(h => p(GetValue(h)))` is a sequence of vantages-by-reference where nested matches coexist free, O(1) per match against the buffer's O(result) copy. Three carriers, one core; what varies is where the selected vantages land | pour policies are per-carrier semantics (each operator's doc); the comonadic core inherits the pinned extend laws; the walker spelling's equivalence to the buffered operator (modulo pour policy) is unpinned — a natural pin once the branches reunify | zip / monoidal — **and the family REDUCES to one primitive** (discovered in the phase-2 close, 2026-08-12): `Intersection = Union.PruneBefore(!both)` (the *vanish* rule), `SymmetricDifference = Union.Where(!both)` and `Subtract = Union.Where(!HasRight).Select(.Left)` (the *promote* rule) — the Empty-graft fork of §6 is already living in the shipped set ops, one derived operator per rule | Union `Empty` identities + associativity up to reassociation + commutativity up to swap: **PINNED** (`CategoricalLawTests`); Intersection annihilation + commutativity up to swap: **PINNED**; Subtract right-identity + left-annihilator: **PINNED**; SymmetricDifference `Empty` identities both sides + commutativity up to swap: **PINNED**. **Non-law, documented:** Δ-associativity is NOT owed — tree-Δ rides the promote rule, promotion shifts positions, the set-theoretic xor law does not transfer to positional merges (same class as positional Where's non-composition) |
| `StructuralMerge` | zip family — and Union IS the structural merge ("the engine behind the other set ops") | associativity: **PINNED** via Union's law |
| `Do` | **effectful map (Kleisli into the effect layer)** — the operator that REFINES the equivalence relation: the survey's laws hold modulo visit-stream equivalence, and effects are exactly what that quotient cannot see, so collapse across `Do` fails *by theorem*, not by fiat ("the window materializes the pane" — an observer invalidates optimizations that were only valid up to observational equivalence) | in the finer (effect-trace) setting: `Do(a).Do(b)` ≡ `Do(a then b)` (adjacent observers merge, order preserved — pinned on streams AND effect traces) and `Do(noop)` ≡ id: **PINNED** (`CategoricalLawTests`, 2026-08-12); effects-per-drain = the cold contract extended to the effect layer (documented). Landing idiom pinned (`DoLandingCompositionTests`). *(Reclassified 2026-08-12 from "deviation done right" — the non-composition is a theorem about quotient refinement, which is better than a documented deviation.)* |
| `Hide` | **opaque identity** — a representation morphism (identity modulo the quotient) whose purpose is refusing to advertise its concrete type, so the lattice's composite-first probes miss and stacked behavior is forced (the tests' isolation tool) | identity law ≡ id modulo quotient: *implicit* (its uses in the composition batteries depend on it); distinct shape from `Do` — split from the shared row 2026-08-12 |
| `RootfixScan` | scan-extend (inherited-attribute evaluation — Knuth's attribute grammars: inherited = rootfix) | cross-tier coherence `Scan(boundary, fold)` ≡ fold-encoded `Dispatch(boundary)`: **PINNED** (`CrossTierCoherenceTests`) |
| `LeaffixScan` / `LeaffixDispatch` | scan-extend, upward (synthesized attributes = leaffix); dispatch = the sibling-complete survey tier | same coherence family: **PINNED**; the seat rules and boundary instruments are the operational shadow of extend's neighborhood being order-restricted |
| `RootfixDispatch` | scan-extend, downward survey tier | coherence: **PINNED**; full-participation/boundary rules documented |
| `RootfixAggregate` / `LeaffixAggregate` | fold ∘ scan-extend | derivability from scan + leaves: *implicit* by construction |
| `Invert` | **natural transformation** (value-independent) | subsumption `Invert` ≡ `OrderChildrenByDescending`(source sibling index): **PINNED** (`OrderChildrenByTests`); involution `Invert∘Invert ≡ id` AND naturality `Invert∘Select(f)` ≡ `Select(f)∘Invert`: **PINNED** (`CategoricalLawTests`, 2026-08-12) |
| `OrderChildrenBy` | key-driven structure map (not natural — depends on values through the key) | stability documented; subsumption law shared with Invert: **PINNED** |
| `Memoize` / `Materialize` | **representation morphisms** — identities modulo the quotient (tabulation-adjacent) | replay ≡ source: **PINNED** (conformance batteries; memo replays and serializer round-trips ride `VisitStreamConformance`) — the surface's strongest law, already enforced |
| `Consume`, `AnyNodes`/`AllNodes`/`CountNodes`/`CountTrees`, traversal enumerables, `GetLeaves`/`GetLevels`/`GetBranches` | folds / drains | fold-respects-structure: *implicit* via conformance of the streams they consume |
| `ToFormattedLines` / `ToFormattedString` | fold to rendering | golden-pinned (`FormattedLinesTests`) |
| `ToDegenerateTree` / `ToTrivialForest` | **embeddings** — two functors List → Tree (the chain and the flat forest) | functoriality: **PINNED** (`CategoricalLawTests`) |
| `Tree.Empty` | the monad zero | zero laws: pending bind (§6) |
| `Tree.Defer` / `Lazy` / `Using` | thunk-layer citizens (call-by-name / call-by-need / bracketed resource) | the representation's purity conditions — documented as contracts |
| Serializer (De/Serialize) | representation morphisms (encode/decode) | round-trip ≡ id modulo quotient: **PINNED** (round-trip conformance) |
| `ExpandNode` / `Graft` (Experimental) | **bind fragments** — pieces of the monad's missing `SelectMany` | none until adopted by §6 (their half-baked state is *explained*: they are jigsaw pieces of one canonical operation) |

Corrections this survey bakes in from the audit discussions: the reshapings are NOT
categorically homeless — the local trio are bind-candidates (list `filter` has always been
monadic); the takes are order-sensitive and hence stream-level by *nature*, not by
accident; `Do`'s refusal to compose is a documented deviation, which is the correct way to
be lawless.

## 4. Inventory — the walker tier

The comonad is the **focused pair** (walkable, handle) — the **Store comonad**,
`Store s a = (s → a, s)` with `s = THandle` and the adjacency probes as the decoration
that makes the position space tree-structured. (The codebase named its flat backings
"stores" years before this identification; Store-the-class and Store-the-comonad
converged independently.)

| Member / operation | Shape | Laws & status |
|---|---|---|
| `GetValue` | `extract` | **comonad laws PINNED** (2026-08-12, `WalkerComonadLawTests` — extract participates in all three: `extend(extract) ≡ id`, extract-after-extend recovers the observer, co-associativity; laws are joint equations, so this row was "pending" only until Extend existed to be the other party). `duplicate` is now a derived one-liner — `w.Extend((w0, h) => h)`, the diagonal labeling — its laws corollaries of the pinned extend laws; an explicit pin is optional polish |
| `GetParent` / `GetChildAt` / `GetRootAt` | the co-algebraic decoration (tree-structured position space); by-value probes, closed over handle-space with `GetValue` the only exit | closure/one-way-door: architectural (no value→handle member exists — the pledge by construction) |
| `GetHandles` / `GetHandlesWithValues` | `duplicate` flattened (the tree of foci, poured out as rows of the labeling) | order deliberately unspecified (the set is the promise): documented |
| Axes (`GetAncestors`, `GetDepth`, …) | co-Kleisli queries (functions from a focus) | composition via LINQ (the walker's operator algebra is LINQ, by design) |
| `PruneAfter` lens (and future lenses) | reshaping pair-citizen: order half = the streaming operator, adjacency half = a wrapped probe | oracle equivalence lens-stream ≡ operator-stream: **PINNED** (`PruneAfterLensTests`) — the walker's own coherence family |
| `MaterializeWalkable` | **tabulate** (play the stream once, build the accessor) | probe idempotence pinned; tabulate∘sequence laws pending the Walk adapter |
| `Walk()` adapter (**BUILT 2026-08-12**, internal `WalkerWalk` — a thin composition over the existing hierarchical engine, since the indexed child probe IS a child pull) | **sequence** (read the accessor in a committed order → a stream) | conformance certified by `Extend(extract) ≡ id` — the adapter's streams equal the store treenumerators' (the degenerate-tower pin, executable) |
| `Extend` (**BUILT 2026-08-12**) | the comonad's defining operation — neighborhood-aware relabel; shape and handles untouched | **Store comonad laws PINNED** (`WalkerComonadLawTests`): `Extend(extract) ≡ id`; extract∘extend recovers the observer; co-associativity over genuinely neighborhood-dependent second observations. Plus the promised coherence law: **`RootfixScan(seed, fold)` ≡ `Extend`(root-path fold)** — the scan tier's cross-tenancy certified against the true comonadic operation |
| Re-root / membership (region lenses, unbuilt) | non-local reshapings — genuinely outside both the monad and the comonad proper; the seam family | descendant-information law prices them; laws per-lens |

**The pairing.** A labeled tree is the cofree comonad over its branching functor; a
consumer's traversal plan is a free-monad program over the request functor; **enumeration
is running the free ⋈ cofree pairing** — the treenumerable is the program side, the
walkable the machine side. "Two halves of the same whole," under its literature name.
`Materialize`/`Walk` are the tabulate/sequence conversions between the two function-space
representations (`() → stream` vs `handle → answer`), which is why one direction costs
O(n) once and the other is free.

## 5. Phase 2 — the law tests

**First wave LANDED 2026-08-12 (`CategoricalLawTests`, 16 tests, all green on first run —
zero deviations found):** functor identity + composition (stacked side Hide-forced); the
licensing squares (Where predicate merge, Select/Where interchange, both prune
OR-merges — the collapse lattice's licenses now named and semantically pinned); Invert
involution + naturality; Union `Empty` identities + associativity up to reassociation
(three-way merges flattened to a canonical per-node description); Intersection
annihilation; Subtract identities; both Do laws (streams AND effect traces, the traces
covering both drains — the per-drain contract exercised); embedding functoriality. The
technique of record: force the stacked side through `Hide` so each law is tested against
the genuinely stacked pipeline rather than the collapse it licenses.

**Second wave landed same day — PHASE 2 COMPLETE (20 laws, all green, zero deviations
across both waves):** Union/Intersection/SymmetricDifference commutativity up to swap;
SymmetricDifference `Empty` identities; Δ-associativity documented as a principled non-law
(promotion shifts positions). The close also produced the reduction discovery recorded in
§3's set-op row: the derived set ops are Union composed with the two reshaping rules —
the §6 Empty-graft fork already ships, one operator per rule.

**Pin-attribution spot-checks: CONFIRMED (2026-08-12).** `CrossTierCoherenceTests` holds
five Scan ≡ fold-shaped-Dispatch pins across the flavor grid;
`OrderChildrenByTests.Invert_IsOrderChildrenByDescendingSiblingIndex` is the subsumption
law; `DoLandingCompositionTests`, `VisitStreamConformance`, and the flat-family/contract
conformance batteries exist as cited. §3's attributions stand.

**The one deferred item — walker comonad laws** — is blocked on the `Extend`/duplicate
surface and moves to §6 as part of that build's acceptance criteria; until then the
oracle-equivalence family (each lens vs its streaming twin) is the walker's law suite and
grows with each lens. *(Resolved 2026-08-12 by phase 3 part B — kept for the record.)*

**Approved-for-later (review disclosure, 2026-08-12): set-op invariants currently
untested.** Disclosed during the Union review as below-the-line calls; the review promoted
them to sanctioned backlog:

- **Idempotence**: `t ∪ t ≡ t` and `t ∩ t ≡ t`, up to projection (both sides of every
  merged node present, values equal). One test apiece.
- **Self-subtraction**: `t − t ≡ Empty`. One test.
- **Distributivity**: `∩ over ∪` and kin — *statement work needed before pinning*: the
  nested `MergeNode` types require the same canonicalization machinery as the
  associativity test, and it is genuinely unclear whether positional-overlay semantics
  owes these laws at all. Investigate the statement first; a principled non-law is an
  acceptable outcome (the Δ-associativity precedent).

## 6. Phase 3 — law-driven specs for the two missing definers

**`SelectMany` (the monad's bind) — VERIFICATION EXECUTED 2026-08-12, finding recorded.**
The semantics were already decided (SELECTMANY_DESIGN.md, 2026-07-04: root-graft
substitution, promote at k = 0, children under the LAST root at k ≥ 2 — the k ≥ 2 case
flagged there as "asserted, not yet proven"). Phase 3 ran the verification via a
reference-model oracle grounded against the shipped `Where` and `Select`
(`SelectManyLawVerificationTests`): **identities PASS; the tree-valued fragment (k ≤ 1)
PASSES associativity — a lawful monad-with-zero whose theorems include
`Where ≡ SelectMany(Return-or-Empty)`; the forest-valued case (k ≥ 2) FAILS
associativity**, counterexample pinned — "under the last root" is not stable under
composition because a later bind can erase the attachment root. Decision now with the
design-holder (see the design doc's addendum): restrict to the lawful tree-valued core,
or design slot-inheritance semantics and re-verify. Prediction post-mortem: "vanish beats
promote" was wrong about the boundary (promote-at-k=0 is lawful) and right about the
mechanism (erasure-meets-attachment is where the algebra breaks). `ExpandNode`/`Graft`
adoption waits on the ruling.

**`Extend` (the comonad's co-bind) — BUILT AND LAW-CERTIFIED 2026-08-12.** The spec was
the Store comonad laws, and the build passed them on the first run
(`WalkerComonadLawTests`): `Extend(extract) ≡ id` (which simultaneously certifies the
Walk adapter against the store treenumerators — the degenerate-tower pin, executable at
last); extract-after-extend recovers the observer; co-associativity, exercised on a
genuinely neighborhood-dependent second observation (the second observer consults the
first extension's parent values). And the promised coherence theorem landed:
**`RootfixScan(seed, fold)` ≡ `Extend`(root-path fold)** — the scan tier's cross-tenancy
is no longer a classification, it is a pinned equation between the streaming tier's
restricted extend and the true one. Implementation shape: `Extend` is an
adjacency-delegating relabel whose streaming half is the Walk adapter (`WalkerWalk`) —
itself a thin composition over the existing hierarchical engine, because the walkable's
indexed child probe IS a child pull. The deferred walker-comonad item from §5 is
resolved.

## 7. The open classification: the treenumerator as machine

`MoveNext(NodeTraversalStrategies)` makes the treenumerator not a plain stream but a
**dialogue machine** — per step the consumer sends a strategy, the machine yields a visit.
Mealy-machine-flavored; the pipes/free-monad-over-request-functor lineage is the closest
engineered prior art; the precise categorical home (and whether the strategy channel
changes any law statements — e.g., bisimulation must quantify over strategies, which the
conformance battery's full strategy matrix already does) is left to name here in a later
pass. Note the walk-floor citizens (`TakeNodesUntil` and kin) are operations on this
machine layer, which is why they resist tree-monadic classification.

## 8. Reading map

1. **Meijer, "Subject/Observer is Dual to Iterator" (2010)** — the .NET-native precedent
   for dualizing a state-machine factory interface (`IEnumerable` → `IObservable` by
   arrow-flipping); this survey's Store-comonad move is the same maneuver aimed at random
   access.
2. **Rutten, "Universal coalgebra: a theory of systems"** — machines as coalgebras;
   equality as bisimulation (= `VisitStreamConformance`); the branching functor picks the
   family (`1 + A×X` enumerators, `A × List X` trees).
3. **The machines / pipes / conduit lineage (Kmett, Bjarnason, Gonzalez)** — monads over
   machine factories, engineered; `pipes`' bidirectional request/respond proxy is the
   closest formal cousin of `MoveNext(strategies)`.
4. **Piponi, "Cofree meets free"; Kmett, "Monads from comonads"** — the free ⋈ cofree
   pairing: program against machine = enumeration; the two tiers under their literature
   names.
5. **Uustalu & Vene, "Comonadic functional attribute evaluation"** — attribute grammars
   (Knuth 1968) evaluated as `extend` over tree zippers; inherited attributes = rootfix,
   synthesized = leaffix — the scan tier's pedigree.
6. **Spivak & Niu, *Polynomial Functors: A General Theory of Interaction*** — the
   ultimate generalization: Moore machines, dialogue, wiring, and *lenses* as citizens of
   one category (`Poly`). Where "everything is a state-machine factory" becomes a
   definition with a calculus. (The standard graphical notation for a polynomial is a
   forest of corollas — the pictures of the objects are trees.)

**Frontier note:** the literature above delivers *trees* — sharing breaks cofree/polynomial
structure, so the `Dagnumerable` work sits where the textbooks thin out. Some of that
branch's friction is the field's, not the design's.

## 9. Open questions

- The precise home of the dialogue machine (§7), and whether any law statement needs the
  strategy channel made explicit beyond the conformance battery's existing matrix.
- Which of the reshaping trio survives §6's associativity arbitration as a theorem.
- Whether the positional operator flavors should be formalized as Kleisli/co-Kleisli over
  the position-decorated tree, or left as documented decorations.
- Where the walker's non-local lenses (re-root, membership) sit categorically — the
  current answer is "outside both, priced by the descendant-information law," which is
  honest but unnamed.
- Whether the licensing squares (§5.3), once named, suggest lattice collapses not yet
  built — the lattice was discovered empirically; the laws may know more than the
  implementation does.
