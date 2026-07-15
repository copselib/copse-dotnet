# Operator Fusion (design record)

> **Status: DESIGNED 2026-07-15; deliberately not yet built.** Phase 0 (internal recipe
> surface) is on main; the genericized-Where substrate lives on `feature/operator-fusion`
> (a7318b5) with one measurement-gated ruling pending. Everything else here is decided
> design awaiting a build session. Companion decisions in
> [LAZINESS_AND_BUFFERING_POLICY.md](LAZINESS_AND_BUFFERING_POLICY.md);
> capability probing (the orthogonal "cheaper when rich" axis) in
> [TREE_CAPABILITY_INTERFACES.md](TREE_CAPABILITY_INTERFACES.md) — its takeaway after
> review: capability wins do not compound through composition, so that lattice stays
> demoted while fusion (which does compound) proceeds.

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
- The `NodeContext` overloads keep today's exact semantics and never fuse with their own
  kind (they may still fuse with position-invariant partners — see matrix).

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
- **Double dispatch is forced by generics**: the outer operator cannot name the inner's
  erased `TSource`, so fusion hooks live on the inner wrapper (`Compose`, `FuseWhere`, …) —
  tell, don't ask. Candidate shape: an internal base class for wrapper treenumerables with
  virtual hooks defaulting to plain wrapping (LINQ's `Iterator<T>` shape), since interfaces
  can't carry defaults on the older TFMs.
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

## Phases

0. ✅ Recipe surface internal + param hygiene (main 09a760f).
0.5 ✅ Genericized Where substrate (branch a7318b5); ⏳ struct-selector ruling.
1. Value-only `Where`/`Select` overloads; `WhereTreenumerable` + probes; `FuseWhere` on
   `SelectTreenumerable`; value-only Where∘Where by predicate combination; the test
   battery.
2. Prune pairs; Select-into-captures.
3. (Only on demonstrated need) context-ful Where∘Where, DFT-first.
