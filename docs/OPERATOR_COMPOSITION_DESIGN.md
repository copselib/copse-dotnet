# Operator Composition (design record)

> **Status: DESIGNED 2026-07-15; SHIPPED through the narrow (single-dimension) halves
> 2026-07-18 (see Phases), all rulings taken and measured.** The recipe surface is the
> one-property/one-method shape with the light-tier capability doors, mirrored per
> traversal dimension. Companion decisions in
> [LAZINESS_AND_BUFFERING_POLICY.md](LAZINESS_AND_BUFFERING_POLICY.md);
> capability probing (the orthogonal "cheaper when rich" axis) in
> [TREE_CAPABILITY_INTERFACES.md](TREE_CAPABILITY_INTERFACES.md) — its takeaway after
> review: capability wins do not compound through composition, so that lattice stays
> demoted while operator composition (which does compound) proceeds.

> **Vocabulary (settled 2026-07-17 after two renames; sharpened 2026-07-18):** the recipe
> surface speaks the MACHINERY — `ISelectTreenumerable` (the projection wrapper's compose,
> reviving the original interface name) and `ISelectWhereTreenumerable` /
> `SelectWhereTreenumerable` (the general wrapper: LINQ's WhereSelect precedent; in this
> codebase "Where" already names the generalized filter machinery that hosts the prunes).
> Both methods are `Compose` — both kinds compose; neither is "the" canonical composition
> (the interim Composable/Composition spelling implied exactly that and was renamed away).
> The carrier is `SelectWhereResult`, whose two fields ARE the name's two halves: `Value`
> is the Select half's answer, `Strategies` the Where half's. The technique itself is named
> by what it does — collapsing stacked layers into one composed wrapper — and this document
> and the codebase speak ONLY the composition vocabulary (composed vs stacked); the
> literature's synonym for the technique is deliberately not used anywhere (ruling
> 2026-07-18).

## Motivation

Chained operators stack wrapper treenumerators; every pull cascades through every layer
(position bookkeeping, visit republishing, and — on the async side — one state machine per
layer; the AsyncOverhead `OperatorStack` pair floors at ~1.5x for this reason). LINQ solves
this with internal combined iterators (`WhereSelectEnumerableIterator` etc.). Copse already
ships one such collapse: `Select∘Select` via `ISelectTreenumerable.Compose`.

## The two laws (ratified)

1. **Composition never reorders lambdas.** Collapse layers, never sequence: every user
   delegate fires for exactly the nodes, with exactly the contexts, in exactly the
   per-lambda order of the stacked pipeline. Side effects make reordering a semantics
   change, not a UX preference. Adjacent-pair composition only — LINQ's line as well.
2. **Labels change only at emission.** Within a treenumerator every position is the INNER
   tree's label; user lambdas see their operand tree's coordinates unmodified by the
   operator's own structural edits (the Where drivers test
   `_Predicate(InnerTreenumerator.ToNodeContext())`). Promotion is logical; relabeling
   (depth compression, sibling renumbering) happens once, at the emission boundary — pinned
   by WhereTests (a(b(d,e,f),c) filtered on b emits d,e,f at (0,1),(1,1),(2,1), c
   renumbered to (3,1)). The frames carry both views (`OriginalPosition` + `Position`).
   Corollary: collapsing k layers deletes k−1 emission boundaries, but each layer's
   would-have-been-emitted labels remain semantically live — the composed driver must
   compute them without synthesizing the intermediate streams.

## The load-bearing decision: value-only overloads are the composition surface

**Compound ≠ stacked.** Over `a(b(c))` with p1 = node≠"b", p2 = depth≤1:
`Where(n => p1 && p2)` yields `a` (p2 judges c at source depth 2), while
`.Where(p1).Where(p2)` yields `a(c)` (p2 sees layer 1's emitted label, depth 1, after b's
removal promoted c). Position-observing predicates make naive predicate-ANDing a bug.

