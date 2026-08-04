# ScanResult: the canonical pairing (ratified 2026-08-02; seat rule 2026-08-04)

> **Status: RATIFIED 2026-08-02 (Jason; the coffee-walk session), swept on
> `feature/do-scan` ahead of the alpha. AMENDED 2026-08-04 (the alpha.9 LINQPad verdict):
> the SEAT RULE below supersedes this document's callback-input clause -- the pairing's
> only home is the RESULT; callbacks speak the minimal basis. The 2026-08-02 sweep's
> ScanResult-parent accumulator was reverted accordingly.** Vocabulary ratified same date:
> `ScanResult<TSource, TAccumulate>` with `.Node` / `.Accumulate` ("scan" read broadly as
> the aggregation family); the leaffix survey view renames to **DispatchSources** — the
> dual of DispatchTargets (write-handles down, read-handles up), accepting the "source"
> overload cost with eyes open.

## The observation everything follows from

The (node, accumulate) pairing kept independently forcing itself into existence: the
consumer toys hand-threaded `(Name, Total)` tuples because scan results lost the node;
`DispatchNode` was invented because the dispatch result needed the pairing; the Do
operators' internals threaded `(TNode, TAccumulate)` tuples through the pure scan; and
dag-side `DagInflow`/`DagDispatchInflow` are the same shape with an edge attached. Three
independent inventions is recognition, not speculation — and it is the tree-side instance
of the dag family's dispatch-provenance principle: **the pairing comes from the API,
never smuggled through the payload.**

## What canonicalizing buys

1. **`NodeContext<TAccumulate>` dies** — the accumulate-disguised-as-a-node parameter, the
   most confusing type in the scan signatures (`a.Node` returned an accumulate). The
   accumulator's parent parameter becomes `ScanResult<TNode, TAccumulate>`: `.Node` is the
   actual parent node — **previously unavailable at all** without value-smuggling —
   and `.Accumulate` its accumulation. The scans go value-flavored in the same stroke.
2. **Pure results decorate everywhere.** `RootfixScan`/`LeaffixScan`/`LeaffixDispatch`
   return ScanResult trees; the aggregates yield ScanResult sequences (a leaf/root paired
   with its fold). Project `.Accumulate` when only values are wanted. `DispatchNode` is
   subsumed and retired.
3. **The leaffix survey gains child identity.** `DispatchSources` elements carry the
   child's context alongside its accumulation, mirroring `DispatchTarget`.

## The two homes of the pairing (position ruling)

- **Callback-context types** (`DispatchTarget`, `DispatchSource`): carry the full
  `NodeContext` — immediate, consumed in place, cannot go stale.
- **Traveling values** (`ScanResult`): node + accumulate ONLY, deliberately no position —
  **in-band positions go stale under composition** (`Where` renumbers siblings, promotion
  compresses depths). The visit stream is the single source of truth for coordinates.

## The seat rule (ratified 2026-08-04 — supersedes the callback-input clause above)

> **A parameter earns a seat iff the caller cannot derive it from the seats already at
> the table.**

`TAccumulate` IS the caller's chosen summary of the root-to-node path — that is what a
fold's state parameter *means*. Parent entity, grandparent, the full ancestor list: every
ancestry-shaped fact is a particular path statistic, all served by the state channel, and
privileging any one of them is bolting a derivable axiom to the API (the grandparent
slippery-slope is the proof — there is no non-arbitrary place to stop). Hence:

- **Folds** are `(TAccumulate, TNode)` everywhere — LINQ Aggregate's shape; the pure
  accumulator and the Do tier's `compute` are the SAME shape.
- **Surveys** are `(TNode subject, TDispatch arrival, view)` — the survey's node is not
  ancestry-context but the OPERAND (you cannot survey a node you were not handed); it
  keeps its seat. A parent-centric rule is not a fold with a missing parameter — it is a
  survey.
