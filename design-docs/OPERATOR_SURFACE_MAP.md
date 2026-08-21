# Operator Surface & Flat-Family Map

> **Status: LIVING INVENTORY** (established 2026-07-13 from a full survey; last verified
> 2026-08-15, the pre-merge hygiene pass). Companion to
> [LAZINESS_AND_BUFFERING_POLICY.md](LAZINESS_AND_BUFFERING_POLICY.md) — this is the
> reality that policy is checked against: what every operator returns, what it buffers and
> when, how the flat store family hangs together, and where ad-hoc store construction is
> duplicated (the cohesion-pass work list).
>
> **Maintenance convention:** any change that adds, removes, or re-shapes an operator,
> store, stream, decoder, or wrapper updates its row/entry here in the same commit — the
> same discipline the benchmark families follow. Sync/async twins share one row (the async
> source is the edit surface; the `.g.cs` is generated). When a policy-audit flag below is
> fixed, delete the flag and update the operator's row. Bump "last verified" whenever a
> full re-check happens; keep entries free of line numbers (files and shapes, not lines).

## 0. Naming grammar (ratified 2026-08-14)

The surface's prefixes are a grammar, not a habit. Five families, two minor conventions,
and the rulings below — new surface must name itself by these:

| Prefix | Meaning | The test |
|---|---|---|
| *(none)* | **The algebra** — transformations and operators, LINQ's verb/noun tradition (`Select`…`Extend`, `Subtrees`, `SpanningSubtree`, `Materialize`) | returns an algebra citizen; partiality is typed in the result (`Try` never joins an algebra name — the BCL has no `TrySelect`) |
| `Get` | **Acquisition or read** — the `GetEnumerator` sense: reads (`GetValue`, `GetLeaves`, `GetHandles`) AND minting doors (`GetDepthFirstTreenumerator`, `GetTreeWalker`, `GetTreeWalkerAt`) | bare `Get` is total or THROWING — if it can fail, failure is a violation (malformed question, exception channel), never a typed miss. The contract's door joined this family when the sentinel completion made it total (2026-08-20: the unfocused stance answers, so the door's Try exited per this very grammar) |
| `TryGet` | **Acquisition whose miss is typed** (`TryGetParent`, `TryGetChildAt`, `TryGetRootAt`, `TryGetTreeWalkerAtRootIndex`, the walker's `TryGetValue`) | the miss is an expected "no" carried in an `Option` — the async spelling of the BCL try-pattern (`out` cannot cross an `await`); `TryGet` ⇔ result-typed miss is the TWO-CHANNEL DOCTRINE in the name |
| `Take`/`Skip` | **Selection reshapings** — LINQ heritage (`TakeTrees`, `TakeNodesUntil`) | positional/conditional selection; same kind in and out |
| `To` | **Representation conversion, eager** (`ToFormattedLines`, `ToDegenerateTree`) | name, return shape, and cost agree — the honest-eager rule |
| `Move` | **Walker steps** — stance verbs, result-typed (`MoveToParent`, `MoveToChild`) | partial but unmarked, on `IEnumerator.MoveNext`'s precedent (the BCL's original unmarked-partial movement verb; ruled pragmatic 2026-08-14 — a full Try-everything pass was considered and deferred) |

Suffix `At` = indexed/addressed access (`GetTreeWalkerAt` by handle, `TryGetChildAt` by
index). Machinery/type naming is the [Mechanism]+[Axis]+[Tier] grammar
(LAZINESS_AND_BUFFERING_POLICY.md).

**The search law** (`Find` deliberately absent — retired 2026-08-14 the day it was
introduced): *searches are not surface.* An extension earns a place only if it needs
information per-element LINQ cannot reach (child-lookahead, depth, traversal semantics,
receiver-recovery); a predicate search is consumer LINQ over `GetHandlesWithValues` (the
one receiver-recovery exception), and its honest miss is the **empty sequence** — flow it
into a result-typed consumer to keep the miss typed. Never `FirstOrDefault` over ordinal
handles: handle 0 is the root, and the miss masquerades as it.

**The typed-miss rule**: *an operation whose miss is an expected answer returns
`Option<TValue>`* (Copse.Vocabulary) — a flag beside a named payload, BY VALUE, never
`out`: the shape stores nothing (no frame bloat) and is legal in an async method, which is
why one hand-written type serves both colors with no generated twin. The exception channel
stays reserved for malformed questions. This SUPERSEDES the 2026-08-16 bespoke-carrier
rule that stood here (`ParentResult`/`ChildResult`/`TreeWalkerResult`, one named type per
miss, a generic `Option<T>` declined): the refused alternative was then built and
measured — the type swap is free, and one vocabulary type replaced three sources, three
generated twins, and the buffer's two internal tuple doors. OPTION_DESIGN.md holds the
criterion, the closed inventory, and the measurements. (The unrelated `Option`-labelled
sentinel completion of the carrier is a separate question, canonized as semantics and
refused as representation in CATEGORY_THEORY_SURVEY.md §11.) Two clauses govern what is
option-shaped:

- **The flag must govern the WHOLE carrier** — `false` means the caller gets nothing.
  Where the flag governs a COMPONENT of a compound answer, the thing is a variant, not an
  option (`MergeNode` stays bespoke for exactly this).
- **The payload must already be one named thing** — otherwise the option form mints a type
  to wrap, one type traded for another plus a hop at every read (`PreorderRead` converted
  by DEMOTING it to the payload).

**One extension class per color** (`Treenumerable` / `AsyncTreenumerable`, the
`Enumerable`-idiom name — the folder wears the `*Extensions` suffix, the class does not):
both receiver tiers, one static class, so overload resolution across tiers is ONE
candidate set and betterness always picks the specific receiver — closed under
refactoring, the silent-fallback hazard structurally impossible.

## 1. Operator surface — what returns what, what buffers when

Dims key: **F** = `ITreenumerable`, **D** = `IDepthFirstTreenumerable`, **B** =
`IBreadthFirstTreenumerable`. Behavior key:

- **streams** — wrapper treenumerator, bounded state (bound noted).
- **capture(deferred-once)** — full O(n) flat capture, construction pinned to first
  acquisition (`Tree.Lazy`), built on first pull, shared by all replays and both dimensions.
- **capture(eager)** — full O(n) at call time.
- **drains** — terminal; consumes the source to produce an enumerable/scalar.

### Tree-returning operators (Copse.Linq)