**LINQ's answer, verified against the BCL source:** only the value-only overloads
combine. `Where(Func<T,bool>)` probes `Iterator<T>` and combines via
`CombinePredicates(p1, p2) == x => p1(x) && p2(x)` — legal because no coordinate is
observed. The indexed `Where(Func<T,int,bool>)` is a plain iterator block that never
participates; stacked indexed Wheres each keep their own counter (the second index counts
the first's survivors). LINQ shipped coordinate-observing filters for fifteen years and
never combined them.

**Copse adopts the same partition, with one twist: our only `Where` today is the
"indexed" one** (every predicate sees `NodeContext`), so machinery-level composition would
miss the common case entirely. Therefore:

- Add **value-only overloads**: `Where(Func<TNode, bool>)`,
  `Select(Func<TSource, TResult>)` (and the prune twins). Position-blind by type ⇒
  compound ≡ stacked ⇒ adjacent same-kind collapse is delegate composition, trivially
  correct. LINQ-parity API; likely the majority of real predicates.
- **Signature ruling (Jason, 2026-07-15): the positional overload is `(TNode, NodePosition)`,
  not `NodeContext`** — verified by compiler experiment: `Func<TNode,bool>` beside
  `Func<NodeContext<TNode>,bool>` is CS0121-ambiguous for any lambda whose body binds under
  both (`x => true`, parameter-ignoring closures, node types with a `Node`/`Depth` member),
  while the arity split (`x => …` vs `(x, p) => …`) resolves always — LINQ's `(x, i)` shape
  for the same reason. Consequence: `NodeContext` exits the public operator-lambda surface
  entirely (it remains treenumerator-level vocabulary); migrating every
  `Func<NodeContext<T>, …>` operator signature is a full-surface break — legal in alpha,
  sized as its own workstream, best landed before beta/consumers.
- The positional overloads keep today's exact semantics. Positional **Select**s still
  self-compose (Select never relabels, so chained selectors see identical positions:
  `(n, p) => outer(inner(n, p), p)`); positional **Where** never composes with its own
  kind. The general law: a pair is composable iff no lambda observes a coordinate that a
  collapsed emission boundary would have relabeled.

## The API & UX design (ratified 2026-07-16)

**The context gradient — the one idea that explains the whole surface:** the richer the
context a lambda observes, the more real the machinery around it must be.

1. **Value lambdas** (`n => …`) — see nothing but the node; compose anywhere,
   unconditionally. The simple tier is the fast tier, by design.
2. **Positional lambdas** (`(n, position) => …`) — see coordinates; compose only across
   label-preserving prefixes, and otherwise their append point IS an emission boundary (a
   real layer stacks so the labels they read are genuinely emitted).
3. **`NodeVisit` / the treenumerator protocol** (`Do`, visit-stream consumers) — see the
   full traversal state; never composed, because the protocol they observe must physically
   exist where they watch it.

Users choose a tier by what they need to see; each tier carries the strongest guarantees
and best performance it can honestly offer.

**Ratified decisions:**

- **Composition is completely invisible — never exposed.** No public docs mention it, no
  type surfaces it, no behavior reveals it (to pure lambdas). It is an implementation
  property, not a feature of the surface.
- **The purity contract** (the invisibility clause's fine print): computation lambdas
  (predicates, selectors, keys) must be pure — the library guarantees what each lambda
  observes and in what order, never how many times it runs. Concretely: over `a(b,c)`
  (7 visits, 3 nodes), a stacked Select evaluates its selector 7 times, the composed
  Select∘Where evaluates it 3 times; identical trees out, only an impure counter can tell.
- **`Do` is the sanctioned effect point, with a SPECIFIED cadence**: its action runs on
  every emitted visit and receives the full `NodeVisit` — deliberately permissive, because
  every narrower cadence is a one-line filter inside the caller's action
  (`Mode == SchedulingNode` = once per node; `VisitCount == 1` = first visits). `Do`
  therefore keeps `NodeVisit` through the signature migration (the deliberate survivor),
  never composes, and inserting one between operators prevents their composition by
  definition — the window materializes the pane. (It also serves as the honest
  force-stacked control in tests.) No value-flavored `Do` overload: it would be a lossy
  special case.
- **Node/position decoupling proceeds across the whole operator surface** (consistency
  over per-case NodeContext survivals), with survivals decided case by case where the full
  context IS the point (`Do`). Which operators get a positional flavor at all is decided
  during the migration; the default posture is LINQ's — filter/map family yes, key
  selectors/accumulators value-only, with the tuple escape hatch
  (`.Select((n, p) => (n, p))`) covering the rare positional key.

## Pair matrix

| Pair (inner→outer) | Verdict | Mechanism |
|---|---|---|
| Select∘Select | **shipped** | `Compose` delegate composition |
| value-only Where∘Where | **shipped** | predicate combination (LINQ's `CombinePredicates`) |
| Select↔Where | **shipped** | genericized Where core: `<TInner, TNode>` + selector at the test site; Select is position-invariant so no label arithmetic exists |
| Select∘Prune | **shipped** | same seam on the prune cores — composition is a dispatch table of pair types; prune's bespoke no-promotion machinery is never funneled through Where |
| Select∘capture-ops | filed | fold projection into the capture walk (the keyed factory's side-channel precedent) |
| context-ful Where∘Where | **gated, presumed unneeded** | see below |
| Where↔Prune cross | don't | combined machinery not worth it; layers stack |

**Context-ful Where∘Where, the settled analysis:** possible, not five copies of the
machinery — one driver whose frames carry a FATE (first rejecting layer; early-exit
matches stacked) plus k-wide accepted-child counters; depth at layer j = ancestors with
fate > j. Intermediate visit streams never exist, so manufacture/suppress synthesis runs
once, at the final layer. Consumer strategies translate in one hop independent of k
because promotion only moves nodes up WITHIN their surviving ancestor's subtree — every
virtual subtree closes with its root's SOURCE subtree (SkipSiblings ⇒ skip until the
effective parent's source subtree closes; effective parent = nearest frame with
fate = survives-all). DFT is tractable; **BFT is the monument** (front cadence, per-layer
prefix carries, manufactured-visit ledger — the proven-fragile code widens per layer). If
ever demanded: DFT-first via the dimension split. The value-only overloads are expected to
make this cell permanently moot.

## Mechanics (ratified)

- **Recipe surface is internal** (main 09a760f): a public recipe makes our operators'
  correctness depend on foreign `Compose` implementations, and net48/netstandard2.0 rule
  out default interface members, so public interface evolution is breaking. LINQ's
  hidden-iterator precedent is the considered one. Internal→public later is free.
- **Compose-in-one-step, ONE method (Jason's original `Compose` model, restored 2026-07-16
  after the map middleman was reviewed out; collapsed to a single method 2026-07-17)**: the
  interface is `Relabels` plus ONE total method —
  `Compose<TOut>(resultSelector, relabels)` where a result selector is
  `Func<NodeContext<TNode>, SelectWhereResult<TOut>>` — returning the successor
  treenumerable directly: the wrapper unwraps its own mapping, composes, discards itself,
  constructs. One method suffices because the composition law subsumes fmap: a projection
  is a result selector that never rejects (results carry `TraverseAll`), and the law
  composes it correctly without being told. The law is written ONCE, in
  `SelectWhereComposition` (see the algebra bullet below). The interim `CompositionMap`
  object had re-encoded the wrapper TYPE structure as runtime data and cost a transient
  object per composition step; deleted. The PROJECTION FAST PATH is a capability
  interface, `IAsyncSelectTreenumerable : IAsyncSelectWhereTreenumerable`, that ONLY
  `AsyncSelectTreenumerable` implements (`Compose` keeps projection∘projection on the
  light acquisition; an implementer is by construction projection-only, so its relabeling
  bit is always false) — the Select operator probes the capability first, then falls back
  to the general `Compose` with the never-rejecting result selector. Optimizations belong
  to the optimized: the fast path is one type's virtue, not everyone's obligation.
  `AsyncPruneAfterTreenumerable` converts itself to the general representation and
  delegates (unwrap, discard, rebuild). Erasure unchanged: the wrapper knows its source
  type; the method is typed on output only. Each operator's semantics are stated ONCE —
  the compose branch reuses the plain path's selector struct as a method group
  (`new WhereResultSelector(p).GetResult`); PruneAfter's lives in the wrapper's
  `CreateResultSelector`. `SelectWhereResult` is a BARE PAIR `(value, strategies)` — two
  fields, one constructor, zero behavior — rejection IS SkipNode membership, inherited
  from the consumer protocol, so every pair is coherent by definition. (An earlier
  Accept/Reject factory vocabulary was dropped in review 2026-07-16: once PruneAfter is an
  accept carrying skip instructions and PruneBefore a reject whose payload is the whole
  message, the case names no longer carry the semantics — the strategies value does, and
  the factories' implicit `| SkipNode` never fired at any call site.) Design lineage, each
  step Jason's: four flavor-hooks → two methods + property → one method + self-describing
  increment → one property + the map with combinators → two compose methods + the bit, map
  deleted → ONE compose method + the bit, fast path demoted to a single-implementer
  capability.
- **The representation lattice (2026-07-17; forced by the first post-merge CI run)**:
  representations follow the relabeling gradient. `ISelectTreenumerable` widened into
  `ISelectPruneAfterTreenumerable : ISelectWhereTreenumerable` — the LIGHT TIER, for chains
  whose results never carry `SkipNode` (projections + prune-afters; the relabels-nothing
  row, so `Relabels` is false by construction and positional lambdas always compose
  across). Its two methods are the type-enforced tier boundary — `Compose(selector)`
  returns a bare value, `ComposePruneAfter(predicate)` a bool; neither signature can
  smuggle a rejection. Implementers: the Select wrapper (projections stay lightest), the
  PruneAfter wrapper (prune∘prune merges by predicate union on the bespoke driver), and
  `SelectPruneAfterTreenumerable` (mixed chains; a dimension-agnostic passthrough driver —
  no promotion machinery, no path state, delegate-bound arrow since only composition
  creates it). A rejecting operator converts to the SelectWhere representation via the
  inherited general `Compose`. The lesson that forced it: composition is invisible, so
  "who would write that chain" is the wrong rarity test — the canonical Mega trees are
  PruneAfter-built, making prune- and select-over-pruned-tree the default shape; the Where
  driver's path state surfaced as a 2x allocation regression within one CI run of the
  merge.
- **The algebra has one home: `SelectWhereComposition` (2026-07-18, extracted with the
  narrow halves)**: every way two adjacent arrows compose into one — the composition law
  (`ResultSelectorThenResultSelector`) plus the tier arrows (`SelectThenSelect`,
  `SelectThenPruneAfter`, `PruneAfterThenSelect`, `PruneAfterThenPruneAfter`, the
  `SelectPruneAfterThen…` trio) — lives in one static class, named [inner]Then[outer] in
  execution order. The algebra is dimension-blind (arrows never touch a treenumerator);
  wrappers own only representation choice (which successor type to build) and acquisition
  (which driver runs the composed arrow).
- **The narrow (single-dimension) halves mirror the lattice (2026-07-18)**: the
  dimension-preserving operator overloads (`IDepthFirstTreenumerable` /
  `IBreadthFirstTreenumerable` receivers) compose exactly like the composite-width ones.
  Per dimension: `ISelectWhere[Depth|Breadth]FirstTreenumerable` (the bit + the one
  `Compose`, returning a narrow successor) and
  `ISelectPruneAfter[Depth|Breadth]FirstTreenumerable` (the light tier's two doors), with
  the four wrappers mirrored (`SelectWhere…`, `Select…`, `PruneAfter…`,
  `SelectPruneAfter…` — same drivers, same algebra, narrow successor width). The narrow
  overloads also probe the composite recipe surface first: a composite-width wrapper
  arriving through a narrow-typed receiver keeps composing on its own representation, and
  its successor keeps both dimensions. The twins are GENERATED, not hand-mirrored
  (2026-07-18, `CompositeToNarrow` in Copse.CodeGen; see docs/ASYNC_CODEGEN.md): the
  dimension axis, like the color axis, cannot be abstracted by C# generics — the successor
  type constructor is what varies — so a change to a wrapper is made once, in the
  composite-width async file, and fans out to five generated twins (narrow async ×2,
  sync ×3). The operator overloads' probe tables stay hand-written: their narrow and
  composite forms differ genuinely (probe sets, fallbacks).
- **Composed machinery is the genericized core, not duplicated types**: `Where` drivers are
  `<TInner, TNode>` with a selector evaluated once per TESTED node against the source
  context; the path structs store projected values (values are opaque cargo — the library
  never compares nodes). Plain Where/PruneBefore instantiate with their bespoke selector
  structs. Composed SelectWhere = the same driver with the composed selector — zero new
  driver code.

## Measured gate (closed)

Identity-selector cost on plain Where (Job.Default A/B, branch vs main): one un-inlinable
delegate call + one extra `NodeContext` copy per tested node ≈ 1.2 ns — invisible on all
realistic rows, **+14%/+21% on Dft/Bft_Forest_DropAll** (the ~7 ns/node empty-loop
degenerate rows). RULING TAKEN (see Phases 0.5): the result seam is struct-generic
(`TResultSelector : struct, IResultSelector<TInner,TNode>` — the engines'
`TChildEnumerator` house pattern); the degenerate-row regression is gone, the inlined
selector slightly beats the pre-composition shape.

## Test strategy (agreed)

- **Force-stacked equivalence**: corpus × operator chains, composed pipeline vs the same
  chain with composition broken (`Tree.Defer` / `Tree.DeferDepthFirst` /
  `Tree.DeferBreadthFirst` — their delegating wrappers are not composable), visit-stream
  identical — rides `VisitStreamConformance`.
- **Lambda-order pins**: recorded invocation sequences (which lambda, which node, which
  context) match the stacked pipeline — law 1 as a regression guard.
- **Semantics ruling (pinned)**: the composed seam evaluates selectors once per tested
  node; the plain `SelectTreenumerator` re-evaluates per visit. Visit-stream equality and
  per-lambda order are pinned; selector invocation COUNT is unspecified (purity expected).
  Also: BFT cross-predicate interleaving differs composed-vs-stacked (per-predicate order
  preserved); same ruling class.
- Benchmarks consult-first; the Compose family (composed/stacked ratio pairs) landed
  2026-07-17 — first readings: the collapse win is 1.3-1.6x DFT, 9-13x BFT.

## The result monad (the composition model — Jason's original design, formalized)

This was the shape of the original `Compose` vision: the composed chain internally keeps a
mapping from the source node to `<TResult, TreeTraversalStrategy>`, and composing wrappers
unwrap the map and rebuild it. Formalized, the carrier is a Writer monad over the
strategy monoid, stacked with short-circuit:

```
Result<T> = (T value, NodeTraversalStrategies strategies)   -- a bare pair, one constructor
Rejected   ⇔ SkipNode ∈ strategies                           -- a derived view, not a case
```

The carrier is a PRODUCT, not a sum: the strategies alone say what happens to the node,
because the consumer protocol already defines SkipNode as removal. "Accepted"/"Rejected"
below are the derived views, not constructors. (The review that settled this: once
PruneAfter is an accept carrying skip instructions and PruneBefore a reject whose payload
is the whole message, case names stop carrying the semantics — the strategies value does.)

Each operator's contribution is a Kleisli arrow `NodeContext<TSource> → Result<TOut>`;
the composed wrapper (`SelectWhereTreenumerable<TSource, TResult>`) is the reified
composite arrow, and appending an operator Kleisli-composes and returns a new wrapper —
closed under composition: one wrapper, any order, any length.

**Composition law** (what keeps composed ≡ stacked):
- `Accepted(v, s₁)` → next arrow `Accepted(v₂, s₂)` ⇒ `Accepted(v₂, s₁ ∪ s₂)`.
- `Accepted(v, s₁)` → next arrow `Rejected(s₂)` ⇒ `Rejected(s₁ ∪ s₂)`.
- `Rejected(s)` composes with nothing — the first rejecting arrow ends evaluation (in the
  stacked pipeline, later layers never saw that node). Short-circuit early whenever the
  union reaches skip-everything.

**Arrow vocabulary**: value-Where = `(v, pred ? ∅ : SkipNode)`;
PruneBefore = `(v, pred ? SkipNodeAndDescendants : ∅)`; PruneAfter =
`(v, pred ? SkipDescendants : ∅)` — the accept-with-strategy pair a bool cannot express, and
the reason the carrier is a result rather than a bool; Select = `(f(v), ∅)`.
Because the composite computes the FINAL value regardless of where filters sit among
projections, the Where-then-Select seam needs no emission-side driver surgery — it
dissolves into closure composition.

**The join rule, per lambda FLAVOR** (not per operator): value lambdas join any chain;
positional lambdas join only a label-preserving prefix — the wrapper carries one bit,
`Relabels` (any promoting filter sets it; projections and prune-afters do not) — and
otherwise their append point IS an emission boundary (the chain terminates, a layer
stacks, the positional lambda reads genuinely emitted labels). This applies to positional
Select exactly as to positional Where, and to any future positional flavor.

**Driver contract**: per node, run the composite once against the SOURCE context; on
`Accepted`, publish the value and apply its strategies to the inner pull (the frames
already carry per-node strategies); on `Rejected`, skip with its strategies (`SkipNode` →
promotion machinery, `SkipNodeAndDescendants` → subtree drop). CONSUMER-side strategies
flowing into MoveNext are a separate channel, handled once, at the final (real) layer.

## Phases

0. ✅ Recipe surface internal + param hygiene (main 09a760f).
0.5 ✅ Genericized Where substrate; ✅ struct ruling TAKEN and MEASURED 2026-07-16: the
   result seam is struct-generic (TResultSelector : struct, IResultSelector<TInner,TNode>
   — the engines' TChildEnumerator idiom). Plain operators carry bespoke readonly selector
   structs (JIT inlines GetResult; per-node cost = one indirect call, the user's own
   lambda); composed chains carry FuncResultSelector over the composed closure. Gate
   verified: the degenerate-row regression is GONE (Dft_Forest_DropAll 7.99 -> 6.90 ms vs
   main -- the inlined result slightly beats main's shape). Selector structs must stay
   stateless/readonly (defensive-copy trap, documented on the interface).
1. ✅ SHIPPED (branch, 2026-07-16): Where/Select signatures migrated to the arity split —
   (node) / (node, position), NodeContext removed from these operators (~150 call sites
   swept); the unified internal recipe surface (interim per-flavor hooks returning null to
   DECLINE, since collapsed to the single `Compose` — see Mechanics); named
   `WhereTreenumerable`; value Where∘Where by predicate combination; Select↔Where (both
   Where flavors) into the projection-carrying driver. The pin suite covers
   equivalence-vs-stacked, lambda order + early exit, compound≠stacked, positional-over-
   Select legality, and once-per-node selector evaluation on the composed path (the
   invocation-count ruling, now pinned rather than open).
2. ✅ SHIPPED (branch, 2026-07-16): `SelectWhereResult<T>` + result-shaped filter drivers (one
   composed evaluation per scheduled node; per-node reject strategies; DFT honors accept-side
   strategies via pending merge, BFT seam documented awaiting the PruneAfter migration);
   `SelectWhereTreenumerable` (the reified Kleisli arrow — value chains of any length/order
   collapse to one wrapper, pinned) with the relabeling bit; PruneBefore is a result-selector
   operator and joins chains (removal polarity explicit in the result); the pure-Select
   wrapper stays distinct so projection-only chains keep the light Select treenumerator.
2.5 ✅ SHIPPED (branch, 2026-07-16): the BFT accept-strategy seam (pending/deferred slots,
   verified against the bespoke PruneAfter oracle across ~4,600 interference sub-cases);
   the prune signature migration ((node)/(node, position) pairs; SkipTrees and Intersection
   migrated -- Intersection's prune now composes into the merge pipelines); PruneAfter's
   named wrapper (bespoke no-promotion driver for plain acquisition; label-preserving, so
   positional lambdas compose across it, pinned); the 8-symbol battery (584 chains, 376k
   cases/dim, interference bounded to chains of length <= 2). The extended battery
   immediately found a pre-existing bug: the bespoke PruneAfter driver tested the user
   predicate against the pre-enumeration ForestRoot sentinel -- guarded, user lambdas see
   real nodes only.
2.6 ✅ SHIPPED (main, 2026-07-17): the representation lattice (the light tier; see
   Mechanics) + PruneAfter∘PruneAfter predicate-union merge; the Compose benchmark family
   (composed/stacked ratio pairs — collapse win 1.3-1.6x DFT, 9-13x BFT).
2.7 ✅ SHIPPED (branch, 2026-07-18): the narrow (single-dimension) halves — the algebra
   extracted to `SelectWhereComposition` (one home, dimension-blind) and the wrapper
   lattice mirrored per dimension (see Mechanics); the dimension-preserving overloads of
   Where/Select/PruneBefore/PruneAfter probe narrow + composite recipe surfaces and build
   probeable narrow wrappers instead of anonymous factory layers. Pinned in
   NarrowCompositionTests (collapse, tiering, join rule, composite-through-narrow, engine
   conformance over the corpus). Follow-up same day: the twelve narrow twins became
   GENERATED files (`CompositeToNarrow` phase in Copse.CodeGen, drift-guarded) — the
   lattice's implementation is edited in one place, the composite-width async file.
2.8 Remaining: Select-into-captures; TakeNodesUntil/While migration; the async
   OperatorStack re-measure (the tabled wrapper-wave decision — the BFT collapse ratio is
   the strong signal).
3. (Only on demonstrated need) context-ful Where∘Where, DFT-first.
