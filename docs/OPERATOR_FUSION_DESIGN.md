# Operator Fusion (design record)

> **Status: DESIGNED 2026-07-15; phases 1-2 SHIPPED 2026-07-16 (see Phases), all rulings
> taken and measured.** The recipe surface is the consolidated one-property/two-method
> shape; the prune package is in progress (BFT accept seam done; signature migration and
> the PruneAfter wrapper remain). Companion decisions in
> [LAZINESS_AND_BUFFERING_POLICY.md](LAZINESS_AND_BUFFERING_POLICY.md);
> capability probing (the orthogonal "cheaper when rich" axis) in
> [TREE_CAPABILITY_INTERFACES.md](TREE_CAPABILITY_INTERFACES.md) — its takeaway after
> review: capability wins do not compound through composition, so that lattice stays
> demoted while fusion (which does compound) proceeds.

> **Vocabulary (settled 2026-07-17 after two renames):** the recipe surface speaks the
> MACHINERY — `ISelectTreenumerable` (the projection wrapper's compose, reviving the
> original interface name) and `ISelectWhereTreenumerable` / `SelectWhereTreenumerable`
> (the general wrapper: LINQ's WhereSelect precedent; in this codebase "Where" already
> names the generalized filter machinery that hosts the prunes). Both methods are `Compose`
> — both kinds compose; neither is "the" canonical composition (the interim
> Composable/Composition spelling implied exactly that and was renamed away). The stage
> carrier keeps the algebra name, `CompositionResult`. "Fusion" in this document names the
> *technique* — collapsing stacked layers into one wrapper — by its literature name
> (LINQ's fused iterators, stream fusion).

## Motivation

Composed operators stack wrapper treenumerators; every pull cascades through every layer
(position bookkeeping, visit republishing, and — on the async side — one state machine per
layer; the AsyncOverhead `OperatorStack` pair floors at ~1.5x for this reason). LINQ solves
this with internal fused iterators (`WhereSelectEnumerableIterator` etc.). Copse already
ships one fusion: `Select∘Select` via `ISelectTreenumerable.Compose`.

## The two laws (ratified)

1. **Fusion never reorders lambdas.** Collapse layers, never sequence: every user delegate
   fires for exactly the nodes, with exactly the contexts, in exactly the per-lambda order
   of the unfused pipeline. Side effects make reordering a semantics change, not a UX
   preference. Adjacent-pair fusion only — LINQ's line as well.
2. **Labels change only at emission.** Within a treenumerator every position is the INNER
   tree's label; user lambdas see their operand tree's coordinates unmodified by the
   operator's own structural edits (the Where drivers test
   `_Predicate(InnerTreenumerator.ToNodeContext())`). Promotion is logical; relabeling
   (depth compression, sibling renumbering) happens once, at the emission boundary — pinned
   by WhereTests (a(b(d,e,f),c) filtered on b emits d,e,f at (0,1),(1,1),(2,1), c
   renumbered to (3,1)). The frames carry both views (`OriginalPosition` + `Position`).
   Corollary: fusing k layers deletes k−1 emission boundaries, but each layer's
   would-have-been-emitted labels remain semantically live — the fused driver must compute
   them without synthesizing the intermediate streams.

## The load-bearing decision: value-only overloads are the fusion surface

**Compound ≠ stacked.** Over `a(b(c))` with p1 = node≠"b", p2 = depth≤1:
`Where(n => p1 && p2)` yields `a` (p2 judges c at source depth 2), while
`.Where(p1).Where(p2)` yields `a(c)` (p2 sees layer 1's emitted label, depth 1, after b's
removal promoted c). Position-observing predicates make naive predicate-ANDing a bug.

