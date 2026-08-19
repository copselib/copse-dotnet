# ScanResult: the canonical pairing (ratified 2026-08-02; seat rule + landing rule 2026-08-04)

> **Status: RATIFIED 2026-08-02 (Jason; the coffee-walk session), swept on
> `feature/do-scan` ahead of the alpha. AMENDED 2026-08-04 (the alpha.9 LINQPad verdict):
> the SEAT RULE below supersedes this document's callback-input clause -- the pairing's
> only home is the RESULT; callbacks speak the minimal basis. The 2026-08-02 sweep's
> ScanResult-parent accumulator was reverted accordingly. RENAMED 2026-08-06 (the
> recording rule made type-level): `ScanResult` is now TWO payload-named pairings,
> `NodeAccumulation` (output) and `NodeArrival` (input, RootfixDispatch's) -- see The
> recording rule below; this document keeps its historical filename.** Vocabulary
> ratified same date:
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
has **zero unique algorithmic content**; it is contract plus collapse ("sugar + license",
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
  old `(compute, store)` split's `ComputeStoreAccumulator` collapsed them into exactly this
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
  author's original form — collapses into the Func anyway one layer down; sugar over sugar).

## THE NORTH STAR (ratified 2026-08-05 — cross-tier flavor coherence)

> **A scan is the fold-shaped dispatch: for EVERY boundary flavor,
> `Scan(boundary, fold)` ≡ `Dispatch(boundary, (a, dts) => { foreach dt:
> dt.Dispatch(fold(a, dt.Node)); })`.** Pinned by `CrossTierCoherenceTests`; every future
> boundary flavor must join the battery. (Since THE VIRTUAL-ROOT RULE, 2026-08-06, the
> quantifier is EXACT: every surface flavor has a same-boundary twin on the other tier.
> As ratified it was overstated — the leaffix scan's seed flavor had no same-boundary
> dispatch twin and pinned through the translated boundary `nodeAcc(seed, ·)`; the rule
> retired that flavor rather than keep the translation clause.)

The invariant makes the TWO INSTRUMENTS uniform across the tiers (subsuming the
2026-08-04 two-instruments ruling, which had them per-tier-asymmetric):

- **The SEED is the virtual root's arrival**, transformed by the tier's callback — the
  fold at every node (`fold(seed, root)`), the survey at every family (the virtual
  root's first). One value the callback speaks over; everything participates. Rootfix
  only, since THE VIRTUAL-ROOT RULE below.
- **The SELECTOR sets each boundary node's value directly**, bypassing the callback —
  known per-node values, the explicit instrument. On the survey tier arrival IS the
  value, so the bypass sets arrivals; on the fold tier it sets accumulations.
- Consequently seed ≠ constant selector wherever both exist (pinned
  deliberately-different), and the flavors' meanings never shift as a consumer moves
  between tiers.

**The leaffix corollary (same day; subsumed 2026-08-06 by THE VIRTUAL-ROOT RULE)**: a
seed exists only where the flow has an entry channel for it to participate through. The
leaffix DISPATCH survey has no arrival seat — its broadcast-seed flavor was the bypass
instrument wearing the seed's name (identically `_ => x`) and stays DELETED; leaves are
set by SELECTOR flavors. (The survey-only overload followed it on 2026-08-05: the
family's one fixer-less signature -- TAccumulate appears only inside the lambda, so
inference structurally fails; the type-fixer-first grammar, enforced by the compiler. The
use-case survey showed the sibling-comparative workloads this tier exists for need a leaf
rule anyway, and formula-shaped fringes belong to LeaffixScan's dual fold. Full
participation persists internally -- the pass surveys every node; the selector flavors
are its public face.)

## THE VIRTUAL-ROOT RULE (ratified 2026-08-06 — seeds belong to an object, not a shape)

The corollary's seat test was mechanism-visible, not consumer-visible, and it priced in
an inconsistency: the leaffix SCAN kept a seed flavor that was extensionally pure form —
`LeaffixScan(seed, edge, nodeAcc)` ≡ `LeaffixScan(leaf => nodeAcc(seed, leaf), edge,
nodeAcc)` on every tree — while the leaffix dispatch had none. The author's verdict:
*"either they all take a seed, or only those that are strictly necessary take seed — I
don't want to live in a world where one method that doesn't require a seed has a seed,
and one method that doesn't require a seed doesn't get a seed."* "All" is not honestly
available (a leaffix-dispatch seed can only mean broadcast, the deleted alias), so the
rule is:

> **The SEED is the virtual forest root's arrival, and it exists exactly where that
> object faces the boundary: Rootfix methods speak the virtual root — seed and selector
> flavors. Leaffix methods speak the fringe per-leaf — selector flavors only, both
> tiers.**

The predicate is the existence of an OBJECT, not the shape of a callback seat. The
virtual forest root is real in this library — singular, tree-lawful, load-bearing
(`NodePosition.ForestRoot`, the conformance-checked pre-enumeration convention). Its
would-be dual is not: a SINGULAR virtual node below all leaves would have n parents —
that is no tree (it is a DAG object; compare Copse.Dags, whose MVP likewise ruled no
sinkfix seeds — the families align on source-side-only seeds). The only tree-lawful
reading of the fringe is plural, one virtual child per leaf, and a plural boundary's
instrument is a per-leaf RULE — which is the selector. The redundancy inventory that
forced the choice: the rootfix DISPATCH seed is irreplaceable (the survey over the
virtual family is group-shaped; no per-root selector encodes it), the two fold-tier
seeds were both pure form (each expressible as the translated selector), and the
leaffix dispatch seed was impossible. The seed is kept as a CLASS at the rootfix
boundary — the object is load-bearing there, and `(seed, fold)` is the fold tradition's
canonical grammar — and retired entirely at the leaffix boundary, where no object ever
backed it. Formula-shaped fringes are written `leaf => nodeAcc(x, leaf)`.

Consequences: the north star's quantifier became exact (no translated equivalence in the
battery); LeaffixScan's seed machinery died with the flavor (`SeededDualFoldSurvey`, the
internal `FullSurvey` no-leaf-branch path — every remaining LeaffixScan flavor routes
through public LeaffixDispatch, so `CrossTierCoherenceTests` no longer pins an
internally-unreachable path); LeaffixAggregate's seed flavors (already implemented AS
the translation) went with it. The dual reshape's content survives untouched — the
edge/node decomposition, the node accumulator being literally RootfixScan's fold shape.
What's demoted is the boundary-flavor symmetry claim: the FOLD dualizes; the boundary
does not, because the multiplicity flip is precisely what trees cannot dualize — the
same fact, applied consistently, that deleted the dispatch seed.

