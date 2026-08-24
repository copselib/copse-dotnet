# The Public Projection Citizenship (Select Into Captures)

> **Status: CLOSED 2026-08-18.** Drafted 2026-08-16, ratified 2026-08-17, and completed
> through the thin shape (§4a), the emission mint, both compose-left doors (leaffix
> capture-shaped, rootfix streaming-shaped), and the fourth cell (SCAN_TIER_DESIGN.md).
> Every ruling is taken: the memo is a compose seam by law, narrow parity is deferred
> until demand, filtering citizenship is internal-forever (§6). All steps are on the CI
> record. This document is history. Origin:
> the pair-product price (OPERATOR_SURFACE_MAP.md LeaffixScan row). Scans deliver
> `(Node, Accumulate)` pairs by ruling — the richer contract justifies itself — and
> `Scan(...).Select(x => x.Accumulate)` should recover the narrow product's cost. Today
> that spelling is a stream veneer over a pair buffer built in full. This design makes the
> spelling collapse — and does it through a door anyone can walk through, not a private
> arrangement between our operators.

> **SUPERSEDED IN SHAPE (2026-08-18, the same day this document closed):**
> PUBLIC_COMPOSITION_SURFACE_DESIGN.md generalizes the citizenship. The interface is now
> `IAsyncSelectTreenumerable` (no "Composable"), joined by `IAsyncPruneAfterTreenumerable`
> (the boundary law: public composition = what consumer strategies can express), the
> internal `IAsyncSelectWhereTreenumerable` EXTENDS both (the reversed hierarchy), the
> probe ladder collapsed to one sniff per door under the door-optimality law, and the
> canonical wrapper classes went public as the citizenship's vehicles. Every RULING here
> (filtering internal-forever, value-only seat rule, compose-left parked, the laws and the
> dogfooding proof) carries forward unchanged -- only the citizenship's SHAPE moved.

## 1. The ruling this design splits

The SelectWhere lattice's citizenship is INTERNAL by deliberate ruling: a public recipe
would make our operators' correctness depend on foreign implementations, and the older
TFMs' lack of default interface members makes every interface evolution a breaking change.

That ruling is really two claims, and they deserve different fates:

- **Filtering stays internal.** The claim holds: the SelectWhere recipe carries subtle
  invariants (traversal strategies, the relabel join rule, promotion semantics) that a
  foreign implementation would get wrong in ways our operators would silently inherit.
- **Projection goes public.** Projection has almost no invariant surface: project the
  values, keep the shape. Positions never move, the visit stream is untouched, no strategy
  channel exists. A citizenship this small can be public without the lattice's fragility —
  and it must be: we and third parties should be able to drop in a select-composable
  treenumerable and have the framework fold it in with NO action beyond implementing the
  interface. No registration, no cooperation from this library.

## 2. The citizenship