| Operator | Source dims | Returns | Behavior | State bound |
|---|---|---|---|---|
| Select | F, D, B | same-dim | streams | O(1); lambdas take (node) or (node, position) — NodeContext left the operator surface 2026-07-16 (composition design); consecutive Selects compose, either flavor (projection never moves positions); **BUFFER RECEIVER (THE THIN SHAPE 2026-08-17, SELECT_INTO_CAPTURES_DESIGN.md 4a)**: value-flavor Select over an `ITreenumerableBuffer` returns the projected buffer citizen — source buffer + selector, built as ONE counted array map off the source's completed store (`TryGetPreorderStore` door; veneer-capture fallback for foreign buffers); `ComposeSelect` composes the selector so chains stay one map; measured composed ≈ plain (the map is repaid by the narrow decode); the positional buffer overload re-captures |
| Where / PruneBefore / PruneAfter | F, D, B | same-dim | streams | O(depth) DFT / O(width) BFT; Where lambdas take (node) or (node, position) — value-only Wheres COMPOSE (predicate combination) and compose over Selects into the projection-carrying driver; positional Wheres never compose with their own kind (each layer sees its input tree's labels — LINQ's indexed-Where rule); prunes take (node) or (node, position) too. **THE SEAL OPENED 2026-08-18 — COMPOSITION IS TOTAL** (OPERATOR_COMPOSITION_DESIGN.md 2.10; sealed 2026-08-04 by 2.9's Mixed regression, +20–25%): the struct-composed arrow (`ComposedResultSelector` — chains nest in the TYPE, every splice leg inlinable) removed the all-delegate chain that forced the seal, so rejecting operators now SPLICE over light wrappers through the inherited struct `Compose` — any number of Select/Where/PruneAfter/PruneBefore, any order, ONE driver. Ruled at ~+6% on short DFT mixes (Bft parity; the tax inverts on longer chains — FiveOperators collapse 2.36×/15.4×); prune-after ARRIVING on a general chain still stacks its light layer on top (the surviving half: joining would demote its representation for a near-free layer); CI Mixed series = the watch rows |
| **SelectMany** (pointed bind) | F(→D streams; B = documented capture), D | same-dim (D); composite from composite | **streams (DFT); capture per BFT acquisition** | **SHIPPED 2026-08-21 — the tree monad's bind, SELECTMANY_DESIGN.md Addenda II–IV**: root-graft substitution over POINTED expansions — `Expansion<TResult>` = a forest + a `SlotPlacement` for where the node's rewritten children re-hang; the four special values are the library's own reshapings as theorems (`Return` ≡ Select's unit, `Promote` ≡ Where's drop arm, `Drop` ≡ PruneBefore, `Leaf` ≡ PruneAfter — all four pinned byte-for-byte), `Expansion.Of(forest, placement)` the general form. v1 placements = the lookahead-free set over a visit stream (`AfterRoots`, `UnderLastRoot`, `None`); slot-before-roots / under-first-root DEFERRED (they owe emission after the source subtree ends, which a visit stream cannot announce without one event of source lookahead — a declared phase lag if ever shipped). DFT machine = a frame per open source node + its paused expansion treenumerator (the lab's bracket stack one carrier up); contract pinned: nothing pulled ahead of its emission, a dropped subtree never pulled; one-event phase lag on forest ROOT visits (a visit stream never marks a root's last visit). Consumer strategies v1: forwarded to the expansion cursor that scheduled the node (`SkipDescendants` on a slot-bearing root skips the splice too); conformance pinned for TraverseAll, strategy matrix deferred. Perf (SelectMany benchmark class, same-run): 2.3x Select / 1.7x Where / 1.4x PruneBefore on Mega Triangle DFT after the structural Return/Leaf fast path and the struct-frame distill (frames + slots as two RefSemiDeque stacks; theorem rows 220 KB total, zero Gen0 -- the allocation floor; what remains is the general machine's per-event work). Oracle = `PointedBindReferenceModel` (the phantom-slot model), `SelectManyOperatorTests` lockstepping both dimensions |
| TakeNodesUntil / TakeNodesWhile | F, D, B | same-dim | streams | O(1); **flavor sweep 2026-08-05**: predicates arity-split value \| positional — NodeContext callbacks retired from the public surface family-wide |
| TakeTrees / SkipTrees | F, D, B | same-dim | streams | sugar over take/prune |
| TakeLastTrees / SkipLastTrees | F, D, B | same-dim | **eager count at call time** | two-pass by design (count the roots, then take/skip; decided 2026-07-13 — a single-pass form must buffer k whole subtrees); B's counting pass drains level 0 only |
| TakeSubtreesWhere | F, D, B (ALL stream) | same-dim | streams | **THE SCAN SPELLING (2026-08-17, the layering north star's first landing — SELECT_INTO_CAPTURES_DESIGN.md §5)**: the operator IS `RootfixScan(false, (kept, n) => kept \|\| predicate(n)).Where(pair => pair.Accumulate).Select(pair => pair.Node)` — "keep this node" is a rootfix fold fact, the outermost rule falls out of the disjunction's short-circuit. **DIMENSION-DISPATCHED same day (the honest-streaming-baseline rule, Jason's correction: memory dropping when un-buffering is table stakes, so time answers to the best STREAMING implementation, not the buffer)**: the composite's DFT arm constructs the bespoke O(1)-state pass-through wrapper (the chain measured ~2.3x it for the same work; composite DFT now BEATS the retired buffer — Triangle 101→64ms, Chain 71→26ms), its BFT arm constructs the Where machinery in SUBTREE MODE (the subtree stage, `63b3f84`: kept-region membership read off the skip prefix the machinery already carries — one wrapper, no scan engine, no pair; the scan chain remains the algebraic definition and the product variant's route); **the dispatch lives BEHIND the citizenship** (Jason's seam objection, same day: a bare dispatcher would be a composition seam mid-chain) — the result is a streaming citizen (`TakeSubtreesWhereTreenumerable` + product variant, the rootfix-citizen pattern): a following Select composes onto the product selector (BFT: absorbed inside the chain's driver; DFT: ONE light wrapper over the bespoke wrapper however long the chain), a following Where joins the one driver over the citizen — pinned by the mid-chain seam pin in SelectComposableLawTests. The former buffer arms are RETIRED — their "result's BFT cannot stream" rationale was DISPROVEN: general Where's breadth-first wrapper produces the re-rooted forest's true level order by pulling its inner ahead through its queue (verified on the reorder-wall shape: deep match preceding a shallow match in preorder — pinned in TakeSubtreesWhereTests). BREAKING (pre-beta): composite + B-narrow returned buffers through 2026-08-17; streaming semantics now (predicate per drain, Materialize = the consumer's pin). Cost at the swap: alloc collapses where the result store dominated (Triangle ~41MB→~0.2-0.5MB, Bft_Chain 40→6MB); composite DFT faster than the buffer (dispatch); composite BFT at or below the retired buffer's time since the subtree stage (the ~1.5x scan-chain interim is history). D-narrow arm = the same bespoke wrapper. Semantics unchanged: <b>(2026-08-06 ruling)</b> matched subtrees re-rooted, match depth compresses to 0, descendants keep sibling indices, roots take source preorder match order, OUTERMOST MATCH WINS; predicate arity-split value \| positional (source labels; the positional flavor is the same citizen reading position off its context predicate); the dag analog `TakeSubgraphsWhere` on experimental/dag gets outermost EMERGENTLY from induced in-degree, and is the general form |
| Union / Intersection / Subtract / SymmetricDifference | F×F, D×D, B×B | merge/narrow | streams | lockstep co-traversal, O(depth) DFT / O(width) BFT |
| Do / Hide | F, D, B | same-dim | streams | O(1); Hide takes an optional **HideScope** (2026-08-19, Copse.Linq.Traversal -- ordered scope, NOT flags): `Treenumerable` claims no capability beyond the plain contract and forwards acquisition, so the barrier costs nothing per pull; `Treenumerator` also wraps the machine (a real per-MoveNext layer) and is the no-arg overload's historical default -- nothing in Copse sniffs a treenumerator, so that scope defends only against foreign code; Do = the sanctioned effect point: action per emitted visit, receives the full NodeVisit (deliberately permissive — narrower cadences are caller-side filters); keeps NodeVisit through the signature migration; NEVER composes and prevents composition across it by definition (composition design: the window materializes the pane); **THE LANDING IDIOM (the Do-quartet demotion, 2026-08-04 — design-docs/SCANRESULT_DESIGN.md THE DEMOTION)**: mutable-node aggregation = pure op . Do(scheduling-mode-filtered landing) . Select(.Node) — effects per drain (the re-enumeration contract), Materialize/Memoize is the consumer's pin; the quartet (Rootfix|Leaffix)Do(Scan|Dispatch) is DELETED (derivable sugar + operator-pinned effects); admission bar for any Do variant: a workload the mode filter cannot serve; pinned by DoLandingCompositionTests |
| RootfixScan (seed / rootNodeSelector) | F, D, B | same-dim | streams | O(depth) DFT / O(width) BFT; seed/selector precedes the accumulator (type-fixer-first, 2026-08-02); **ScanResult sweep 2026-08-02**: returns the canonical pairing (`ITreenumerable<NodeAccumulation<TNode,TAcc>>` since the 2026-08-06 type-level recording rule; born `ScanResult` — project `.Accumulate` for values), `NodeContext<TAccumulate>` retired, selector arity-split value \| positional (the sweep instantiated the engines with TAccumulate = the pairing — superseded, see the emission mint below); **SEAT RULE 2026-08-04** (the alpha.9 verdict): accumulator is `(TAcc, TNode)` — Aggregate's shape, the minimal basis, the shape every effect callback shares via the landing idiom (ancestry rides the state — a parameter earns a seat iff the caller cannot derive it; a parent-centric rule is a survey)); **THE NORTH STAR 2026-08-05** (cross-tier flavor coherence; reversed the one-day arrival-semantics detour): seed = the virtual root's arrival, folded at EVERY node (accumulator(seed, root)); rootNodeSelector = each root's ACCUMULATION set directly, fold bypassed at roots (the bypass instrument, mirroring the dispatch selector) — seed ≠ constant selector, pinned deliberately-different on BOTH tiers; Scan(boundary, fold) ≡ fold-encoded Dispatch(boundary) for every flavor (CrossTierCoherenceTests). **STREAMING PROJECTION CITIZEN (SELECT_INTO_CAPTURES_DESIGN.md)**: the composite result implements the public citizenship — a composed Select re-plants the projection inside the product engine twins (one selector call at emission, no wrapper layer; measured −7%/−14% on the witness rows, residual +2-4% over the bare scan); plain acquisitions construct the plain engines, selector-free (the plain spelling never pays); narrow results are NOT citizens (composite-width contract, narrowing deferred); **THE FOURTH CELL 2026-08-18 (the ancestor composer, SCAN_TIER_DESIGN.md — Jason's 2×2 taxonomy: input scope × label effect)**: the scan citizens also join the general-splice surface, so a REJECTING operator lands in the fold-carrying driver (ScanWhereTreenumerable — DFT: Where machinery + the depth-indexed accumulate trail; BFT: Where machinery + the accumulate tracker, the rootfix engine's state minus emission, rejections bridged to its skipped stack) — Scan().Where(pair => …).Select(…) is ONE machine, both dimensions, every downstream leg composing into it (witnesses: Compose.ScanWhere pair, composed −18% under the two-machine spelling on DFT, BFT parity w/ the engine layer's alloc gone); bare Select still takes ComposeSelect (the product engine; the probe-order clause is history — since the reversed hierarchy, PUBLIC_COMPOSITION_SURFACE_DESIGN.md, there is ONE ComposeSelect slot per member and the citizen's slot IS the re-plant); **THE ROOTFIX DOOR 2026-08-18** (compose-left, the leaffix door's streaming mirror): Select(f).RootfixScan(...) surrenders through IAsyncProjectionSource and the citizen runs over the un-projected inner RAW — the projection folds into the accumulator and rides the now context-shaped product selector at emission, zero wrapper layers on any pull (FromSelect witnesses: +20-30% wrapper cost → ~+6-13% projector residual), and the door's result is still the citizen so the left-composed chain stays total (Select→Scan→Where→Select = ONE ScanWhereTreenumerable, pinned); **THE EMISSION MINT 2026-08-17** (Jason's `int → (int,int) → int` observation): the engines' stacks/level-buffers carry BARE accumulates — the fold's own width, the O(depth)/O(width) information floor — and the pairing (or the composed product) is constructed per emission from the inner's node-in-hand, never stored; reclaimed the ScanResult sweep's hidden state cost, measured 16 bytes/node of chain depth: Dft_Chain 104.02→88.02 MB, the exact pre-ScanResult level, BOTH spellings, time parity |
| Invert | **B-narrow** | IBreadthFirstTreenumerable | **streams** | O(width) — the one genuinely streaming mirror (`InvertedLevelOrderStream`) |
| Invert | D-narrow; buffer | ITreenumerableBuffer | capture(deferred-once) | mirrored preorder arrays. **RECEIVER-SMART 2026-08-14** (the experiment collapse): the acquisition seam sniffs — a preorder-affording capture hands over its skeleton (concrete buffer → raw store via TryGetPreorderStore; foreign walkable/memo → the walker-probe mirror, no second skeleton either way; measured 2.4–4.6x on buffer receivers); level-order captures and streams pay the one capture the mirror always owed. **Specialization KEPT (decided 2026-07-15)**: Invert ≡ OrderChildrenByDescending by source sibling index (pinned by OrderChildrenByTests' subsumption law), but the specialized build is measured ~1.15x faster, 2.4x leaner on wide trees (no keys channel, LIFO emit, no per-group sort), and its B arm streams O(width) with NO capture — a cost class the keyed general operator cannot reach. Both families share trees on the Buffer leg, so the premium stays continuously measured; reopen only if the rows converge. |
| Invert | F | ITreenumerableBuffer | capture(deferred-once) | dimension-dispatched: DFT-first → mirrored preorder arrays; BFT-first → the streaming mirror drained once into level-order arrays (2026-07-13; both arms now share the build-on-first-pull cost shape) |
| LeaffixScan (leafNodeSelector) | D; **B**; F(→D) | ITreenumerableBuffer\<NodeAccumulation\> | capture(deferred-once) | **THE DUAL RESHAPE 2026-08-05** (design-docs/SCANRESULT_DESIGN.md THE LEAFFIX DUAL): `(leafSelector \| positional, Func<TAcc,TAcc,TAcc> edgeAccumulator, Func<TAcc,TSource,TAcc> nodeAccumulator)` — value(n) = nodeAcc(edgeReduce(children), n); the nodeAccumulator IS RootfixScan's fold shape; the selector answers at the fringe (**seed flavors RETIRED 2026-08-06, THE VIRTUAL-ROOT RULE**: seeds belong to the virtual forest root, the family's one tree-lawful virtual node — the leaffix boundary is selector-only on both tiers; a formula fringe is `leaf => nodeAcc(x, leaf)`); the old map-then-combine (the map carried boundary and contribution at once) is DELETED, ternary context flavor awaits an edgeAccumulator workload; sugar over public LeaffixDispatch — the stream path delegates every flavor (CrossTierCoherenceTests pins the flavor pairs). **RECEIVER-SMART 2026-08-14, value-selector flavor** (the experiment collapse; re-keyed from the retired seed flavor at the 2026-08-16 reunification — the leaf slot is position-free either way): a capture folds IN PLACE — concrete buffer → the span fold over its raw store (reverse-ordinal, span hops, no child-index/positions builds; measured 3.3–4.1x on buffer receivers), memo → the walker-probe fold (completes it exactly once, no second skeleton); streams take the dispatch delegation; the in-place result buffer carries probes at birth (ReceiverSmartOperatorTests pins every receiver shape against the engine oracle). The positional flavor stays dispatch-only (it needs positions). **THE THIN SHAPE 2026-08-17 (SELECT_INTO_CAPTURES_DESIGN.md 4a; retired the one-week shared-fold-pass citizen, measured out)**: scan results are PLAIN buffers again — buffer-tier projection citizenship lives at the Select seam (the projected buffer's one counted map off the scan's completed store), and the span fast path serves scan-of-scan again (the Twice_Dft_Chain heal, 232ms/272MB → ~101ms/108MB). **THE PAIR-PRODUCT PRICE (measured 2026-08-16, ACCEPTED by ruling — the richer contract justifies itself)**: the ScanResult re-founding (28a9811, 2026-08-05) made the scan product `(Node, Accumulate)` pairs, roughly doubling build+replay width — +13% Dft / +20% Bft on the Chain stream rows, same-CPU 7763 series; the Perf 1/4–4/4 pass recovered ~1–3%, the rest is the contract's standing cost (the pair is load-bearing for dispatch correlation and the consumer's node-in-hand) |
| LeaffixDispatch | D; **B**; F(→D) | ITreenumerableBuffer | capture(deferred-once) | **SIBLING-COMPLETE TIER, and the true upward dual of RootfixScan** (survey = once per node receiving all n arrivals; leaf boundary = SELECTOR flavors only — the broadcast seed overload was DELETED 2026-08-05 under THE NORTH STAR: a seed participates through the tier's callback and upward flow has no channel for one, so the old seed flavor was the bypass instrument misnamed (`_ => x`); canonical leaf count = `_ => 1`): survey sees all children at once via the no-copy `DispatchSources` view (subtree-span hops; deliberately NOT IEnumerable — interface paths would box per survey); owns the one buffer-producing leaffix build (LeaffixScan delegates in; the build is the shared fold pass `RunLeaffixDispatchPassAsync`, 2026-08-02 — the Do twin rides it, its result reusing the capture's arrays); selector/seed precedes the survey (type-fixer-first, 2026-08-02); **ScanResult sweep 2026-08-02**: value-flavored survey over `DispatchSources` (DispatchTarget's READ dual — context + accumulation per child, O(1) Count/indexer off the shared `DispatchChildIndex`), boundary arity-split seed \| value \| positional selector, returns pairing buffer; build RESTRUCTURED to capture + child-index + reverse-preorder fold — the same passes as the rootfix build, genuinely shared (perf re-baseline pending on the dashboard); **FULL PARTICIPATION 2026-08-04** (boundary-shape-follows-tier-shape): the survey fires on EVERY node — a leaf's sources view is EMPTY, not skipped (`sources.Count == 0` is the in-band leaf test); leafNodeSelector flavors are the public face (the survey-only overload DELETED 2026-08-05 — fixer-less, inference structurally impossible, type-fixer-first enforced by the compiler; sibling-comparative workloads need a leaf rule anyway, formula-shaped fringes belong to LeaffixScan); full participation persists internally, and LeaffixScan's seed flavor rides the internal no-leaf-branch path (the scan IS the fold-encoded dispatch — CrossTierCoherenceTests); **PROBES-AT-BIRTH 2026-08-17**: the result buffer's adjacency rides the same lazy store its visit stream builds (the former Tree.Lazy wrapping hid the store, and receiver-smart consumers — a second scan, the projected buffer's map — paid a full second capture through `EnsureTopology`) |
| RootfixDispatch (seed \| rootNodeSelector) | D; **B**; F(→D) | ITreenumerableBuffer\<NodeArrival\> | capture(deferred-once) | **SIBLING-COMPLETE TIER of the rootfix pair** (added 2026-08-01; fold tier = RootfixScan, which streams; rootNodeSelector overload 2026-08-02 completes the boundary-pair grid — RootfixScan, RootfixDispatch, and LeaffixDispatch all offer selector \| seed at their arriving boundary): survey sees arrival + ALL children as exactly-once write-handles via the no-copy `DispatchTargets` view (one whole-build written-flags array; double/missed Dispatch throws); result DECORATES (`DispatchNode` = value + arrival), flavors are compositions (Select/Do); two-pass build (structure DFS, then top-down surveys in preorder); B overload Materializes first; seed-before-survey order is the shape the 2026-08-02 type-fixer-first unification adopted family-wide; VALUE-flavored 2026-08-02 (feature/do-scan — the surface the Do tier inherits): survey gets the parent's value, rootNodeSelector arity-split value \| (node, position); `DispatchTargets` grew an honest O(1) Count + indexer (2026-08-02 iteration 2: pass 1½ gathers a child-index — CSR over the preorder encoding, ~2n ints, two O(n) hop passes — after the O(k)-indexer shape was rejected as dishonest complexity); `ToArray()` is the explicit bridge to interface-shaped APIs (LINQ, IEnumerable params — any interface path costs one allocation per survey; ToArray makes it visible), foreach/indexer paths alloc-free; **ScanResult sweep 2026-08-02**: `DispatchNode` retired — returns the pairing buffer (`.Dispatched`→`.Accumulate` then); **recording rule 2026-08-04, TYPE-LEVEL 2026-08-06**: the survey tier records the ARRIVAL (its input — the family's one 1-in-n-out shape has no node-grained output; its outputs are the children's arrivals), folds record their output — now split into distinct pairings: this operator alone returns `ITreenumerableBuffer<NodeArrival<TSource,TDispatch>>` (`.Arrival`), everything else `NodeAccumulation` (the dag family's DagScanResult/DagDispatchResult principle, tree-side); **FULL PARTICIPATION 2026-08-04, UNIFIED same day** (the alpha.10 root-asymmetry verdict; the interim rootSurvey callback lived one tag — its duplication exposed the survey's SUBJECT as a derivable seat): survey is now SUBJECT-LESS `(TDispatch arrival, DispatchTargets)` — a node's arrival is authored at the dispatch site with the node in hand, so subject-shaped facts flow inside TDispatch (the seat rule; leaffix keeps its subject — upward flow, underivable); ONE dispatcher serves every family, the VIRTUAL FOREST ROOT's first (`(seed, roots)` — the boundary is an INVOCATION, not a callback), so roots participate with zero ceremony and budget-across-a-forest is the seed flavor verbatim; rootNodeSelector flavors survive as per-root-different sugar |
| OrderChildrenBy / …Descending (±comparer) | D; **B**; F(→D) | ITreenumerableBuffer | capture(deferred-once) | key selector once per node at capture, source context; stable per-group sort; D rides the keyed `PreorderCapture.CaptureFrom` → preorder layout; **B STREAMS (2026-07-15): one source walk, one buffered level (O(width) aux), level-order layout** — flag 4; **flavor sweep 2026-08-05**: keySelector arity-split value \| positional (positional = the SOURCE, pre-ordering position); GetTraversals' strategies selector likewise |
| Memoize | F, D, B | **IMemoizeTreenumerableBuffer (IDisposable)** | capture(lazy, incremental) | ONE capture (2026-07-15): the first pull pins the layout; off-pin replays ride it cross-order; **source enumerated at most once** — upstream side effects fire at most once per node; pays only for the region reached; idempotent on a live memo; **the only disposable return on the surface** |
| Materialize | F(±layout), D, B | ITreenumerableBuffer | **capture(deferred-once)** | **LAZY 2026-08-10** (was eager; async surface renamed `MaterializeAsync`→`Materialize` — no longer awaitable, suffix dropped): construction pinned to the first pull through the lazy store's grow seam (the LeaffixScan/Invert cost shape); the law — *construction uniformly lazy, the pin a commitment at the earliest free moment*. Organic: THE FIRST CONSUMER PINS THE LAYOUT (DFT-first → preorder, BFT-first → level-order; dimension-dispatched `Tree.Lazy`). Declared-layout overload (**parameter retyped `TreeTraversalStrategy`→`BufferLayout` same day** — the layout is the deliverable, so the parameter speaks STORAGE vocabulary per BufferLayout's naming rule; the old form opened by converting, the tell; `Consume(strategy)` correctly keeps traversal vocabulary because Consume walks): the layout GUARANTEE stands (2026-07-15, never ignored) and the pin lands AT THE CALL — a live memo's capture is created for the layout's native dimension with zero nodes pulled, so an intervening consumer cannot re-pin a shared memo; the O(n) waits for the first pull. Probes unchanged in spirit: a live memo gets the completion seam (`MaterializeTreenumerable` — completes IN BULK at the first pull, feed retires there; the result is non-disposable, the memo's disposal stays the caller's); a compliant buffer returns as-is; a mismatched/undecided one is TRANSPOSED from the buffer at the first pull. An unconsumed result holds exactly what the unconsumed pipeline held. The both-layouts recipe survives = materialize twice, one source pass. Benchmark rows force the build (Materialize rows one-pull in-method; Memoize/Serialization settle in setup). **WALKABLE 2026-08-13 (the buffer re-parent, design-docs/WALKABLE_CONTRACT_DESIGN.md); DOOR-ONLY since Stage C (design-docs/WALKER_FACTORY_DESIGN.md, 2026-08-15)**: the result is a walkable — `ITreenumerableBuffer : IWalkableTreenumerable<TValue,int>`, captures are never address-poor — whose door binds an ordinal-handled topology (the four adjacency probes are provider SPI now, `ITreeTopology`; consumers navigate through the walker). Steps are demand (a growing capture is forced exactly as far as the answer needs; steps upward never force; past a retired memo feed = ObjectDisposedException, the replay rule); handle spaces are PER-CAPTURE layout ordinals, never portable across captures/layouts. `MaterializeWalkable` ABSORBED (it was `Materialize(BufferLayout.Preorder)`; alias deleted — OPEN-3's collapse). Adjacency conformance: `BufferAdjacencyConformanceTests`, every producer × both handle spaces vs the visit-stream oracle. **THE FLAT PRODUCT'S BUILD PRICE (measured 2026-08-16, ACCEPTED)**: capturing an unknown-length stream into flat arrays transiently costs ~2n (the chunked build buffer, discarded, plus the final arrays, kept) and ~+13% build time vs the old chunked memo product — the price of every later read being span arithmetic over raw arrays (replay 1.3–1.8x faster on preorder shapes, same silicon; the MaterializeReplay/BufferProbes families carry the dividend). Counted sources (transposes from settled buffers, settles from completed memos) take the PRESIZE fast path (2026-08-16): the exact count read off the completed store presizes the final arrays and skips the chunks — measured −66% transpose build allocation (the chunked build's doubling overshoot and ToArray copies were part of the price too); the count is a contract, kept loud by a closing check |

### Enumerable / scalar consumers (Copse.Linq)

| Operator | Source dims | Returns | Behavior | Notes |
|---|---|---|---|---|
| GetPreorderTraversal / GetLevelOrderTraversal | D / B | IEnumerable | streams | O(1)–O(depth) |
| GetPostorderTraversal | D | IEnumerable | streams | O(depth) pending path |
| GetRoots / GetLeaves | D (GetLeaves also B, F) | IEnumerable | streams | O(1) |
| GetLevels | B only | IEnumerable\<TNode[]\> | streams per level | O(width) reused deque; one array alloc per level |
| GetBranches | D only | IEnumerable\<TNode[]\> | streams per branch | O(depth); array per yield |
| Get\*Traversal (visit streams) | D, B, F (±strategy selector) | IEnumerable\<NodeVisit\> | streams | |
| RootfixAggregate (seed / selector) | D, B, F(→D) | IEnumerable | streams | RootfixScan + GetLeaves; seed/selector first (type-fixer-first, 2026-08-02); ScanResult sweep: yields leaf pairings; seat rule 2026-08-04: accumulator `(TAcc, TNode)` |
| LeaffixAggregate (leafNodeSelector) | D; **B**(documented capture); F(→D) | lazy IAsyncEnumerable\<NodeAccumulation\> (one per root) | per-root lazy (DFT); capture (BFT) | **value-flavored on the dual shape 2026-08-05** — `(leafSelector \| positional, edgeAccumulator, nodeAccumulator)`, the family's last NodeContext callbacks retired (the deferred signature workstream closed); seed flavors RETIRED 2026-08-06 with LeaffixScan's (THE VIRTUAL-ROOT RULE — they were already implemented AS the translated selector); mechanism = LeaffixScan's node-last fold collapsed to roots; per-root laziness (DFT) and the index-chasing BFT capture preserved |
| AnyNodes / AllNodes / CountNodes / CountTrees | F, D, B | scalar | drains | **flavor sweep 2026-08-05** (predicates arity-split value \| positional); Any short-circuits; CountTrees gained its B + F entries 2026-07-13 (B counting = a level-0 drain via SkipNodeAndDescendants) |
| Consume | F(±strategy), D, B | void | **drains, unconditionally** | MECHANICAL again (2026-07-15, probes REVERTED): walks a treenumerator to exhaustion whatever the receiver — buffers replay inertly, deferred captures are FORCED, a lazy capture completes as a side effect. The probe episode (2026-07-14→15) optimized for a caller that does not exist and silently broke the benchmarks; minimum-work settling lives on the lazy buffer's Complete() member and in Materialize. One word one meaning: Consume walks, Complete finishes, Materialize delivers |
| ToFormattedLines / ToFormattedString | D | IReadOnlyList\<string\> / string | **eager terminal** | honest since 2026-07-15 (flag 2): walks the source ONCE at the call — `To*` name, return shape, and cost now agree; one `(text, depth)` record buffer, reverse-rendered into the pre-sized result (formatter once per node, preorder); glyph contract pinned by `FormattedLinesTests` |
| ~~To\*TreeTokenizer~~ | D / B | tokenizer | streams | DEMOTED to Copse.Linq.Experimental 2026-07-15 (sync only; async deleted, codegen rows dropped): lost its last product consumer when ToFormattedLines went record-based, and shipping now would lock in the token shape — tokens carry less context than a treenumerator (no positions/visit counts), and a real consumer may want richer tokens. Revisit shape-first if a consumer appears. |
| ToDegenerateTree / ToTrivialForest | IEnumerable | ITreenumerable | streams | fresh enumerator per acquisition |

### Walker-tier operators (Copse.Linq, both colors — single-sourced like everything else since the 2026-08-14 crossing)

The contract's door — `GetTreeWalker()`, the walkable's ONLY member beyond
`ITreenumerable` since the Stage C cut (design-docs/WALKER_FACTORY_DESIGN.md, 2026-08-15),
made TOTAL by the sentinel completion (§11 there, 2026-08-20: the door lands on the UNFOCUSED
STANCE, above the roots; the empty forest is the unfocused stance alone, so the door cannot
miss — `GetEnumerator` symmetry at last) — is the seam everything below rides: the four
adjacency probes are provider SPI (`ITreeTopology`), the walker is the entire public
navigation surface. The walker's climb answers to the top (`MoveToParent` from a root =
the unfocused stance; stepping up from it = the one upward miss), `MoveToChild` from the
unfocused stance walks the roots, `GetValue`/`Focus` throw at the unfocused stance (violation channel;
`TryGetValue` is the typed read), and `HasFocus` is `Focus`'s guard (false = the climb topped out). The steps answer
in **`TreeWalkerResult`** — the step family's flat three-state result (missed /
focused / unfocused in one outcome byte, 16 bytes, three fields) rather than a nested
`Option<TreeWalker>`: the nesting makes a four-field aggregate that falls off JIT struct
promotion and costs 2x on the BufferProbes sweeps (the receipt is
WALKER_FACTORY_DESIGN.md §11's perf addendum). Reads like the option it replaces
(`HasValue`/`Value`/`TryGetValue`), so call sites carry over textually.

| Operator | Receiver | Returns | Behavior | Notes |
|---|---|---|---|---|
| Extend(observer) | IWalkableTreenumerable | IWalkableTreenumerable\<TResult, THandle\> | lazy (lens view) | comonadic co-bind: the observer receives THE WALKER at every node (vantage as value, 2026-08-15 — Stage C's honest type); result is a topology transformer over a deferred door knock (`WalkableTopology`); stream half derived through the Walk adapter |
| Extend(observer) | TreeWalker | TreeWalker\<TResult, THandle\> | lazy | the same co-bind at the focused presentation; constructs the extend lens directly (walker in, walker out — co-Kleisli composition's working form); keeps the stance INCLUDING the unfocused stance — observers fire at nodes (the interior part of the completed extend; the unfocused row = `observer(unfocusedWalker)`, a direct application, CATEGORY_THEORY_SURVEY.md §12) |
| Subtrees() | IWalkableTreenumerable | walkable-of-walkables | lazy (lens views) | the cofree duplicate in the severed presentation: labels = re-rooted subtree views sharing source handles (`SubtreeWalkable` — exactly two answers rewritten); laws pinned by `SubtreesLawTests` incl. hand-pinned interior labels; SelectMany's waiting coherence oracle (graft ↔ Subtrees) |
| Subtree() | TreeWalker | IWalkableTreenumerable | lazy (lens view) | the INCLUSIVE HOIST (survey §12): at a node, the severed re-rooted view at the focus — the walker-side spelling of the same lens; at the UNFOCUSED STANCE, the source forest itself (`TopologyWalkable`, the identity view — nothing above it to sever, and the valueless focus has no spelling in the treenumerable, so it drops out by type). Door-then-Subtree = the identity round trip, no case analysis (pinned, `UnfocusedStanceTests`) |
| Duplicate() | TreeWalker | TreeWalker\<TreeWalker, THandle\> | lazy | `Extend(focus => focus)` — extend of the identity, the definition; one line |
| GetTreeWalkerAt(handle) | IWalkableTreenumerable | **TreeWalker** (bare) | O(1) | the TRUST door: door-then-jump (`walker.At(handle)`); pure construction, cannot fail — a forged handle detonates at the first probe (per-capture clause); stored handles re-enter here |
| TryGetTreeWalkerAtRootIndex(k) | IWalkableTreenumerable | TreeWalkerResult | O(1), honest miss | door + one downward step (the roots are the unfocused stance's child group — `TryGetRootAt` was always the sentinel's `MoveToChild`, now literally); answers in the step family's own result shape; RootIndex spelled out so ordinal-vs-handle stays visible when THandle = int |
| GetHandles / GetHandlesWithValues | IWalkableTreenumerable | IEnumerable\<THandle\> / \<HandleAndValue\> | streams (pure stance walk) | acquisition scans: ONE knock, then steps — roots seeded from the unfocused stance's child group; the unfocused stance gets no row (no handle, no value — excluded by type); the walk assigns its own preorder numbering (any-layout receivers fold in place); WithValues = the search law's ONE earned exception (receiver-recovery: a value predicate mid-chain can't reach the walker without naming the receiver twice) |
| PruneAfter lens | IWalkableTreenumerable | pair-citizen view | lazy | stream half delegates to the streaming operator; adjacency half is its own topology (the lens family; crossed colors 2026-08-14 — async source, generated sync twin) |
| SpanningSubtree(targets) | IWalkableTreenumerable | **Option\<TreeWalker\<TValue, int\>\>** | **capture (O(kept), at the call's end)** | **NEW 2026-08-14 (UC-32 distilled — the capstone as an operation)**: minimum spanning subtree of the targets, returned as a walker at the spanning root over a FRESH preorder capture (handles are the new capture's ordinals — the per-capture clause, pinned by test). Result-typed ONCE since the sentinel completion (2026-08-20): k = 0 (spanning of ∅ is ∅) is the honest miss; DISJOINT trees ANSWER — their common ancestor is the unfocused stance, and the result is the spanning forest under an unfocused walker (one spanning subtree per touched tree, unfocused above its own capture's roots); k = 1 = the node alone. Composition of shipped pieces: walker-first LCA fold (TOTAL — climbs meet at the unfocused stance) + path-recording climbs (the kept-set), the hoist (`Subtree()` — severed at a node, the whole forest when unfocused), the handle-decorated-stream clamp (Extend → PruneBefore in handle-space → Select), one Materialize. Future membership LENS makes it zero-copy; semantics fixed here. The private walker-first LCA is the axis wave's first promotion candidate. Crossed colors 2026-08-14 (async SpanningSubtreeAsync = the source; the sync twin generated) |

| Factory | Behavior |
|---|---|
| Tree.FromTopology(topology) | **PUBLIC 2026-08-15** (absorbed the operator tier's internal WalkerWalk, the day the ecosystem opened): the Walk adapter on the one creation surface — the engine drives any `ITreeTopology`'s indexed child probe as a pull, labels resolving during it, both dimensions afforded. The third-party payoff: implement the SPI, and `IWalkableTreenumerable`'s streaming half is one delegation (the walker half is the public `TreeWalker` mint). Lenses self-feed (`FromTopology(this)` — an Extend view's GetValue IS the observation, so no labeled overload exists). Conformance = the degenerate-tower pin |
| Tree.Defer / DeferDepthFirst / DeferBreadthFirst | factory re-runs **per acquisition** (call-by-name — that's Defer's contract) |
| Tree.Lazy / LazyDepthFirst / LazyBreadthFirst (+ dimension-observing form) | factory runs **once**, pinned for both dimensions (call-by-need); the deferral seam every capture op rides |
| Tree.Using (× dims) | resource per acquisition; treenumerator Dispose is the release point |
| Tree.Empty | singleton |
| Preorder/LevelOrderTreenumerable | full citizen over a random-access store (off-native dimension rides cross-order, ~1.08x tax) |
| Preorder/LevelOrderStreamTreenumerable | narrow-dimension over a forward-only stream; fresh stream per acquisition, treenumerator owns/disposes it |

### Serializer surface (Copse.SimpleSerializer)

Registered 2026-08-03 — the serializer had no rows here, which is how the string tier's
schedule went undisclosed. **Every Deserialize overload now has Defer semantics** (the
standard lazy contract): re-enumeration re-parses, the value map runs per traversal, fresh
instances every pass; parse-once-replay-many is the caller's explicit `Materialize`/`Memoize`
escalation.

| Method | Source | Returns | Behavior |
|---|---|---|---|
| Deserialize{DepthFirst,BreadthFirst}Tree (±map) | `string` | **F** | streams via `Tree.Defer`: fresh lazily-parsed string store per acquisition, parse bounded by the traversal frontier; retention scoped to the treenumerator. *(Until 2026-08-03 one growing store was shared by every treenumerator of a result — an undisclosed Memoize schedule selected by overload resolution; unified after it surprised on re-enumeration.)* |
| Deserialize{DepthFirst,BreadthFirst}Tree (±map) / …FromFile | `Func<TextReader>` / path | **D** / **B** narrow | streams: fresh reader per acquisition, owned and disposed by the treenumerator; the unaffordable dimension is absent from the type — escalation is explicit `Memoize`/`Materialize` |
| Serialize{DepthFirst,BreadthFirst}Tree (±map) | D / B narrow receiver | `string` / writer `void` | drains; narrow interfaces are the honest receivers (each layout needs only its own dimension) |

### Experimental

`ExpandNode` and `Graft` (F ×5 overloads each) return `ITreenumerable` but their
breadth-first treenumerator factory is `() => throw new NotImplementedException()` — see flags.

### Policy-audit flags (2026-07-13)

Checked against [the policy](LAZINESS_AND_BUFFERING_POLICY.md). The good news first: **no
per-traversal re-capture exists anywhere** (every capture op is `Tree.Lazy`-pinned), and the
**disposable audit is clean** (`Memoize` is the sole disposable return). The strains:

1. **`ExpandNode` / `Graft` break the dimension split's guarantee at runtime**: full
   `ITreenumerable` return type, `NotImplementedException` on any BFT acquisition. Under the
   split's own rules these should be D-narrow until their BFT arms exist. (Experimental, but
   the type is still lying.)
2. *(RESOLVED 2026-07-15)* `ToFormattedLines` was `To`-named but lazy-shaped and lazy-shaped
   but eager-costed (full forest drain before the first yield, re-drained per enumeration,
   forest buffered twice: token list + rendered-line stack). Fixed by making the shape honest
   rather than the implementation lazy: returns `IReadOnlyList<string>`
   (`ValueTask<IReadOnlyList<string>>` async, now `ToFormattedLinesAsync`) — eager like every
   `To*` terminal, work exactly once. The per-node glyph gap is O(subtree) (a node's ├/└
   needs "does a later sibling follow"), so a streaming shape was never owed under rule 2;
   the per-tree-streaming TODO was deliberately dropped (no consumer). The rewrite also
   halved the buffering: one `(text, depth)` record list (depth deltas carry the full tree
   shape — the tokenizer emits group tokens only on depth changes, so records reconstruct
   it), reverse-rendered bottom-up into the pre-sized result array. Output pinned by
   `FormattedLinesTests` goldens captured from the old renderer.
3. *(RESOLVED 2026-07-13)* `TakeLastTrees` / `SkipLastTrees`: D and B narrow overloads
   added (with the `CountTrees` B twin + F disambiguator they require). The two-pass shape
   was examined and kept deliberately: the sequence-style single-pass trick exists but its
   queue slots hold k whole *subtrees* (the parked preorder-window at tree granularity), so
   two passes at O(1) space is the better default; impure-`Defer` callers can `Materialize`
   first. B's counting pass drains level 0 only.
4. **BFT-narrow leaffix/`RootfixDispatch` / `OrderChildrenBy` double-capture**: the deferred build
   `Materialize()`s the source into a full memo, then walks that capture into the result
   arrays — two O(n) allocations transiently vs one on the DFT path. Correct under the
   disclosure rule, but a candidate for a combined single capture.
   *(RESOLVED for `OrderChildrenBy` 2026-07-15, and better than a combined capture — STREAMING: the
   level-permutation build walks the BFT source ONCE straight into the ordered level-order
   encoding. Sibling groups are contiguous in level-order arrival and no level-d node is
   visited before level d finishes scheduling, so each level's permutation settles before its
   children arrive — one buffered level (O(width) auxiliary) suffices, refuting the original
   commit's "cannot generalize." The result's native layout is level-order; both entries now
   replay their own arrival dimension natively. `LeaffixScan`/`LeaffixDispatch`/`RootfixDispatch`-B
   still hoist — their builds need depth-first order; the capture-then-fold candidate is the
   LeaffixAggregate-B index-chasing pattern.)*
5. *(RESOLVED 2026-07-13)* `Invert(F)`'s BFT-first arm now builds its whole capture on the
   first replay pull (a one-shot drain of the streaming mirror via the stream-shaped
   `LevelOrderCapture.CaptureFrom`), matching the preorder arm's cost shape. The
   dispose-completes-capture surprise is gone (dispose owes nothing; the source is released
   inside the build), `StreamFedLevelOrderStore` was **deleted**, and the orphaned
   `LazyLevelOrderStore` became the arm's deferral seam. The tier-by-tier laziness this
   traded away was only ever real for a replay abandoned *without* disposal.
6. *(FIXED 2026-07-13)* `Materialize` now probes before memoizing: a live memo is consumed
   in place (the aliasing is by design and documented in the XML docs); a completed buffer
   is returned as-is instead of being wrapped in a fresh memo and copied node-by-node.
7. **FLAGGED 2026-08-12 (ruling pending): `TakeNodesUntil`/`TakeNodesWhile`'s composite
   (F) overload manufactures incoherent citizens.** The categorical survey's
   drawing/reading litmus classified these as WALK-level truncations: with O(1) state each
   dimension truncates its own encounter order, so the F result's DFT and BFT streams
   describe DIFFERENT trees (toy: `TakeNodesUntil(c)` over `a(b(d,e),c(f,g))` — the DFT
   reading has {a,b,d,e}, the BFT reading {a,b}; no single tree yields both). The narrow
   D/B overloads are innocent — one truncated walk is a coherent narrow citizen. Options
   on the table: (a) chop the pair entirely; (b) delete only the F overload — the
   dimension split's own medicine, since a truncated walk affords exactly its own
   dimension; (c) re-home the semantics on the walker side, where "walk from here until
   you encounter" is a natural cursor/walk-tier verb; (d) keep F with a pinned
   documented-incoherence test (a coherence detector comparing the trees the two
   dimensions describe). Note: the pair sits in the queued periphery composition wave —
   that work should PAUSE pending this ruling.

## 2. Flat-family dependency map

Async twins are the codegen **sources** (in `Copse.Async` / `Copse.Linq.Async`); the sync
`.g.cs` files are transcriptions. SPIs and array stores live **per-color** (async sources
in `Copse.Async/Stores`, sync twins generated into `Copse/Stores`) — the 2026-07-14
de-share; `Copse.Primitives/FlatStores` is retired.

```
SPIs — per-color since the de-share (2026-07-14): async sources in Copse.Async/Stores
(namespace Copse.Async.Stores), sync twins GENERATED into Copse/Stores (Copse.Stores);
the read structs (PreorderRead/LevelOrderRead) and completed array stores
(Preorder/LevelOrderArrayStore + their new Async* twins) follow the same pattern.
Copse.Primitives/FlatStores is retired.
├─ I(Async)PreorderStore   random-access preorder; growable (Ensure* may pull a feed)
├─ I(Async)LevelOrderStore random-access level-order dual
├─ I(Async)PreorderStream  forward-only preorder; TrySkipToDepth skip seam; disposable
└─ I(Async)LevelOrderStream forward-only level-order groups; disposable

Decoders (Copse/Treenumerators ← Copse.Async/Treenumerators)
├─ PreorderStoreDepthFirstTreenumerator      NATIVE   (span arithmetic)
├─ PreorderStoreBreadthFirstTreenumerator    cross-order (visit queue + schedule stack)
├─ LevelOrderStoreBreadthFirstTreenumerator  NATIVE   (sequential index)
├─ LevelOrderStoreDepthFirstTreenumerator    cross-order (child-span chasing, O(depth))
├─ PreorderStreamDepthFirstTreenumerator     NATIVE only (O(depth) path + lookahead)
└─ LevelOrderStreamBreadthFirstTreenumerator NATIVE only (masked-ring window, O(width))
   (no stream cross-order decoders — the dimension split, by design)

Wrappers (Copse/Treenumerables)
├─ PreorderTreenumerable<TStore>        full citizen → both preorder decoders
├─ LevelOrderTreenumerable<TStore>      full citizen → both level-order decoders
├─ PreorderStreamTreenumerable<TStream>   D-narrow → stream DFT decoder (owns stream)
└─ LevelOrderStreamTreenumerable<TStream> B-narrow → stream BFT decoder (owns stream)

Capture factories (Copse/Stores ← Copse.Async/Stores; public statics; ADDED 2026-07-13)
├─ PreorderCapture.CaptureFrom(source[, sideChannelSelector])  the ENCODE direction, written
│    once: shape A hoisted from the operator builds → PreorderArrayStore (+ preorder-parallel
│    side array — OrderChildrenBy's keys hook). Consumers: Invert's build; OrderChildrenBy
│    adopts at its rebase. CaptureRaw (added 2026-08-02) is the naked-arrays form for
│    consumers that weave a DIFFERENT store from the walk: RootfixDispatch's pass 1 dissolved
│    into it (positions on the side channel; the result store reuses the capture's subtree-size
│    array). The leaffix builds stay bespoke (LeaffixDispatch's close-hook needs
│    DispatchSources, a Copse.Linq type this layer cannot see; LeaffixScan owns no build,
│    it delegates to LeaffixDispatch; LeaffixAggregate folds into open slots as it goes).
└─ LevelOrderCapture.CaptureFrom(source)      shape B in one-shot form (the memo's front-cursor
     parse) → LevelOrderArrayStore. No consumer yet; first candidates are the LeaffixScan-B /
     LeaffixAggregate-B capture-then-fold rebuilds. No side-channel overload until a consumer exists.

Concrete stores/streams                       consumers
├─ (Async)PreorderArrayStore (readonly structs)  Invert-D/F, OrderChildrenBy, LeaffixScan/LeaffixDispatch, RootfixDispatch
│    (per-color since the de-share: Copse/Stores builds all terminate here; benchmarks; tests
│     ← Copse.Async/Stores, completed arrays)
├─ (Async)LevelOrderArrayStore (readonly structs)  Invert-F BFT arm; benchmarks/tests
├─ LazyPreorderStore (internal, Linq)    THE deferral seam: Invert-D, OrderChildrenBy,
│    runs a Func<PreorderArrayStore> once     LeaffixScan all ride it
├─ LazyLevelOrderStore                   Invert-F BFT-first arm's deferral seam (orphan
│                                             ADOPTED 2026-07-13, flag 5)
│  (StreamFedLevelOrderStore DELETED 2026-07-13 — its incremental drain became the
│   stream-shaped LevelOrderCapture.CaptureFrom, one-shot; no preorder dual, still)
├─ Memoize{Preorder,LevelOrder}Store     the memo's resumable captures (preorder /
│    + …Store readonly-struct SPI adapters    level-order encodings, PullOne/Consume)
├─ InvertedLevelOrderStream                   the streaming mirror (O(width) tier transform)
├─ PreorderStringStore / LevelOrderStringStore   serializer string tier (hand-written sync-
│    + nested .Handle struct adapters            only; a string can't suspend)
└─ PreorderTextStream / LevelOrderTextStream     serializer streaming tier (forward-only)

Walker-tier machinery (Copse.Linq/Treenumerables — async sources, generated sync twins;
ADDED 2026-08-14/15, the walker workstream)
├─ (Async)PreorderAdjacencyIndex<TStore>   the GROWING preorder citizen (2026-08-16 split:
│    one engine per layout per completeness): incremental open-span scan; child axis =
│    three parallel linked arrays on RefAppendOnlyList (zero per-node objects — the old
│    List<List<int>> allocated a child list per scanned node); sibling-chain cursor keeps
│    sequential child iteration O(1) amortized; ScanUntouched seam for the reclaim
├─ (Async)LevelOrderAdjacencyIndex<TStore> its GROWING level-order dual (suspendable
│    two-cursor parent merge, RefAppendOnlyList-backed)
├─ (Async)PreorderArrayTopology            the COMPLETED preorder citizen: exact arrays
│    built at most once on first probe per axis — parents in one open-span sweep (the
│    stack lives in the answer array), children/roots as a CSR index derived from the
│    parent map (probes are then 1–3 array reads); pure span arithmetic was measured
│    and REJECTED (+69% on warm walks — BufferProbes, 2026-08-16); carries the
│    bulk-fold Store seam
├─ (Async)LevelOrderArrayTopology          the COMPLETED level-order citizen: children/
│    roots are store arithmetic, parents one exact two-cursor merge on first probe
├─ (Async)LazyTopology (né DoorTopology, then WalkableTopology — settled 2026-08-15 on the
│    MECHANISM name once nothing else was left to distinguish it from; internal sealed in
│    Copse/Topologies, PUBLIC via TreeTopology.Lazy — the topology tier's creation surface
│    beside Tree's, since it has zero Linq dependencies: the maroon pattern's third strike)
│    Stage C's deferral seam: "the topology this walkable's door WILL bind," knocked once
│    at first probe (Tree.Lazy semantics — the contract promises neither cheap nor
│    idempotent doors, so the cache is what keeps a view honest); the total door always
│    yields a bound topology, so the empty forest answers as itself — probes miss,
│    GetValue throws (the two-channel doctrine); resolves to the door walker's
│    public Topology (WalkerTopology, its short-lived eager sibling, RETIRED same day by
│    the frame-of-reference ruling — an eager bridge from a vantage in hand is just the
│    property read)
├─ (Async)ExtendWalkable / SubtreeWalkable / PruneAfterWalkable   the lens family — each
│    is its own topology (topology transformers; every door is total now — the unfocused mint)
├─ (Async)TopologyWalkable   the identity view (NEW 2026-08-20, the sentinel completion):
│    a topology worn as a walkable, nothing rewritten — what the unfocused stance's Subtree()
│    denotes; streams via Tree.FromTopology over the same topology the door binds
└─ WalkerWalk RETIRED 2026-08-15 → public Tree.FromTopology (Copse; frame struct =
     (Async)TopologyChildEnumerator beside the engine): the Walk adapter joined the one
     creation surface; lens views self-feed through it

Outside the family, on purpose:
└─ TestUtils PreorderTree — same (values[], subtreeSizes[]) encoding but rides the DFS/BFS
   ENGINE via PreorderChildEnumerator: the conformance oracle must not route through the
   flat-family playback it referees. (PreorderArrayStore's header still claims PreorderTree
   "dissolves into" it — aspirational, not current.)
```

## 3. Ad-hoc store construction sites (the cohesion-pass work list)

Two canonical loops are re-implemented across the codebase:

- **Shape A — the DFT capture loop**: walk `SchedulingNode` visits; open-index stack;
  backfill `subtreeSizes[closed] = values.Count - closed` on depth retreat; `0` = still open.
- **Shape B — the BFT capture loop**: append `(value, firstChildIndex=-1, childCount=0)`;
  wire into the front node's span; advance the front on first visit.

| # | Site | Shape | Builds | Variation |
|---|---|---|---|---|
| 1 | `Treenumerable.Invert.g.cs` `BuildMirror` | ~~A~~ **factory** + span-hop emit | `PreorderArrayStore` | *(2026-07-13)* phase 1 now rides `PreorderCapture.CaptureFrom`; the zero-key LIFO emit stays specialized (CI benchmark rows) |
| 2 | ~~`Treenumerable.OrderChildrenBy.g.cs` `BuildOrderedChildren`~~ | ~~A + span-hop emit~~ | `PreorderArrayStore` | *(RESOLVED on the branch, 2026-07-15)* the shape-A half rides `PreorderCapture.CaptureFrom(source, keySelector)` — the keyed side-channel overload built for this consumer; the operator keeps only the sibling-group-sort emission |
| 3 | `Treenumerable.LeaffixScan.g.cs` `BuildLeaffixScan` | A | `PreorderArrayStore` | richer close: pending-node stack carries NodeContext, close computes the accumulation |
| 4 | `Treenumerable.LeaffixAggregate.g.cs` | A | **no store** | same loop, per-root reused buffers, lazy yield — bounds what a store factory can absorb |
| 5 | `Memoize{Preorder,LevelOrder}Store.g.cs` | A / B, resumable | memo buffers | `PullOne` = one loop iteration suspended; `Consume` = the loop with guards hoisted; selector `VisitCount==1` instead of `SchedulingNode` (equivalent in DFT — documented there) |
| 6 | ~~`StreamFedLevelOrderStore.g.cs`~~ | B (append wiring) | — | *(2026-07-13)* deleted; its drain lives on as the stream-shaped `LevelOrderCapture.CaptureFrom(ILevelOrderStream)` |
| 7 | `PreorderStringStore` / `LevelOrderStringStore` (serializer) | A / B arrays from **text** | themselves | open stack driven by `(`/`)` or group terminators; leaves committed `subtreeSizes=1` immediately (vs backfill) |
| 8 | `TestUtils EngineTree.ParseArrays` | A from text | raw arrays for `PreorderTree` | intentionally independent (oracle) |
| 9 | `Benchmarks FlatDecode.FlatEncodings` | A verbatim; plus a preorder→level-order **transpose** that exists nowhere in product | both array stores | transpose was measured out of product (~1.08x cross-decode tax vs ~5-replay break-even) |
| 10 | ~~`Copse.Linq.Tests FlatFamilyConformanceTests`~~ | ~~A / B verbatim~~ | — | *(RESOLVED 2026-07-15)* the duplicate stores fell to the public types, then the hand-rolled A/B build loops fell to `PreorderCapture`/`LevelOrderCapture.CaptureFrom` — conformance now runs the product chain (factory → store → decoder) against the engine oracle |

Each product site (1–6) exists twice on disk, once in source — the async file in
`Copse.Linq.Async` is the edit surface; the sync `.g.cs` is generated.

## 4. Coherence observations (feeding discussion-queue item 3)

1. **The array stores are the natural factory home.** All three operator builds terminate in
   `new PreorderArrayStore<T>(values, subtreeSizes)` after restating the store's own
   documented invariant. A `PreorderArrayStore.CaptureFrom(source[, per-node selector])`
   factory plus a sibling-group-reorder emission would collapse sites 1, 2, and the
   benchmark/test copies; `LazyPreorderStore(() => PreorderArrayStore.CaptureFrom(...))`
   is exactly today's call pattern with the loop named and moved. LeaffixScan needs a
   close-hook (accumulator) and LeaffixAggregate needs the no-store reusable-buffer form —
   they mark the boundary of what one factory can absorb.
2. **Placement problem — DECIDED 2026-07-13** (see
   [STORE_FAMILY_REVIEW.md](STORE_FAMILY_REVIEW.md)): `Copse.Primitives` references only
   `Copse.Vocabulary` and cannot see treenumerators at all, so the factories go in the
   `Copse`/`Copse.Async` codegen pair (the layer that already owns the decoders). Not yet
   built — sequenced after the OrderChildrenBy-B streaming spike (flag #4).
3. **Naming seams**: (a) *(RESOLVED 2026-07-13)* the memo cluster's storage types were
   renamed to encoding names (`MemoizePreorderStore`/`MemoizeLevelOrderStore` +
   `MemoizePreorderStore`/`MemoizeLevelOrderStore`) under the adopted rule — traversal
   things carry dimension names, storage things carry encoding names; every store now has a
   one-line taxonomy header; (b) *(RESOLVED by the de-share 2026-07-14)* the nested
   `.Handle` adapter convention is now universal (memoize stores and serializer alike);
   (c) *(RESOLVED 2026-07-15)* the test-side store re-implementations are gone —
   `FlatFamilyConformanceTests` rides the public stores and the public capture factories.
4. **Missing duals** (cross-check [dual-symmetry backlog]): ~~`LazyLevelOrderStore`
   orphan~~ *(adopted 2026-07-13 — it is now Invert-F's BFT-first deferral seam; the
   stream-fed store it displaced was deleted, its drain preserved as the stream-shaped
   `LevelOrderCapture.CaptureFrom`)*; no stream-shaped `PreorderCapture.CaptureFrom`
   (`IPreorderStream`) dual yet — nothing needs it; ~~no public sync→async completed-store
   adapter~~ *(dissolved by the de-share 2026-07-14: `Async{Preorder,LevelOrder}ArrayStore`
   are real types now, and the benchmark-private hack is deleted)*; the
   preorder→level-order transpose lives only in benchmarks (deliberately).
5. **Selector inconsistency inside shape A**: operator builds filter on
   `Mode == SchedulingNode`, memo/tests on `VisitCount == 1`. Equivalent in DFT, but a
   hoisted factory should pick one and document why.