**THE LEAFFIX DUAL (2026-08-05, the same day's second act — "the mechanism is not the
dual of RootfixScan")**: the old LeaffixScan folded the boundary INTO its map ("both an
accumulator and a generator"), which is why it had no boundary flavors and read as
non-dual. The true dual decomposes on upstream multiplicity — one parent down, n children
up — so the reshape is:

> `LeaffixScan(leafSelector | positional, edgeAccumulator, nodeAccumulator)` —
> `value(n) = nodeAccumulator(edgeReduce(children), n)`; at the fringe the selector
> answers directly. (As reshaped the boundary also took a seed —
> `value(leaf) = nodeAccumulator(seed, leaf)`, character-for-character the dual of
> `fold(seed, root)` — retired 2026-08-06 by THE VIRTUAL-ROOT RULE.)

The `nodeAccumulator` is LITERALLY RootfixScan's fold shape `(TAcc, TSource)`; the
`edgeAccumulator` reduces siblings left-to-right from the first child (k−1 firings, no
identity demanded). The reshape also returned the seed to the leaffix scan — the node
accumulator's state seat at every leaf created the participation channel the corollary's
rule required — but the channel proved to be form without power (the seed flavor was
extensionally the translated selector), and THE VIRTUAL-ROOT RULE retired it the next
day: a channel is not a mandate, and no singular virtual object backs a leaffix seed.
Per-edge node context flavors on `edgeAccumulator` wait for a workload.
`LeaffixAggregate` was re-derived on the same shape, value-flavored — retiring the
family's last NodeContext callbacks and the long-deferred signature workstream. The
ternary map-flavor died with the map (its per-edge node context returns as an
`edgeAccumulator` arity-split if a workload shows).

**This REVERSED the arrival-semantics decision of 2026-08-04** (selector-as-arrival,
fold-fires-everywhere): that fix optimized the lesser, intra-tier equivalence
(seed ≡ constant selector) at the cost of the cross-tier one, and its real motivation —
the merged RootfixDoScan's silent root landing — had already died with the quartet. The
one-day detour is preserved in history; the north star is the standing law.

## The callback grammar (articulated 2026-08-04; NodeContext retired from the surface 2026-08-05)

Every consumer-facing callback in the library follows one grammar:

- **Value flavor primary, positional arity-split**: a callback receives the NODE; rules
  that read coordinates take the `(node, position)` overload (positions are
  machinery-owned and underivable — justified seats, rationed by arity). Forced on the
  library by Select/Where composition; swept family-wide 2026-08-05 (AnyNodes, AllNodes,
  CountNodes, TakeNodesUntil/While, GetTraversals' strategies selector, OrderChildrenBy's
  key selector — the aggregation family was already there).
- **The Aggregate pair travels intact**: wherever a fold happens, `(state, increment)`
  order is preserved (rootfix fold `(acc, node)`; edge accumulator `(left, right)`).
- **Applicators are target-first**: a callback whose job is operating on a node leads
  with it (the old store's `(node, value)`; the leaffix survey's subject).
- **Context prefixes**: contextual extras go in front of an intact pair, never inside it.

`NodeContext<T>` consequently appears in NO public callback signature. Its lawful homes:
the view element types (`DispatchTarget`/`DispatchSource.Context` — immediate, consumed
in place, cannot go stale, per the position ruling) and internal machinery (the
SelectWhere composition currency; the engine's child-enumerator protocol; capture/fold
plumbing; the test scenario corpus, adapted at single call sites).

## The readiness clause (ratified 2026-08-05 — survey order)

A survey fires when its data is ready: after the arrival lands (rootfix — parents before
children), after the children complete (leaffix — children before parents). The PARTIAL
order is the operator's meaning and is guaranteed, as is sibling order within every view;
the TOTAL cross-node sequence is deliberately UNSPECIFIED — a pure callback cannot
observe it, its only beneficiaries would be off-contract impure surveys, and pinning it
would foreclose parallel builds forever. (Field origin: a candidate collapsed leaffix build would have changed
reverse-preorder to postorder and the author noticed he had assumed an order; the clause
converts that from a latent trap into stated law. The collapsed build itself was then
MEASURED OUT -- one walk lost to three sequential array passes on every cell, time and
memory both, because fine-grained walk bookkeeping outweighs re-iterating flat arrays --
so the clause outlived its trigger: survey order stays unspecified, the pass structure is
settled by evidence, and the remaining chain-time delta versus the pre-ScanResult build
is the PRICED cost of the features: pairing results, child identity, O(1) views.)

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
  EMPTY, not skipped (`sources.Count == 0` is the in-band leaf test). The selector
  flavors are the public face (the survey-only overload was deleted 2026-08-05 as
  fixer-less; the seed flavor as a misnamed bypass); full participation persists in the
  internal pass.
- **Scans — the ARRIVAL SEMANTICS detour (2026-08-04, REVERSED 2026-08-05 by THE NORTH
  STAR)**: the "1(2,3,4)" verdict briefly made the rootfix selector supply the root's
  arrival with the fold firing at every node, chasing the intra-tier seed ≡
  constant-selector equivalence. The north star showed the cross-tier equivalence is the
  one consumers reason by, and it requires the ORIGINAL semantics: the selector sets the
  root's accumulation directly, the bypass instrument, mirroring the dispatch selector.
  (The detour's real motivation — the merged DoScan's silent root landing — had already
  died with the quartet.) The rootfix seed flavor folds at every node, unchanged
  throughout (the leaffix seed flavors were later retired outright — THE VIRTUAL-ROOT
  RULE, 2026-08-06). The leaffix map was always per-node — no change.
- ~~**HELD OPEN (tier 3)**: the re-opened dispatch `store` seats~~ — RESOLVED BY
  DELETION (see THE DEMOTION): the Do dispatches no longer exist, so there is no store to
  merge. The pure surveys' seats are settled (rootfix subject-less; leaffix
  subject-bearing).

