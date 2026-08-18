# The Scan Tier (DRAFT — awaiting ratification)

*Drafted 2026-08-18, from the TakeSubtreesWhere dialogue (Jason's typed-pipeline framing).
Sequenced deliberately after the reunification (OPERATOR_COMPOSITION_DESIGN.md 2.10): the
tier is born onto a unified lattice with struct-composed splice legs.*

## 1. Origin: the typed pipeline

Jason's unwrapping of `tree.Select(f).PruneAfter(q).TakeSubtreesWhere(p)`:

```
T1 -> select -> T2 -> pruneAfter -> T2 -> scan -> (bool, T2) -> where -> (bool, T2) -> select -> T2
```

The pipeline is the SPEC; the machine erases what it can. `(bool, T2)` exists only in the
type algebra — the bool rides driver state, `T2` rides the emission, and the pair is never
constructed because the final projection discards it and no consumer can observe it.

## 2. The licensing distinction: fold-as-output vs fold-as-control

- **Fold-as-output** (RootfixScan): every emitted node carries its accumulate — the machine
  that owns the path state must be the emitter. This FORCES a scan engine and is why
  RootfixScan cannot be a Select (the stateful-selector laws: Select's invocation count is
  deliberately unspecified; a fold smuggled into a selector corrupts under composition —
  the Do lesson generalized).
- **Fold-as-control** (TakeSubtreesWhere): the accumulate is consumed internally and
  discarded. Internal control state is what a DRIVER is allowed to own — the Where
  machinery already carries O(depth)/O(width) path state.

**The output-reachability erasure rule:** in a stage pipeline, an intermediate type that
does not reach the output type is machine state at its own width; what reaches the output
is emission. This rule decides, per composed chain, whether the fold lives in the driver
(erased) or demands the scan engine (emitted).

## 3. Scope: inherited folds only

The tier's fold is `acc(n) = f(acc(parent(n)), n)` — an INHERITED (rootfix-direction,
top-down) attribute, available at the node's scheduling because the parent's accumulate is
already in path state. A SYNTHESIZED (leaffix-direction) fact — "subtree contains a match",
subtree sums — does not exist until the subtree closes, so no streaming driver can consult
it at scheduling. **SelectSubtreesWhere is therefore NOT a one-line rewrite into this
tier**; it remains a capture-side candidate (compositions over LeaffixScan, per
SELECT_INTO_CAPTURES_DESIGN.md section 5).

## 4. The stage, mechanically

The general driver family gains an optional fold stage: a `TAccumulate` slot per path
entry (DFT: the accepted/skipped stacks; BFT: the queue entries plus a re-anchored
per-depth carry, exactly the shape of the skip prefix), with:

- **fold at scheduling**, exactly once per scheduled node (the driver's cached decision
  point — the invocation contract TakeSubtreesWhere's spelling already relies on);
- predicate/selector legs may CONSUME the accumulate (the pair minted transiently, the
  emission-mint discipline — never stored);
- **composition = data flow**: the fold sees its INPUT stream — upstream prunes starve it
  naturally; downstream rejections fire promotion machinery without disturbing fold state
  (the sandwich `scan → where` answers this by construction; the merged stage must
  preserve it).

The boolean special case is already shipped and free (the subtree stage: kept ≡
not-skipped, riding the existing skip prefix — zero new state). The general case is NOT
free: a `TAccumulate` slot widens path entries. **The plain-Where guard is therefore a
hard gate: the stage must cost plain filters nothing** — likely a separate generic driver
variant (the no-fold driver keeps its exact shape), mirroring how plain operators keep
bespoke machinery everywhere else in the lattice.

## 5. What it subsumes / deletes

- TakeSubtreesWhere's remaining scaffolding: the citizen dispatch pair and the bespoke
  DFT wrapper collapse into "a rewrite into the driver" once the stage exists (the bool
  fold + where(acc) + select(.Node), erased) — IF the gate in §4 holds.
- New one-line operators fall out: depth-window filters, ancestor-conditioned filters,
  "keep while path-budget lasts" — any inherited-fold + filter + discard chain.
- `RootfixScan(...).Where(pair => keep(pair.Accumulate)).Select(pair => pair.Node)` for
  arbitrary TAccumulate: today scan engine + driver; with the stage, one driver.

## 6. Open questions (the ruling list)

1. Driver bifurcation vs generic unification: separate fold-carrying driver types, or one
   generic family with a zero-cost no-fold instantiation? (The gate: plain Where unmoved.)
2. One fold slot or many? Chained scan stages compose as a product accumulate — defer
   until a workload demands it?
3. Where does the rewrite live — does `TakeSubtreesWhere` construct the staged driver
   directly, or does a citizenship-style probe recognize scan→where→select suffixes?
   (Recommend: direct construction; suffix recognition is machinery for a pattern users
   never write by hand.)
4. Positional flavors: the fold callback's seat (context accumulator, as the scan engines
   speak internally)?
5. Benchmark shape: the TakeSubtreesWhere family is the natural witness (consult-first on
   any new rows).
