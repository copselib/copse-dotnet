# The Ancestor Composer (the fourth cell) — SHIPPED 2026-08-18

*Drafted 2026-08-18 from the TakeSubtreesWhere dialogue; reframed the same day by Jason's
composer taxonomy; ruled BUILD ("let's do things the right way — we'll be glad we did
when we're done"). Sequenced after the reunification (OPERATOR_COMPOSITION_DESIGN.md
2.10): the cell is born onto a unified lattice with struct-composed splice legs.*

## 1. The taxonomy (Jason's frame, the ruling's basis)

Two axes classify every streaming composition stage:

- **Input scope** — what the stage's callback sees: the node alone (LOCAL), or the node
  plus a path-carried fact (ANCESTOR). There is no abstraction ladder above this —
  parent, grandparent, k-window, whole path are all ONE case, because the fold's
  `TAccumulate` is the caller's chosen summary of the root-to-node path (the seat rule,
  SCANRESULT_DESIGN 2026-08-04: ancestry threads through the state).
- **Output effect** — does the stage move surviving labels: NO (projections, subtree
  truncation) or YES (rejection with promotion). This axis already exists in code as the
  `Relabels` flag.

|  | labels preserved | topology mutated |
|---|---|---|
| **local** | light tier (`ISelectPruneAfter`) — Select, PruneAfter | driver tier (`ISelectWhere`) — Where, PruneBefore |
| **ancestor** | the scan product engines (the streaming citizenship) — `Scan().Select(f)` is ONE machine | **THE FOURTH CELL** — ancestor-conditioned rejection |

Three cells are inhabited by shipped, composable machinery. The fourth's only inhabitant
is degenerate: TakeSubtreesWhere's boolean stage, free because `kept ≡ not-skipped` rode
the Where machinery's existing skip prefix. The general inhabitant is THE FOLD-CARRYING
DRIVER, and building it completes the composition algebra: every cell composable, one
machine per chain stretch whose stages share a cell row, one carrier engine only when the
accumulate is itself the output.

**Right vs left of the scan (Jason's split):** composing to the scan's RIGHT (scan, then
filters/projections) is this cell — one machine, feasible now. Composing to its LEFT
(stages upstream of the scan) is the compose-left DOOR grammar (`CaptureThrough`,
pieces-as-data — the two-verb distinction) and stays a separate, deferred mechanism (the
rootfix door).

## 2. The licensing law (unchanged from the draft)

- **Fold-as-output** (bare `RootfixScan`): every emitted node carries its accumulate —
  the machine owning the path state must be the emitter. The scan ENGINE, at its floor
  since the emission mint (bare accumulates in state, products minted at emission).
- **Fold-as-control-or-ingredient** (the fourth cell): downstream stages CONSUME the
  accumulate — test it, project through it, discard it. The driver owns the fold as
  carried state; what reaches the output is minted at emission
  (**the output-reachability erasure rule**: intermediate types that don't reach the
  output are machine state at their own width; what reaches the output is emission).
- Scope: INHERITED folds only. A synthesized (leaffix) fact does not exist at scheduling
  time; SelectSubtreesWhere remains a capture-side candidate.

## 3. The machine

The fold-carrying driver: the Where-family machinery plus

- a `TAccumulate` slot on the path entries (DFT: the accepted/skipped stack frames; BFT:
  the queue entries plus a re-anchored per-depth carry, the skip prefix's shape);
- the fold runs at SCHEDULING, exactly once per scheduled node, parent slot in hand:
  `acc = accumulator(parentAcc, node)` — including for REJECTED nodes (their descendants
  fold through them: a dropped node's children still descend from its accumulate, exactly
  as the two-machine spelling behaves — composition is data flow);
- the result-selector legs see `(node, acc)` — the pair minted transiently at the
  decision site and at emission, never stored (the emission mint);
- promotion/relabel machinery unchanged — rejection semantics are the driver's, blind to
  why the predicate said no.

**The hard gate:** plain Where is the hottest machinery in the library and must not move
a byte. RULING: the fold-carrying driver is a TWIN family
(`ScanWhere*Treenumerator` or similar under the naming grammar), not a genericized slot
in today's driver — mirroring how every plain operator keeps bespoke machinery. Today's
Where treenumerators stay byte-identical.

## 4. Entry points (who composes into the cell)

1. **The scan citizen's rejection door:** a rejecting operator landing on the streaming
   scan citizen (`AsyncRootfixScanTreenumerable` / product variant) constructs the
   fold-carrying driver from the citizen's recipe (accumulator, seed, product selector)
   plus the predicate — `Scan().Where(pair => …)` becomes ONE machine. Further
   Select/Where compose into that driver's legs (the lattice's normal story).
2. **TakeSubtreesWhere's general form** — and prospective one-line operators
   (depth-window filters, path-budget filters). The BOOLEAN case keeps its free
   subtree-mode encoding -- MEASURED AND CLOSED 2026-08-18: the staged spelling costs
   1.5-2x on time and 3-5x on allocation vs the free encoding (deep-match chain, both
   dims: 37.4ms/8.2MB vs 58-70ms/43.9MB DFT) -- zero state on the skip prefix beats a
   real fold slot, permanently. The boolean machinery is the cell's optimized degenerate
   instance and STAYS.

## 5. Implementation questions (all settled)

1. The positional seat — settled as written: the internal fold speaks the context-shaped
   accumulator (as the scan engines do); the public surface stays seat-rule-minimal.
2. The `Relabels` interaction — settled as written: the fold sees INNER positions (its
   input tree — the data-flow law); positional legs downstream obey the existing join
   rule.
3. Benchmarks — DONE: the ScanWhere witness pair was seeded BEFORE the machinery
   (`1f128c9`, ~1.0 baseline) and the flip is on the CI record (composed below stacked,
   both dimensions); the Where family rows guard the untouched plain driver.
4. Battery — DONE: the fourth cell joined SelectComposableLawTests (equivalence pins
   against the two-machine oracle over the corpus, both dimensions) plus strategy-matrix
   conformance for the new treenumerators; full battery green at landing.