## The recording rule (the alpha.9 edge-1 clause; TYPE-LEVEL since 2026-08-06)

Folds record their OUTPUT (the node's accumulation). The rootfix survey records its
INPUT — the arrival — because it is the family's one 1-in-n-out shape: no node-grained
output exists, and its n outputs are recorded as its children's arrivals. Forced, not
accidental.

**Made TYPE-LEVEL 2026-08-06** (the logged item-2 amendment, following the dag family's
`DagScanResult`/`DagDispatchResult` split — "the two tiers never overload one field with
two meanings"): the two recordings no longer share a field. Output-recorders — the
scans, the aggregates, and LeaffixDispatch — return
`NodeAccumulation<TSource, TAccumulate>` (`.Accumulate`); the family's ONE
input-recorder, RootfixDispatch, returns `NodeArrival<TSource, TDispatch>` (`.Arrival`).
The old single type's dual reading was only "forced" while one type served both tiers —
its signature already confessed (`RootfixDispatch<TSource, TDispatch>` returning a field
named `Accumulate`). Named by PAYLOAD, not operator (the house pairing grammar:
NodeContext, NodeVisit, NodePosition), because tree-side the operator axis lies:
LeaffixDispatch is a dispatch that records accumulations (n-in-1-out has a node-grained
output) — where the dag family's operator↔payload mapping is exact, so its
operator-named pairings are honest there. The tree arrival is SINGULAR (one parent)
where the dag's is a group (`DagArrivals`, n parents): the field shape itself records
the structural difference between the families. `ScanResult` was the shared type's name
from ratification (2026-08-02) to the split; this document keeps its historical
filename.

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
| roots take the seed (no parent above — but the VIRTUAL forest root stands in, one tree-lawful node) | leaves take NO seed (a singular virtual node below all leaves would need n parents — no tree has one) | forced-different — THE VIRTUAL-ROOT RULE (2026-08-06); was "matched — the boundary pair" while the leaffix scan seed lived |
| `DispatchTarget`: context + write facility | `DispatchSource`: context + read value | matched (this sweep) |
| O(1) Count + indexer via the child-index | O(1) Count + indexer via the child-index | matched (this sweep — the leaffix build restructured to capture-then-fold, sharing the rootfix passes) |
| `DispatchTargets` | `DispatchSources` | matched (this sweep) |
| pure result decorates (`NodeAccumulation`; `NodeArrival` for the dispatch tier) | pure result decorates (`NodeAccumulation`, both tiers) | matched on decoration (this sweep — leaffix previously REPLACED); the pairing TYPE follows the recording rule (2026-08-06) |
| survey records the ARRIVAL (its input; no node-grained output exists) → `NodeArrival` | survey records its OUTPUT (n-in-1-out has one) → `NodeAccumulation` | forced-different — the recording rule (2026-08-04; type-level 2026-08-06) |
| callbacks: minimal basis — subject + flow state, pairing in results only | callbacks: minimal basis — subject + flow state, pairing in results only | matched (the seat rule, 2026-08-04) |
| Do store: (node, arrival) | Do store: (node, rollup) | matched — born dual (the dispatch tier) |
| fold tier: landing rides the fold's return (RootfixDoScan MERGED — one callback per node produces that node's value) | fold tier: combine is child-edge-grained (0× on leaves, k× else) — `store` keeps its seat | forced-different — the landing rule (2026-08-04) |
| boundary = an INVOCATION of the same survey: the virtual root's family goes first, (seed, roots) | boundary = an invocation of the same survey with empty sources (the fringe is every family's base case) | matched — full participation, unified: boundaries are invocations, not callbacks; flavors are sugar |
| survey `(arrival, targets)` — subject DERIVABLE (the arrival is authored with the node in hand at the dispatch site) | survey `(subject, sources)` — subject UNDERIVABLE (upward flow: the node's value passes through nobody else's hands) | forced-different — each survey keeps exactly the seats its flow direction cannot derive (2026-08-04) |
| seed = the virtual root's arrival, folded at every node; selector = each root's ACCUMULATION set directly (fold bypassed at roots) | seed = the virtual root's arrival, surveyed at the virtual family; selector = each root's ARRIVAL set directly (survey bypassed) | matched — THE NORTH STAR (2026-08-05): the two instruments, uniform across tiers; seed ≠ constant selector on both, pinned deliberately-different on both; Scan(boundary, fold) ≡ fold-encoded Dispatch(boundary) for every flavor, the quantifier EXACT since THE VIRTUAL-ROOT RULE confined seeds to this rootfix column (CrossTierCoherenceTests) |
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