**LINQ's answer, verified against the BCL source:** only the value-only overloads fuse.
`Where(Func<T,bool>)` probes `Iterator<T>` and combines via
`CombinePredicates(p1, p2) == x => p1(x) && p2(x)` — legal because no coordinate is
observed. The indexed `Where(Func<T,int,bool>)` is a plain iterator block that never
participates; stacked indexed Wheres each keep their own counter (the second index counts
the first's survivors). LINQ shipped coordinate-observing filters for fifteen years and
never fused them.

**Copse adopts the same partition, with one twist: our only `Where` today is the
"indexed" one** (every predicate sees `NodeContext`), so machinery-level fusion would miss
the common case entirely. Therefore:

- Add **value-only overloads**: `Where(Func<TNode, bool>)`,
  `Select(Func<TSource, TResult>)` (and the prune twins). Position-blind by type ⇒
  compound ≡ stacked ⇒ adjacent same-kind fusion is delegate composition, trivially
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
  self-fuse (Select never relabels, so chained selectors see identical positions:
  `(n, p) => outer(inner(n, p), p)`); positional **Where** never fuses with its own kind.
  The general law: a pair is fusable iff no lambda observes a coordinate that a fused-away
  emission boundary would have relabeled.

## The API & UX design (ratified 2026-07-16)

**The context gradient — the one idea that explains the whole surface:** the richer the
context a lambda observes, the more real the machinery around it must be.

1. **Value lambdas** (`n => …`) — see nothing but the node; fuse anywhere, unconditionally.
   The simple tier is the fast tier, by design.
2. **Positional lambdas** (`(n, position) => …`) — see coordinates; fuse only across
   label-preserving prefixes, and otherwise their append point IS an emission boundary (a
   real layer stacks so the labels they read are genuinely emitted).
3. **`NodeVisit` / the treenumerator protocol** (`Do`, visit-stream consumers) — see the
   full traversal state; never fused, because the protocol they observe must physically
   exist where they watch it.

Users choose a tier by what they need to see; each tier carries the strongest guarantees
and best performance it can honestly offer.

**Ratified decisions:**

- **Fusion is completely invisible — never exposed.** No public docs mention it, no
  type surfaces it, no behavior reveals it (to pure lambdas). It is an implementation
  property, not a feature of the surface.
- **The purity contract** (the invisibility clause's fine print): computation lambdas
  (predicates, selectors, keys) must be pure — the library guarantees what each lambda
  observes and in what order, never how many times it runs. Concretely: over `a(b,c)`
  (7 visits, 3 nodes), an unfused Select evaluates its selector 7 times, the fused
  Select∘Where evaluates it 3 times; identical trees out, only an impure counter can tell.
- **`Do` is the sanctioned effect point, with a SPECIFIED cadence**: its action runs on
  every emitted visit and receives the full `NodeVisit` — deliberately permissive, because
  every narrower cadence is a one-line filter inside the caller's action
  (`Mode == SchedulingNode` = once per node; `VisitCount == 1` = first visits). `Do`
  therefore keeps `NodeVisit` through the signature migration (the deliberate survivor),
  never fuses, and inserting one between operators prevents their fusion by definition —
  the window materializes the pane. (It also serves as the honest force-unfused control in
  tests.) No value-flavored `Do` overload: it would be a lossy special case.
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
| value-only Where∘Where | **decided, unbuilt** | predicate combination (LINQ's `CombinePredicates`) |
| Select↔Where | **phase 1, substrate built** | genericized Where core: `<TInner, TNode>` + selector at the test site; Select is position-invariant so no label arithmetic exists |
| Select∘Prune | phase 2 | same seam on the prune cores — fusion is a dispatch table of pair types; prune's bespoke no-promotion machinery is never funneled through Where |
| Select∘capture-ops | phase 2, cheap | fold projection into the capture walk (the keyed factory's side-channel precedent) |
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
  interface is `ContainsRelabelingStage` plus ONE total method —
  `Compose<TOut>(stage, relabels)` where a stage is `Func<NodeContext<TNode>,
  CompositionResult<TOut>>` — returning the successor treenumerable directly: the wrapper
  unwraps its own mapping, composes, discards itself, constructs. One method suffices
  because the composition law subsumes fmap: a projection is a stage that never rejects
  (results carry `TraverseAll`), and the law composes it correctly without being told. The
  law's ONLY home is `SelectWhereTreenumerable.Compose` (first SkipNode stops the fold — a
  rejected node has no outer value; while accepting, values map and strategies union). The
  interim `CompositionMap` object had re-encoded the wrapper TYPE structure as runtime data
  and cost a transient object per composition step; deleted. The PROJECTION FAST PATH is a
  capability interface, `IAsyncSelectTreenumerable : IAsyncSelectWhereTreenumerable`, that
  ONLY `AsyncSelectTreenumerable` implements (`Compose` keeps projection∘projection
  on the light acquisition; an implementer is by construction projection-only, so its
  relabeling bit is always false) — the Select operator probes the capability first, then
  falls back to the general `Compose` with the never-rejecting stage. Optimizations belong
  to the optimized: the fast path is one type's virtue, not everyone's obligation.
  `AsyncPruneAfterTreenumerable` converts itself to the general representation and
  delegates (unwrap, discard, rebuild). Erasure unchanged: the wrapper knows its source
  type; the method is typed on output only. Each operator's stage semantics are stated
  ONCE — the compose branch reuses the plain path's selector struct as a method group
  (`new WhereResultSelector(p).GetResult`); PruneAfter's stage lives in the wrapper's
  `CreateStage`. `CompositionResult` is a BARE PAIR `(value, strategies)` — two fields, one
  constructor, zero behavior — rejection IS SkipNode membership, inherited from the
  consumer protocol, so every pair is coherent by definition. (An earlier Accept/Reject
  factory vocabulary was dropped in review 2026-07-16: once PruneAfter is an accept
  carrying skip instructions and PruneBefore a reject whose payload is the whole message,
  the case names no longer carry the semantics — the strategies value does, and the
  factories' implicit `| SkipNode` never fired at any call site.) Design lineage, each step
  Jason's: four flavor-hooks → two methods + property → one method + self-describing stage
  → one property + the map with combinators → two compose methods + the bit, map deleted →
  ONE compose method + the bit, fast path demoted to a single-implementer capability.
- **Fused machinery is the genericized core, not duplicated types**: `Where` drivers are
  `<TInner, TNode>` with a selector evaluated once per TESTED node against the source
  context; the path structs store projected values (values are opaque cargo — the library
  never compares nodes). Plain Where/PruneBefore instantiate `<TNode, TNode>` with a cached
  identity selector. Fused SelectWhere = the same driver with the real selector — zero new
  driver code.
- **Where needs a named wrapper**: today `Where` returns an anonymous factory-created
  treenumerable — nothing to probe. Phase 1 introduces `WhereTreenumerable` carrying its
  recipe, mirroring `SelectTreenumerable`.

## Measured gate (open ruling)

Identity-selector cost on plain Where (Job.Default A/B, branch vs main): one un-inlinable
delegate call + one extra `NodeContext` copy per tested node ≈ 1.2 ns — invisible on all
realistic rows, **+14%/+21% on Dft/Bft_Forest_DropAll** (the ~7 ns/node empty-loop
degenerate rows). Options: accept, or a struct-generic selector
(`TSelector : struct, ISelector<TInner,TNode>` — the engines' `TChildEnumerator` house
pattern; identity inlines to nothing, gate passes by construction; cost = a third generic
parameter on two internal drivers). The null-selector variant is disqualified
(`(TNode)(object)` boxes value-type nodes).

## Test strategy (agreed)

- **Force-unfused equivalence**: corpus × operator chains, fused pipeline vs directly
  constructed plain wrappers (bypassing probes), visit-stream identical — rides
  `VisitStreamConformance`.
- **Lambda-order pins**: recorded invocation sequences (which lambda, which node, which
  context) match the unfused pipeline — law 1 as a regression guard.
- **Open semantics ruling**: the fused seam evaluates selectors once per tested node; the
  unfused `SelectTreenumerator` re-evaluates per visit. Proposal: pin visit-stream
  equality and per-lambda order; declare selector invocation COUNT unspecified (purity
  expected). Also: BFT cross-predicate interleaving differs fused-vs-stacked (per-predicate
  order preserved); same ruling class.
- Benchmarks consult-first; after phase 1, re-run the async `OperatorStack` pair — fusion
  removes wrapper layers, which directly informs the tabled async operator-wrapper wave.

## The result monad (phase 2's composition model — Jason's original design, formalized)

This was the shape of the original `Compose` vision: the fused chain internally keeps a
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

Each fused stage is a Kleisli arrow `NodeContext<TSource> → Result<TStage>`; the fused
wrapper (`SelectWhereTreenumerable<TSource, TResult>`, replacing both `WhereTreenumerable` and
the anonymous SelectWhere result) is the reified composite arrow, and appending an
operator Kleisli-composes and returns a new wrapper — closed under composition: one
wrapper, any order, any length.

**Composition law** (what keeps fused ≡ stacked):
- `Accepted(v, s₁)` → next stage `Accepted(v₂, s₂)` ⇒ `Accepted(v₂, s₁ ∪ s₂)`.
- `Accepted(v, s₁)` → next stage `Rejected(s₂)` ⇒ `Rejected(s₁ ∪ s₂)`.
- `Rejected(s)` composes with nothing — the first rejecting stage ends evaluation (in the
  stacked pipeline, later layers never saw that node). Short-circuit early whenever the
  union reaches skip-everything.

**Stage vocabulary**: value-Where = `(v, pred ? ∅ : SkipNode)`;
PruneBefore = `(v, pred ? SkipNodeAndDescendants : ∅)`; PruneAfter =
`(v, pred ? SkipDescendants : ∅)` — the accept-with-strategy pair a bool cannot express, and
the reason the carrier is a result rather than a bool; Select = `(f(v), ∅)`.
Because the composite computes the FINAL value regardless of where filters sit among
projections, the Where-then-Select seam needs no emission-side driver surgery — it
dissolves into closure composition.

**The join rule, per lambda FLAVOR** (not per operator): value lambdas join any chain;
positional lambdas join only a label-preserving prefix — the wrapper carries one bit,
"contains a relabeling stage" (any filter/prune sets it; projections do not) — and
otherwise their append point IS an emission boundary (the chain terminates, a layer
stacks, the positional lambda reads genuinely emitted labels). This applies to positional
Select exactly as to positional Where, and to any future positional flavor.

**Required interface correction (phase 1 is correct only by topology luck):** the single
`FuseSelect` hook erases the appended Select's flavor — safe today only because the sole
accepting wrapper is the pure-Select wrapper. `SelectWhereTreenumerable` needs the flavor, so
the recipe surface splits like Where's pair: `FuseSelect(value)` /
`FusePositionalSelect(positional)` — four hooks, each wrapper answering per flavor.

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
   lambda); fused chains carry FuncResultSelector over the composed closure. Gate verified:
   the degenerate-row regression is GONE (Dft_Forest_DropAll 7.99 -> 6.90 ms vs main -- the
   inlined result slightly beats main's shape). Selector structs must stay
   stateless/readonly (defensive-copy trap, documented on the interface).
1. ✅ SHIPPED (branch, 2026-07-16): Where/Select signatures migrated to the arity split —
   (node) / (node, position), NodeContext removed from these operators (~150 call sites
   swept); unified internal `ISelectWhereTreenumerable` (FuseWhere / FusePositionalWhere /
   FuseSelect; hooks return null to DECLINE, the operator falls back to its plain wrap);
   named `WhereTreenumerable`; value Where∘Where by predicate combination; Select↔Where
   (both Where flavors) into the projection-carrying driver. FusionTests pin
   equivalence-vs-stacked, lambda order + early exit, compound≠stacked, positional-over-
   Select legality, and once-per-node selector evaluation on the fused path (the
   invocation-count ruling, now pinned rather than open).
2. ✅ SHIPPED (branch, 2026-07-16): `CompositionResult<T>` + result-shaped filter drivers (one
   composed evaluation per scheduled node; per-node reject strategies; DFT honors accept-side
   strategies via pending merge, BFT seam documented awaiting the PruneAfter stage);
   `SelectWhereTreenumerable` (the reified Kleisli arrow — value chains of any length/order
   collapse to one wrapper, pinned) with the four-hook flavor split and the relabeling bit;
   PruneBefore is a result stage and joins chains (removal polarity explicit in the
   result); the pure-Select wrapper stays distinct so projection-only chains keep the light
   Select treenumerator. NOTE: the result closure did NOT dissolve the delegate-layer
   question — plain Where now pays library-closure + user-predicate (2 calls/node), same
   magnitude as the phase-0.5 identity pair; the struct ruling transposes to
   `TResultSelector : struct, IResultSelector<TInner,TNode>` if taken.
2.5 ✅ SHIPPED (branch, 2026-07-16): the BFT accept-strategy seam (pending/deferred slots,
   verified against the bespoke PruneAfter oracle across ~4,600 interference sub-cases);
   the prune signature migration ((node)/(node, position) pairs; SkipTrees and Intersection
   migrated -- Intersection's prune now fuses into the merge pipelines); PruneAfter's named
   wrapper (bespoke no-promotion driver for plain acquisition, Map with relabels: false --
   label-preserving, so positional lambdas compose across it, pinned); the 8-symbol battery
   (584 chains, 376k cases/dim, interference bounded to chains of length <= 2). The extended
   battery immediately found a pre-existing bug: the bespoke PruneAfter driver tested the
   user predicate against the pre-enumeration ForestRoot sentinel -- guarded, user lambdas
   see real nodes only.
2.6 Remaining: Select-into-captures; narrow-receiver (D/B) fusion; TakeNodesUntil/While
   migration; the fusion benchmark rows + async OperatorStack re-measure (the tabled
   wrapper-wave decision).
3. (Only on demonstrated need) context-ful Where∘Where, DFT-first.