Per-tier interfaces (no HKT in C#; the CompositeToNarrow situation), PUBLIC, one method
each, minimal and FINAL (the old-TFM constraint: members added later break every citizen —
so nothing speculative goes in):

```csharp
public interface IAsyncSelectComposableTreenumerable<TNode> : IAsyncTreenumerable<TNode>
{
  IAsyncSelectComposableTreenumerable<TResult> ComposeSelect<TResult>(Func<TNode, TResult> selector);
}

public interface IAsyncSelectTreenumerableBuffer<TNode> : IAsyncTreenumerableBuffer<TNode>
{
  IAsyncSelectTreenumerableBuffer<TResult> ComposeSelect<TResult>(Func<TNode, TResult> selector);
}
```

(The return types are the CITIZENSHIPS, not the bare contracts: closure is a contract
property — `Select ∘ Citizen = Citizen` is enforced by the signature itself.)

(Sync twins generated. Positional-flavor question — a second method vs. value-only —
RESOLVED as the lean: value-only `ComposeSelect`, and the positional Select flavor takes
the wrapper over citizens, symmetric with the lattice's Relabels guard.)

**Select's probe order, deterministic and documented:** light tier first
(`IAsyncSelectPruneAfterTreenumerable`), public citizenship second
(`IAsyncSelectComposableTreenumerable` — moved above the general probe when the scan
citizens joined, `53ce350`), general driver third (`IAsyncSelectWhereTreenumerable`),
wrapper fallback last. The buffer-receiver
`Select` overload (returns `IAsyncTreenumerableBuffer<TResult>` — the buffer-producer rule
discloses the O(n)) probes the buffer citizenship, falling back to a COUNTED projected
re-capture (the presize rule: the settled source's count is known; one narrow store, no
transient trap).

**We dogfood the public door**: scan result buffers implement the buffer citizenship with
no private privilege. That is the proof the door is sufficient.

## 3. Compose left and right (the requirement), and the laws (the admission test)

The citizenship is an ENTRY INTO THE SELECTWHERE ALGEBRA, not a parallel Select-only
algebra. The defining equations (ratified framing, 2026-08-16 — composition in either
order lands IN the algebra, never in a wrapper stack):

```
Select      ∘ Citizen     = Citizen       (the citizen's door; closure — ComposeSelect
                                           returns a CITIZEN, by contract, not courtesy)
SelectWhere ∘ Citizen(-composed)
                          = SelectWhere   (the JOIN: the first filter over a citizen or
                                           its composed result produces the ONE composed
                                           SelectWhere driver, which absorbs everything
                                           downstream)
Select      ∘ SelectWhere = SelectWhere   (the lattice's existing law, unchanged)
```

So `scanBuffer.Select(f).Where(p).Select(g).Where(q)` is a narrow build with `f` folded
in plus exactly ONE SelectWhere driver carrying `(p, g, q)` — never stacked wrappers.

**THE TWO-VERB GRAMMAR (ratified in review, 2026-08-17 — do not unify these):**
`Compose` is SAME-KIND SUCCESSION — "here is more mapping, build your successor" — one party
holds both the type knowledge and the construction knowledge, its output is always a
treenumerable, and its floor is one wrapper on the walk. `CaptureThrough` (the compose-left
door, IAsyncProjectionSource/IAsyncProjectionConsumer) is CROSS-KIND SURRENDER — the pieces
(inner tree, projector) cross a knowledge boundary to a builder of a different kind, and the
walk drops to zero wrappers. Double dispatch because the wrapper's inner type is existential
at the consumer's site: the consumer supplies a generic method, the wrapper instantiates it
(the one scope where TInner can be spelled). Only the pure-projection wrapper claims the door
(a filter-carrying driver cannot surrender); the door member lives in its own file
(AsyncSelectTreenumerable.CaptureThrough.cs, a partial part) because the CompositeToNarrow
fan-out is file-granular and the narrow twins must not claim a composite-width door.

**THE FIRST-CALLER COLLAPSE (the guard-rail rule, 2026-08-17; the shared-pass layer it
governed was superseded by the thin shape, §4a):** the shared pass builds when
its first variant pulls, and at that moment the requesting variant is known — so a canonical
first caller gets its pair products written INLINE in the fold loop (the pre-pass cost
exactly; the zip's ValueAt-delegate re-reads had cost the un-composed in-place spelling +16%,
caught by the BufferProbes guard row), a composed first caller declines and the pair array is
never built (the 1-wide promise), and only canonical-after-composed pays a second-pass zip.

The two directions in mechanism terms:

- **Compose right** (lattice downstream of the citizen): `Scan(...).Select(f)` — the
  projection folds into the citizen's build through `ComposeSelect`. The public door.
- **Compose left** (lattice upstream of the citizen): `source.Select(f).LeaffixScan(...)`
  — the capture operator consumes an internal Select citizen and composes the upstream
  mapping into its own walk: the fold reads `f(node)` directly, the projection wrapper
  dissolves. Receiver-smartness about SOURCES, the mirror of Select's probe about
  receivers. Scope note: this direction reads the internal lattice's accumulated mapping,
  so it is OUR capture operators' ability; a third-party citizen cannot unwrap the internal
  lattice from outside. Third parties get full compose-right; a public "hand over your
  projection" door on the light Select tier is PARKED with the filtering question.

The battery (the CrossTierCoherenceTests shape, public via TestUtils so citizens can
self-certify, conditional on claiming the citizenship like the walker law suites):

1. **Functor laws** (govern chained Selects on any citizen):
   `ComposeSelect(identity) ≡ source`;
   `ComposeSelect(f) then ComposeSelect(g) ≡ ComposeSelect(g ∘ f)`.
2. **Compose-right pins**: `citizen.Select(f).Where(p).Select(g)` — the capture tier
   composes until the first filter, the JOIN produces the one SelectWhere driver, `g`
   lands in ITS mapping; extensionally equal to the all-wrapper spelling AND structurally
   one driver (the NarrowCompositionTests idiom — pin the composed machinery's type, not
   just its output; a wrapper stack that computes the right answer still fails the pin).
3. **Compose-left pins**: `source.Select(f).LeaffixScan(...)` ≡ the wrapper spelling —
   same fold, `f` applied inside the walk; and the Materialize analog
   `source.Select(f).Materialize().Select(g)` ≡ `source.Select(g ∘ f).Materialize()`.
4. **The wrapper-equivalence anchor**: `citizen.ComposeSelect(f)` extensionally equals
   wrapper-Select over the citizen, on the corpus — the law every other law reduces to.

## 4. The scan seam (first citizens)

The scan builds get parameterized on what they STORE: `productSelector(node, accumulate)`,
defaulting to `NodeAccumulation.new`. A composed Select makes it `f ∘ pair` — the pair
struct is still constructed per node, on the stack, never stored; chained Selects keep
composing. One-time surgery in the Leaffix/Rootfix builds (span fast path, walker fold,
dispatch store build); the retarget is legal precisely while the build is unpinned (the
deferred-once law: the intermediate that never built cannot be observed missing). The
result: `Scan().Select(f)` is a 1-wide build — no pair store, allocation strictly below
the veneer — and the pair contract stays the default for everyone else.

**The at-most-once constraint (found during seam implementation, 2026-08-16):**
`ComposeSelect` returns a NEW citizen while the original stays alive and pullable — if each
held an independent build, pulling both would walk the source twice, which is illegal for
one-shot stream sources. The citizen architecture therefore shares ONE FOLD PASS (values,
skeleton, accumulations — built at most once) among all product variants; each variant owns
only its finisher zip. This also produces the promised steady state: a built variant drops
its pass reference, so in the chained spelling (original composed away, never pulled) the
pass is collected and what remains is the 1-wide product store plus a SHARED skeleton
array (subtree sizes are identical across variants — one int[] serves all).

Later citizens ride the same door: Materialize's deferred buffer (where `ComposeSelect` is
the composition-law rewrite `source.Materialize().Select(f)` → capture `f(source)`
directly), Invert's build, TakeSubtreesWhere's capture arms.

## 4a. THE THIN SHAPE (2026-08-17) — the buffer tier's mechanism, superseding section 4

Section 4's mechanism (product-parameterized scan builds + a shared fold pass + per-variant
finisher zips) was built, measured, and RETIRED the same week. The worth-it audit's finding:
buffer composition stops at every buffering boundary by design (each scan is a buffer
producer by contract), so the pass machinery's whole surface served exactly one seam — and
the three-arm harness priced that machinery at ~12ms/M-nodes of plumbing, SLOWER than the
transient pair store it existed to avoid.

