# ScanResult: the canonical pairing (ratified 2026-08-02; seat rule + landing rule 2026-08-04)

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
- **Surveys**: ~~`(TNode subject, TDispatch arrival, view)` — the subject is the
  OPERAND~~ **OVERTURNED same day by the unification (below): the rootfix survey is
  `(TDispatch arrival, targets)` — the subject was a derivable seat.** A node's arrival is
  authored at its parent's dispatch site with the node in hand as the target's `.Node`, so
  any subject-shaped fact flows inside `TDispatch`; the operand is the FAMILY, and the
  family is (arrival, targets). The leaffix survey keeps its subject — upward flow means
  the node's own value passes through nobody else's hands (each survey keeps exactly the
  seats its flow direction cannot derive). A parent-centric rule is not a fold with a
  missing parameter — it is a survey.
- **Positional flavors** are justified seats: a node's coordinates are machinery-owned
  and genuinely underivable (`Where` renumbering), rationed by arity-split.
- **`ScanResult` appears in NO callback input.** Its only home is the pure results and
  the aggregates' yields. Consequence: **every callback is shared verbatim between an
  operator and its Do twin** — only the landing differs. (Taken to its endpoint by the
  landing rule below: RootfixDoScan's fold is now literally the pure accumulator's
  parameter, impurity sanctioned.)

## The delivery model (2026-08-04; the Do dispatches are deleted — historical, but the sequencing analysis informed the demotion)

`Dispatch` DELIVERS. The pure operator's `Dispatch` writes into the result pairing; the
Do operator's writes onto the caller's entity via `store`, the landing rule declared
once. The seed is a delivery to the roots. This replaces the "two callbacks, two
contracts" framing whose decoy-mutation reading confused even the design's author at his
own call site.

**Re-founded (same day, after adversarial testing): the docs pin SEQUENCING, not
atomicity.** Stores fire in preorder after the pass completes and validates — that is the
contract, mechanism not morals. Untouched-on-pass-failure follows as a corollary the
caller derives in one step; a throwing *store* leaves the preorder prefix landed, so
"all-or-nothing" was never fully true and is no longer claimed. Atomicity is a free
byproduct of the capture-class build (the arrivals array exists anyway), demoted from
purpose to property. `store`'s seat (as re-argued after full participation): the survey's
writes are EDGE-grained deliveries into machinery slots; `store` is the NODE-grained
landing rule, declared once, applied after validation — landing inside the survey would
be two acts per call site (`dt.Node.X = v; dt.Dispatch(v)`), the forgettable-second-act
trap. (The original "surveys don't reach every node" coverage argument was retired by
full participation; the leaffix merge question this opens is tier 3, held.) The
tested-and-rejected alternatives (Dispatch-carries-the-mutation: the two-act trap, kills
the value channel, allocates a closure per edge; `DoDispatchWithValidation` twin: the
buffer is theorem-forced by sibling-completeness, so the "plain" variant differs only by
a weaker failure posture no workload wants) are recorded in RootfixDoDispatch's doc.

## The family equation and the landing rule (2026-08-04; the equation's right-hand side is now the ONLY side — see THE DEMOTION)

> **`XDoY ≡ XY(pure) ∘ Do(scheduling-filtered effect) ∘ Select(.Node)`**

