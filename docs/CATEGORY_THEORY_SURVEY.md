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
| `Select` | functor map | composition: *implicit* (the collapse lattice merges consecutive Selects — `CompositionTests` pin the collapsed ≡ stacked behavior, which IS the law, empirically); identity: UNPINNED |
| `Where` (value flavor) | local reshaping; bind-candidate under the *promote* Empty rule | predicate merge `Where(p).Where(q)` ≡ `Where(p∧q)`: *implicit* (composition driver); interchange with Select (licenses `SelectThenWhere`): *implicit*; bind-derivability: pending §6 |
| `Where` (positional flavor) | reshaping over the position-decorated tree | deliberately non-composing with its own kind (each layer sees its input's labels — LINQ's indexed-Where rule, documented) |
| `PruneBefore` / `PruneAfter` | local reshapings; bind-candidates (*vanish* rule / *slotless-leaf* rule) | prune-over-prune predicate merge: *implicit* (in-tier composition, TIER SEAL 2026-08-04); bind-derivability: pending §6 |
| `TakeNodesUntil/While`, `TakeTrees`, `SkipTrees`, `TakeLast/SkipLastTrees` | **order-sensitive truncations** — operations on the *walk*, not the tree (the monad is order-free; these are stream-level by nature) | per-op semantics; no monad laws owed. Note: these are walk-floor citizens named before the walk floor existed |
| `Union` / `Intersection` / `Subtract` / `SymmetricDifference` | zip / monoidal | associativity, `Empty` identity, commutativity claims: **UNPINNED** — prime phase-2 targets (any failure = bug or documented deviation) |
| `StructuralMerge` | zip family (general lockstep) | associativity: UNPINNED |
| `Do` / `Hide` | effect landing — **deliberately outside the algebra** ("never composes and prevents composition across it by definition"; the window materializes the pane) | the non-law is documented and pinned (`DoLandingCompositionTests`) — an example of a *deviation done right* |
| `RootfixScan` | scan-extend (inherited-attribute evaluation — Knuth's attribute grammars: inherited = rootfix) | cross-tier coherence `Scan(boundary, fold)` ≡ fold-encoded `Dispatch(boundary)`: **PINNED** (`CrossTierCoherenceTests`) |
| `LeaffixScan` / `LeaffixDispatch` | scan-extend, upward (synthesized attributes = leaffix); dispatch = the sibling-complete survey tier | same coherence family: **PINNED**; the seat rules and boundary instruments are the operational shadow of extend's neighborhood being order-restricted |
| `RootfixDispatch` | scan-extend, downward survey tier | coherence: **PINNED**; full-participation/boundary rules documented |
| `RootfixAggregate` / `LeaffixAggregate` | fold ∘ scan-extend | derivability from scan + leaves: *implicit* by construction |
| `Invert` | **natural transformation** (value-independent) | subsumption `Invert` ≡ `OrderChildrenByDescending`(source sibling index): **PINNED** (`OrderChildrenByTests`); involution `Invert∘Invert ≡ id`: UNPINNED; naturality `Invert∘Select(f)` ≡ `Select(f)∘Invert`: UNPINNED |
| `OrderChildrenBy` | key-driven structure map (not natural — depends on values through the key) | stability documented; subsumption law shared with Invert: **PINNED** |
| `Memoize` / `Materialize` | **representation morphisms** — identities modulo the quotient (tabulation-adjacent) | replay ≡ source: **PINNED** (conformance batteries; memo replays and serializer round-trips ride `VisitStreamConformance`) — the surface's strongest law, already enforced |
| `Consume`, `AnyNodes`/`AllNodes`/`CountNodes`/`CountTrees`, traversal enumerables, `GetLeaves`/`GetLevels`/`GetBranches` | folds / drains | fold-respects-structure: *implicit* via conformance of the streams they consume |
| `ToFormattedLines` / `ToFormattedString` | fold to rendering | golden-pinned (`FormattedLinesTests`) |
| `ToDegenerateTree` / `ToTrivialForest` | **embeddings** — two functors List → Tree (the chain and the flat forest) | functoriality: UNPINNED (cheap) |
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

## 5. Phase 2 — the law-test backlog

Concrete, testable now, in rough value order:

1. **Set-op monoid laws** — `Union` associativity, `Empty` as identity, commutativity
   claims. Any failure is a caught bug or a deviation to document.
2. **Invert involution** (`Invert∘Invert ≡ id`) and **naturality**
   (`Invert∘Select(f)` ≡ `Select(f)∘Invert`). Cheap, satisfying.
3. **The licensing squares** — state the interchange laws the collapse lattice *relies
   on* as named semantic pins (`Select`/`Select` composition, `Where` predicate merge,
   `Select`/`Where` interchange, prune merges): today they are pinned only through the
   collapsed-vs-stacked behavioral tests; naming them makes future lattice work
   spec-driven rather than empirical.
4. **Functor identity** for `Select`; functoriality of the two List→Tree embeddings.
5. **Walker comonad laws** once the duplicate/extend surface exists; until then, the
   oracle-equivalence family (each lens vs its streaming twin) is the walker's law suite
   and grows with each lens.

## 6. Phase 3 — law-driven specs for the two missing definers

**`SelectMany` (the monad's bind).** The central design question — where do the children
of a replaced node attach — is the **Empty-graft rule**, and the candidate rules *are*
existing operators: `f(v) = Empty` with children *vanishing* is `PruneBefore`; with
children *promoted* it is `Where`; with slot/attachment-point semantics, a *slotless leaf*
is `PruneAfter`. One bind gets one rule, so **associativity arbitrates**: choose the
graft-and-Empty semantics that satisfies the monad laws and maximizes the reshaping trio's
derivability; whatever loses stays primitive with the reason documented. (Suggestive
alignment: `Where` is the library's hardest operator, and *promote* is the graft rule
whose associativity is most delicate — operational difficulty and law delicacy pointing at
the same place.) `ExpandNode`/`Graft` are adopted or retired by this design. CLAUDE.md's
own `Where` documentation ("fundamentally different from `SkipDescendants`, which would
remove `d` and `e` too") is the two Empty rules stated operationally, years early.

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
