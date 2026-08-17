# The Public Projection Citizenship (Select Into Captures)

> **Status: DRAFT v2, 2026-08-16** — reshaped after review; awaiting ratification. Origin:
> the pair-product price (OPERATOR_SURFACE_MAP.md LeaffixScan row). Scans deliver
> `(Node, Accumulate)` pairs by ruling — the richer contract justifies itself — and
> `Scan(...).Select(x => x.Accumulate)` should recover the narrow product's cost. Today
> that spelling is a stream veneer over a pair buffer built in full. This design makes the
> spelling collapse — and does it through a door anyone can walk through, not a private
> arrangement between our operators.

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

public interface IAsyncSelectComposableTreenumerableBuffer<TNode> : IAsyncTreenumerableBuffer<TNode>
{
  IAsyncSelectComposableTreenumerableBuffer<TResult> ComposeSelect<TResult>(Func<TNode, TResult> selector);
}
```

(The return types are the CITIZENSHIPS, not the bare contracts: closure is a contract
property — `Select ∘ Citizen = Citizen` is enforced by the signature itself.)

(Sync twins generated. Positional-flavor question — a second method vs. value-only — is
OPEN: a second method doubles the final-contract surface; a citizen whose machinery cannot
see positions cannot honor it. Lean: value-only, and the positional Select flavor takes
the wrapper over citizens, symmetric with the lattice's Relabels guard.)

**Select's probe order, deterministic and documented:** internal lattice first (our
streaming operators), public citizenship second, wrapper fallback last. The buffer-receiver
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

**THE FIRST-CALLER FUSION (the guard-rail rule, 2026-08-17):** the shared pass builds when
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

## 5. The layering north star

With scans composable, capture-tier operators become definable as compositions over Scan —
a subtree-selection operator ("does this subtree satisfy X" is a fold fact) stops
hand-building its own stores and inherits the collapse. Candidates: TakeSubtreesWhere's
capture arms, the prospective SelectSubtreesWhere. The same move the SelectWhere lattice
made for the streaming tier, applied to the capture tier: the capture algebra gets one
home.

## 6. Out of scope

- **Filtering citizenship** (public Where composition): the internal-ruling's logic stands;
  revisit only with a design that makes foreign filter invariants checkable.
- **The memo receiver**: dispose-ownership questions; memo consumers Materialize first.
- **Benchmarks**: consult-first when implementation starts — a composed-vs-veneer pair in
  the Compose-family idiom; the headline is retained size and replay speed, and with the
  citizenship as the mechanism the Alloc column is favorable from day one (no
  worse-transient era to asterisk).
