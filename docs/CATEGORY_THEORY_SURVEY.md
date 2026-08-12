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
| **Reshaping (non-local / order-sensitive)** | structure change driven by identity, position, or traversal order | outside bind by nature; laws are per-operation |
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
| `Where` (positional flavor) | reshaping over the position-decorated tree | deliberately non-composing with its own kind (each layer sees its input's labels — LINQ's indexed-Where rule, documented) |
| `PruneBefore` / `PruneAfter` | local reshapings; bind-candidates (*vanish* rule / *slotless-leaf* rule) | prune-over-prune merge (OR-disjunction), both operators: **PINNED** (`CategoricalLawTests`, Hide-forced — the in-tier composition of TIER SEAL 2026-08-04 now has its licensing law named); bind-derivability: pending §6 |
| `TakeNodesUntil/While`, `TakeTrees`, `SkipTrees`, `TakeLast/SkipLastTrees` | **order-sensitive truncations** — operations on the *walk*, not the tree (the monad is order-free; these are stream-level by nature) | per-op semantics; no monad laws owed. Note: these are walk-floor citizens named before the walk floor existed |
| `Union` / `Intersection` / `Subtract` / `SymmetricDifference` | zip / monoidal — **and the family REDUCES to one primitive** (discovered in the phase-2 close, 2026-08-12): `Intersection = Union.PruneBefore(!both)` (the *vanish* rule), `SymmetricDifference = Union.Where(!both)` and `Subtract = Union.Where(!HasRight).Select(.Left)` (the *promote* rule) — the Empty-graft fork of §6 is already living in the shipped set ops, one derived operator per rule | Union `Empty` identities + associativity up to reassociation + commutativity up to swap: **PINNED** (`CategoricalLawTests`); Intersection annihilation + commutativity up to swap: **PINNED**; Subtract right-identity + left-annihilator: **PINNED**; SymmetricDifference `Empty` identities both sides + commutativity up to swap: **PINNED**. **Non-law, documented:** Δ-associativity is NOT owed — tree-Δ rides the promote rule, promotion shifts positions, the set-theoretic xor law does not transfer to positional merges (same class as positional Where's non-composition) |
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
| `GetValue` | `extract` | comonad laws: pending the duplicate/extend surface |
| `GetParent` / `GetChildAt` / `GetRootAt` | the co-algebraic decoration (tree-structured position space); by-value probes, closed over handle-space with `GetValue` the only exit | closure/one-way-door: architectural (no value→handle member exists — the pledge by construction) |
| `GetHandles` / `GetHandlesWithValues` | `duplicate` flattened (the tree of foci, poured out as rows of the labeling) | order deliberately unspecified (the set is the promise): documented |
| Axes (`GetAncestors`, `GetDepth`, …) | co-Kleisli queries (functions from a focus) | composition via LINQ (the walker's operator algebra is LINQ, by design) |
| `PruneAfter` lens (and future lenses) | reshaping pair-citizen: order half = the streaming operator, adjacency half = a wrapped probe | oracle equivalence lens-stream ≡ operator-stream: **PINNED** (`PruneAfterLensTests`) — the walker's own coherence family |
| `MaterializeWalkable` | **tabulate** (play the stream once, build the accessor) | probe idempotence pinned; tabulate∘sequence laws pending the Walk adapter |
| `Walk()` adapter (unbuilt) | **sequence** (read the accessor in a committed order → a stream) | the tabulate/sequence pair's laws land here; also serves lens stream-halves and the constant-space engine |
| `Extend` (unbuilt) | the comonad's defining operation — neighborhood-aware relabel | Store comonad laws as the spec, before code (§6) |
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
grows with each lens.

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

**`Extend` (the comonad's co-bind).** Spec = the Store comonad laws. The operator: relabel
every node by an arbitrary function of its focus (depth, parent's value, subtree
aggregate — things streaming `Select` cannot see). The scans then become the theorem
`extend` restricted to order-factoring folds, and the coherence family extends to:
scan-extend agrees with true extend wherever both apply.

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