## Measured build constraints (recorded 2026-08-19; measurements as dated)

The build passes carry guardrails whose numbers belong in a record rather than in the
comments that enforce them. Each is stated in the code as a constraint; the evidence is here.

- **Preorder layout for BOTH dimensions** (2026-08-17). Pinning a level-order layout on a
  breadth-first-first pull was built and measured out: over raw array stores the
  breadth-first cross-decode tax is only ~1.08x, so the transpose plus transient double
  storage needs ~5 replays to break even, and it taxes the common single-drain case ~8%.

- **The pristine-loop rule** (profiled 2026-08-17). An in-loop erased-writer call was tried
  in the dispatch fold for composed products and pessimized the whole loop on net8: the
  virtual call resisted devirtualization and additionally taxed the survey lambda around it,
  +22% build time. A separate direct-array pass over the hot outputs costs ~1ms/million nodes
  (the pair zip in the finisher is the standing proof). Anything derived from the outputs
  therefore runs as its own pass.

- **Reverse-preorder over a forward close-stack walk** (2026-08-05). A forward close-stack
  walk deriving positions was built and measured out: it holds O(depth) entries, which is
  O(n) on chains, where the backward walk completes every child before its parent with zero
  walk state.

- **Probes at birth** (2026-08-15, applied to the dispatch tier 2026-08-17). The result
  buffer's adjacency rides the same lazy store its visit stream builds. The former Tree.Lazy
  wrapping hid the store behind the composite, and every receiver-smart consumer paid a full
  second capture.