The replacement inverts the ownership. Scans return PLAIN buffers again (span fast path
restored for scan-of-scan; zero product machinery in any build). Buffer-tier citizenship
is minted at the SELECT seam instead: `Select` over any buffer returns a
`ProjectedTreenumerableBuffer` — the source buffer plus a selector, whose deferred build is
ONE counted array map off the source's completed store (via the `TryGetPreorderStoreAsync`
door; veneer-capture fallback for foreign buffers). `ComposeSelect` composes the SELECTOR
(g∘f over the original source), so chained Selects stay one map, and the source buffer —
replayable by contract — is the sharing substrate that makes at-most-once trivial: no
shared pass needed.

Measured (1M-node chain, net8): projection over a buffer is effectively free — composed ≈
plain (the ~2ms map is paid back by decoding the narrow store), vs veneer ~+25%, vs the
retired pass machinery ~+10%. Scan-of-scan healed from 232ms/272MB (the citizen buffer
type missed the span path's concrete sniff) to ~101ms/108MB. Corollary landed with it:
the dispatch tier's result buffers now wire probes-at-birth over their own lazy store —
the former `Tree.Lazy` wrapping hid the store, and every receiver-smart consumer
(a second scan, the projected map) paid a full second capture to reach it.

## 5. The layering north star

With scans composable, capture-tier operators become definable as compositions over Scan —
a subtree-selection operator ("does this subtree satisfy X" is a fold fact) stops
hand-building its own stores and inherits the collapse. Candidates: TakeSubtreesWhere's
capture arms, the prospective SelectSubtreesWhere. The same move the SelectWhere lattice
made for the streaming tier, applied to the capture tier: the capture algebra gets one
home.

**FIRST LANDING (2026-08-17, Jason's spelling)**: TakeSubtreesWhere IS
`RootfixScan(false, (kept, n) => kept || predicate(n)).Where(pair => pair.Accumulate).Select(pair => pair.Node)`
— "keep this node" is the rootfix fold fact "an ancestor-or-self matched", the outermost
rule falls out of the disjunction's short-circuit, and the buffer arms retired entirely:
the chain streams BOTH dimensions (the "result's BFT cannot stream" rationale was
disproven — general Where's breadth-first wrapper produces the re-rooted forest's true
level order by pulling its inner ahead through its queue, verified on the
deep-match-first-in-preorder wall shape and pinned in the battery). Refined the same day
by the honest-streaming-baseline rule (memory dropping when un-buffering is table stakes;
time answers to the best streaming implementation): the composite DIMENSION-DISPATCHES —
DFT takes the bespoke O(1) contiguous-segment wrapper (the chain measured ~2.3x it; the
dispatch put composite DFT below the retired buffer), BFT takes the chain (the leanest
streaming form there). The dispatch lives BEHIND the citizenship — the result is a
streaming citizen carrying (source, predicate) as its recipe, so mid-chain the operator
is not a composition seam: Select composes onto the product selector, Where joins the
driver over the citizen. The algebraic spelling remains the operator's definition and
its BFT implementation; the machinery choice is an acquisition-time fact, invisible to
the algebra.

## 6. Out of scope

- **Narrow citizenship parity — DEFERRED UNTIL DEMAND (Jason's ruling, 2026-08-18: "it's
  possible to do — maybe do later if there is demand").** Narrow (single-dimension) scan
  results stay non-citizens for now: narrow chains are consume-shaped by nature (a narrow
  source exists because it affords only one dimension), and today's cost is at most one
  wrapper layer over floor-state machinery. Not a won't-do: the composite citizenship is
  the template, the CompositeToNarrow fan-out is the mechanism, and a workload composing
  long narrow scan chains is the trigger. File demand here when it appears.
- **Filtering citizenship — CLOSED as internal-forever (Jason's ruling, 2026-08-18: "too
  difficult to do right").** The public composition door stays projection-only,
  permanently. The lattice's correctness rests on invariants a foreign filter could
  silently violate — `Relabels` honesty, strategy-carrying result contracts, the promotion
  machinery's assumptions, and (since the fourth cell) fold-state discipline — and no
  checkable-invariants design has emerged or is sought. A projection cannot lie in ways
  that corrupt structure; a filter can. Filtering composes only through the library's own
  audited machinery.
- **The memo receiver — CLOSED as law (Jason's ruling, 2026-08-18): the memo is a COMPOSE
  SEAM.** Above it, the total algebra (Select/Where/prune chains over a memo compose into
  one driver, as over any source); through it, nothing — no operator composes INTO the
  memo's machinery, and no recipe holds the disposable. Every pull pays one pass-through
  at the memo's replay layer, which is the seam's honest cost. The memo joins the barrier
  taxonomy beside Do (the count barrier) and Hide (the isolation barrier) as the
  buffer-tier composition seam. `Materialize` is the bridge to capture-tier composition:
  it retires the feed at a moment the caller chose and hands composition a plain,
  non-disposable citizen. (The pre-existing deferred-capture-over-a-live-feed hazard —
  `memo.Select(f)` via the buffer overload holds the memo until first pull, and
  dispose-before-pull throws per the replay rule — predates composition entirely and is
  filed separately as a graceful-handling discussion item; it is orthogonal to compose.)
- **Benchmarks**: consult-first when implementation starts — a composed-vs-veneer pair in
  the Compose-family idiom; the headline is retained size and replay speed, and with the
  citizenship as the mechanism the Alloc column is favorable from day one (no
  worse-transient era to asterisk).