Every Do operator is derivable from the pure tier plus `Do` plus `Select` — the family
has **zero unique algorithmic content**; it is contract plus fusion ("sugar + license",
LINQ's `Average`-over-`Sum`/`Count` status). What the dedicated operators sell over the
composition: (1) a NODE-GRAINED effect contract over a visit-grained stream — `Do` fires
per visit event (a k-child node emits 1 S + k+1 V), so the composed form needs the
scheduling-mode filter that the obvious call site forgets; the dedicated operators make
that trap inexpressible; (2) the effect-class defaults (capture-class Do operators fire
once per build; the composed chain refires per drain unless pinned); (3) one call. The
composition is the documented escape hatch — and the conformance ORACLE: the
`DoFamilyCompositionOracleTests` battery pins dedicated ≡ composed over a mutable corpus.

**The landing rule** (the fold-shape resolution, after the ecosystem detour): *the
callback that produces a node's value lands it; where no callback produces a node's value
with the node in hand, `store` lands for you.* Consequences:

- **RootfixDoScan MERGES** — the family's one shape where one callback per node produces
  that node's value. `RootfixDoScan(seed | rootSelector, Func<TAcc, TNode, TAcc> fold)`:
  the fold's return both lands on the node and flows to its children (C# assignment is an
  expression, so `(a, n) => n.Total = a + n.Weight` is a valid fold; docs lead with the
  block form). Under the selector flavors the fold never fires at roots, so **the selector
  is the root's landing** — the author's original instinctive call site, now correct by
  design. Implementation is literally `RootfixScan(seed, fold).Select(r => r.Node)` (the
  old `(compute, store)` split's `ComputeStoreAccumulator` fused them into exactly this
  fold before the machinery ever saw them — the API now speaks the machinery's shape).
  The operator's remaining content is the LICENSE: fold invoked exactly once per node per
  traversal, effects sanctioned — where the pure scan keeps the permissive
  unspecified-counts clause.
- **LeaffixDoScan keeps `store`** — its binary combine fires per CHILD EDGE (zero times
  on leaves, k times on a k-child node, no invocation knowably last), so no fold
  invocation ever holds a completed accumulation.
- **Both DoDispatches keep `(survey, store)`** — the surveys don't reach the leaves.
- Rejected fold shapes: bare `Action<TAcc, TNode>` (severs the accumulate chain — the
  machinery cannot read the flow back off the node); `Action` + read-back selector (the
  author's original form — fuses into the Func anyway one layer down; sugar over sugar).

## THE DEMOTION (ratified 2026-08-04 night — composition wins)

**The Do quartet is DELETED** (`RootfixDoScan`, `LeaffixDoScan`, `RootfixDoDispatch`,
`LeaffixDoDispatch`, both colors). The sections above and below that describe their design
are the genealogy of how the deletion was earned — each round of convergence shrank the
family's unique content until nothing but derivable sugar remained. The closing arguments:

1. **Derivability** (the family equation): zero unique algorithmic content — pure ∘ Do ∘
   Select expresses everything, and the mode filter is the honest price of the
   visit-grained truth.
2. **The pinning principle** (the deserialize-means-Defer precedent): `ITreenumerable` is
   a contract to re-enumerate; effects ride enumeration, per drain; pinning is the
   CONSUMER's `Materialize`/`Memoize`. The capture-class Do operators fired effects once
   per build — an effect schedule the consumer could not un-pin: the same disease the
   string tier was cured of.
3. **The buffer argument** (the final nail): a "safe" DoOnce would have to return a
   buffer, i.e. be `Do().Materialize()` or `Do().Memoize()` — and choosing between them
   is situational, so the operator cannot exist without deciding for the consumer. The
   recipe is the API.
4. **The gold-plating slope**: DoOnce/DoNTimes/DoIf/DoWhen are all `Do` plus an in-band
   conditional — derivable seats with no non-arbitrary stopping point.

**The surviving surface**: the pure tier (ScanResult results), `Do` (visit-grained,
unchanged), `Select`, and the consumer's pin. The canonical landing idiom:

```csharp
tree.RootfixDispatch(10_000m, AllocateByWeight)
    .Do(visit =>
    {
      if (visit.Mode == TreenumeratorMode.SchedulingNode)
        visit.Node.Node.Amount = visit.Node.Accumulate;
    })
    .Select(pairing => pairing.Node)
    // .Materialize() -- the consumer's exactly-once pin, when wanted
```

Effect semantics, precisely: effects fire per drain, per scheduled node, in that
traversal's scheduling order; pruned subtrees never fire; partial drains fire partial
prefixes; overlapping treenumerators interleave — all inherited from `Do`'s existing
contract and identical to impure `IEnumerable` chains. `Materialize` is definitionally
one full traversal, so the pin removes the partial/double hazards. **The admission bar
for any future Do variant: a workload the mode filter cannot serve.** Tier 3 (below) is
resolved by deletion. `DoLandingCompositionTests` pins the idiom.

## Full participation (ratified 2026-08-04, same day — boundary-shape-follows-tier-shape)

The alpha.10 root-asymmetry verdict ("I don't see why roots should be treated differently
than other levels" — and they shouldn't): **a tier's boundary must speak the tier's own
shape, and no node class is excluded from its tier's callback.**

- **Rootfix dispatches — UNIFIED (same day; the `rootSurvey` intermediate lived one
  tag)**: the roots are the children of the VIRTUAL FOREST ROOT
  (`NodePosition.ForestRoot`, the machinery's standing convention). A separate rootSurvey
  callback duplicated the dispatcher ("why do we have to duplicate the dispatcher for
  roots?"), and the duplication exposed the survey's SUBJECT as a derivable seat (see the
  seat rule above). With the subject dropped, ONE survey `(arrival, targets)` serves
  every family — the machinery invokes it for the virtual root's family first
  (`(seed, roots)`), then per internal family: **the boundary is an INVOCATION, not a
  callback.** The seed flavor IS the participation form; roots participate with zero
  ceremony; a budget allocates ACROSS a forest's roots with the same callback that
  allocates everywhere else. The rootNodeSelector flavors survive as sugar for roots that
  follow a different, per-root rule. A distinct sibling-complete ROOT rule
  (rootSurvey's only unique power) waits for a real workload — the house resurrects
  seats on demand.
- **Leaffix dispatches**: the survey fires on EVERY node — a leaf's sources view is
  EMPTY, not skipped (`sources.Count == 0` is the in-band leaf test). The survey-only
  overload is the general form ("my value plus my children's rollups" needs no boundary
  at all); the seed/selector flavors are sugar wrapping the survey with a leaf branch.
- **Scans — ARRIVAL SEMANTICS (2026-08-04 night, the "1(2,3,4)" verdict)**: the rootfix
  selector flavor was the fold tier's own participation defect — the selector's return
  WAS the root's accumulate and the fold skipped roots, so the pure and effect-composed
  forms diverged at roots (composed lands the selector's value; nothing else does). Fixed:
  the selector supplies the root's ARRIVAL, the fold fires at EVERY node
  (`accumulator(selector(root), root)`, exactly as the seed flavor), the seed flavor is
  the constant selector, and the invocation sentinel is n, not n − roots. The leaffix map
  was always per-node — no change.
- ~~**HELD OPEN (tier 3)**: the re-opened dispatch `store` seats~~ — RESOLVED BY
  DELETION (see THE DEMOTION): the Do dispatches no longer exist, so there is no store to
  merge. The pure surveys' seats are settled (rootfix subject-less; leaffix
  subject-bearing).

## The recording rule (the alpha.9 edge-1 clause)

Folds record their OUTPUT (the node's accumulation). The rootfix survey records its
INPUT — the arrival — because it is the family's one 1-in-n-out shape: no node-grained
output exists, and its n outputs are recorded as its children's arrivals. Forced, not
accidental; stated on `ScanResult` and `RootfixDispatch`.

## The Do-tier ruling (historical — see THE DEMOTION)

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
| Do store: (node, arrival) | Do store: (node, rollup) | matched — born dual (the dispatch tier) |
| fold tier: landing rides the fold's return (RootfixDoScan MERGED — one callback per node produces that node's value) | fold tier: combine is child-edge-grained (0× on leaves, k× else) — `store` keeps its seat | forced-different — the landing rule (2026-08-04) |
| boundary = an INVOCATION of the same survey: the virtual root's family goes first, (seed, roots) | boundary = an invocation of the same survey with empty sources (the fringe is every family's base case) | matched — full participation, unified: boundaries are invocations, not callbacks; flavors are sugar |
| survey `(arrival, targets)` — subject DERIVABLE (the arrival is authored with the node in hand at the dispatch site) | survey `(subject, sources)` — subject UNDERIVABLE (upward flow: the node's value passes through nobody else's hands) | forced-different — each survey keeps exactly the seats its flow direction cannot derive (2026-08-04) |
| fold tier: seed ≡ constant selector (both arrivals transform through the fold — a theorem, pinned) | survey tier: seed ≠ constant selector — TWO INSTRUMENTS (seed = one budget divvied by the survey at the virtual family; selector = known per-root budgets set directly, bypassing the survey) | forced-different — arrival IS the value on the survey tier, so the virtual-family survey is the only transformation point and a per-root selector cannot route through a family-grained callback (the "1(2,3),4(5,6)" probe, 2026-08-04; pinned deliberately-different) |
| survey never reaches leaves (they have no children) → `store` is the every-node channel | survey reaches EVERY node since full participation → `store` derivable in principle; merge HELD OPEN (tier 3) | forced-different since full participation — was "matched, born dual" |

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