- **Positional flavors** are justified seats: a node's coordinates are machinery-owned
  and genuinely underivable (`Where` renumbering), rationed by arity-split.
- **`ScanResult` appears in NO callback input.** Its only home is the pure results and
  the aggregates' yields. Consequence: **every callback is shared verbatim between an
  operator and its Do twin** — only the landing differs.

## The delivery model (ratified 2026-08-04 — the Do dispatches)

`Dispatch` DELIVERS. The pure operator's `Dispatch` writes into the result pairing; the
Do operator's writes onto the caller's entity via `store`, the landing rule declared
once. The seed is a delivery to the roots. All deliveries land together when the pass
completes VALIDATED — a failed pass lands nothing (all-or-nothing effects). This replaces
the "two callbacks, two contracts" framing whose decoy-mutation reading confused even the
design's author at his own call site.

## The recording rule (the alpha.9 edge-1 clause)

Folds record their OUTPUT (the node's accumulation). The rootfix survey records its
INPUT — the arrival — because it is the family's one 1-in-n-out shape: no node-grained
output exists, and its n outputs are recorded as its children's arrivals. Forced, not
accidental; stated on `ScanResult` and `RootfixDispatch`.

## The Do-tier ruling

`Do` = declared intent to mutate the nodes, so the nodes ARE the result: ScanResult never
travels through Do operators — pass-through is forced by the semantics, not preference
(a packaged accumulate would duplicate what `store` landed). `store` stays destructured
`(node, accumulate)`: pairing types are for values that travel, not callback parameters.
The flow channel (arrival, store, slot validation) is unchanged.

## The duality audit (the convergence instrument)

The design is done when every DispatchTargets/DispatchSources cell is either **matched**
or **forced by the direction of information flow** — never accidental. Dual ≠ mirror:

| Rootfix (down) | Leaffix (up) | Verdict |
|---|---|---|
| survey distributes: 1 arrival in, n writes out | survey collects: n reads in, 1 result out | matched — Action+in-param ↔ Func+return |
| exactly-once WRITE: runtime protocol (flags, throws) | exactly-one RESULT: the return type | forced-different — n obligations need runtime enforcement; one obligation is the type system's |
| roots take the seed (no parent) | leaves take the seed (no children) | matched — the boundary pair |
| `DispatchTarget`: context + write facility | `DispatchSource`: context + read value | matched (this sweep) |
| O(1) Count + indexer via the child-index | O(1) Count + indexer via the child-index | matched (this sweep — the leaffix build restructured to capture-then-fold, sharing the rootfix passes) |
| `DispatchTargets` | `DispatchSources` | matched (this sweep) |
| pure result decorates (`ScanResult`) | pure result decorates (`ScanResult`) | matched (this sweep — leaffix previously REPLACED) |
| survey records the ARRIVAL (its input; no node-grained output exists) | survey records its OUTPUT (n-in-1-out has one) | forced-different — the recording rule (2026-08-04) |
| callbacks: minimal basis — subject + flow state, pairing in results only | callbacks: minimal basis — subject + flow state, pairing in results only | matched (the seat rule, 2026-08-04) |
| Do store: (node, arrival) | Do store: (node, rollup) | matched — born dual |

Any future operator pair gets this audit before shipping.

## Costs, stated

- Value-only consumers of pure scans pay `.Select(r => r.Accumulate)` (projection-only
  chains compose; benchmarks watch the delta).
- The leaffix build restructures from single-pass streaming fold to capture-then-fold
  (reverse-preorder over the raw capture) so DispatchSources can be O(1)-indexed and
  context-carrying — the same passes as the rootfix build, now genuinely shared. The
  per-root-streaming affordance was never LeaffixDispatch's (it always captured whole);
  it remains LeaffixAggregate-D's, whose bespoke build is untouched.
- Positional accumulator flavors stay deferred to the signature workstream; the
  boundary selectors (root/leaf) carry value | positional flavors now.
