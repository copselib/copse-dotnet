# SelectMany (bind) — semantics decision

> **Status: SHIPPED 2026-08-21 (Addendum IV) — semantics decided 2026-07-04, amended by Addenda II–III.** Ends a ~3-year
> deliberation on what tree-flatten should mean. The decision is recorded here so
> implementation (whenever it happens) starts from the answer, not the agony.
> Companion to [IX_MORELINQ_SURVEY.md](IX_MORELINQ_SURVEY.md) (candidate #1) and
> [TRAVERSAL_DIMENSION_SPLIT.md](TRAVERSAL_DIMENSION_SPLIT.md) (the cost lens used to
> break ties).

## The decision

`SelectMany` is **root-graft substitution** over forest-valued selectors:

- `f : NodeContext<T> -> ITreenumerable<U>` — each node maps to a **forest** (k roots).
- Each node x is replaced **in place** by f(x). Substitution is per-node and order-free —
  bind is NOT "concatenate expansion trees in visit order" (any visit-order definition
  privileges a traversal dimension; substitution is dimension-neutral).
- x's original children (each themselves already rewritten by the same rule) re-hang:
  - **k = 1**: under the root of x's own expansion, **after the expansion's own
    children** (GHC `Data.Tree` order);
  - **k = 0** (empty expansion): **promoted into x's vacated slot** — exactly `Where`'s
    child promotion;
  - **k ≥ 2**: under the **last** root of the expansion forest — the choice that
    degenerates continuously to both cases above.
- `Return(x)` = the single-node tree. `Where(p) ≡ SelectMany(x => p(x) ? Return(x) :
  Empty)` **by definition** — Where is the bind restricted to {Return, Empty}, and its
  child-promotion machinery is the k = 0 boundary of this rule. The library committed to
  this monad's boundary behavior years ago; SelectMany is the interior.

Worked example — `a(b, c)` with `f(*) = *1(*2, *3)`:

```
source:      a                result:            a1
            / \                          ┌───┬───┴───┬────────┐
           b   c                         a2  a3      b1       c1
                                                    /  \     /  \
                                                   b2   b3  c2   c3
```

Invariant: the expansion roots reproduce the source tree at the source's depths
(delete every *2/*3 and `a1(b1, c1)` remains). Root-graft **preserves original nodes'
depths and levels**; expansions sprout beside the original structure.

## Why this one (the alternatives, and what killed each)

There is no canonical tree monad to defer to — the tree functor admits multiple lawful
monad structures (even sequences do: concat-bind and the diagonal/Omega monad). The
choice comes from fit. The full space, given that bind must be *substitution* and the
only question is where x's original children go relative to f(x):

| Placement | Lawful? | Verdict |
| --- | --- | --- |
| Under expansion root, expansion's children first | ✓ | **CHOSEN** |
| Under expansion root, original children first | ✓ | Lawful but loses on mechanics: the wrapper must pause f(x)'s treenumerator after its root, run the source children, then resume — a paused inner treenumerator held open per ancestor. Chosen order drains f(x) contiguously, one live inner treenumerator per path level. When the laws are indifferent, the visit-stream mechanics vote. Also: GHC `Data.Tree` precedent agrees. |
| Promoted to sibling trees of f(x) ("everything becomes a forest") | ✗ | **Fails right identity**: `a(b).SelectMany(Return)` = forest `[a, b]` ≠ `a(b)`; compounds to total flattening (`a(b(c))` → `[a, b, c]`). Promotion is forced and lawful only at k = 0, where there is no root to hang under — Where's `depth != 0` case. Generalizing the k = 0 boundary inward breaks the laws; generalizing the k = 1 interior outward (children-under-a-root) limits correctly to both boundaries. |
| Leaf-graft (children under f(x)'s deepest/last leaf) | ✓ | Lawful (free-monad flavored; makes spine-encoded sequences concatenate) but **dimension-hostile**: depth shifts accumulate down every spine, so BFT hits the emission-vs-arrival reorder wall. Streams beautifully in DFT only. NOT bind — a separate, differently-named future operator (working name `ExpandDeep`), naturally `IDepthFirstTreenumerable`-typed post-split. |

Cost profile of the chosen semantics: position math is `Where`'s machinery generalized
(k = 0 is literally Where's depth-compression carry; sibling indices shift by expansion
child counts). Streamable in both dimensions with Where-class effort; the BFT wrapper
holds live f-treenumerators across its frontier.

## Implementation notes (for pickup)

1. Prerequisites: `Return` (single-node factory — missing today) and `Empty` (exists).
2. **Monad-law property tests** — the point where CLAUDE.md's "the laws cannot be
   enforced by the type system" becomes "the laws are enforced by the test suite."
   Left identity, right identity, associativity over random trees/selectors; the k ≥ 2
   (forest-valued, children-under-last-root) associativity case specifically needs
   verification — it was chosen for its continuity, its law-compliance is asserted, not
   yet proven.
3. Derived-law regression: `SelectMany(x => Return(g(x)))` ≡ `Select(g)`, and
   `SelectMany(Return-or-Empty)` ≡ `Where` (diff the visit streams, not just shapes).
4. Audit `Copse.Linq.Experimental` (`ExpandNode`, `Graft`, `Collapse`) against these
   semantics — ExpandNode is bind restricted to selected nodes and should be reconciled
   or absorbed.
5. Separate DFT/BFT wrappers, per the usual pattern; expect the BFT one to be the hard
   one (deferred parent visits interact with expansion content the way Where's promotion
   does, plus live inner treenumerators).

---

## VERIFICATION ADDENDUM (2026-08-12) — implementation note 2 executed; the flagged case FAILS

The categorical audit's phase 3 (design-docs/CATEGORY_THEORY_SURVEY.md §6) ran the monad-law
verification this document asked for, via a reference-model oracle grounded against the
shipped operators (`SelectManyLawVerificationTests`: bind restricted to {Return, Empty}
reproduces the real `Where` byte-for-byte; bind of Return-composed reproduces the real
`Select`). Results:

- **Left identity, right identity: PASS.**
- **Associativity over the TREE-VALUED fragment (k ≤ 1 — Empty or a single tree,
  including empties on generated values and expansion-of-expansion): PASS** across the
  corpus. The Return/Empty boundary plus the Data.Tree interior is a lawful
  monad-with-zero.
- **Associativity with FOREST-VALUED selectors (k ≥ 2): FAILS** — the exact case this
  document flagged as "asserted, not yet proven." Pinned counterexample
  (`Finding_ForestValuedSelectors_BreakAssociativity_TheCounterexample`):
  `a(b(d,e),c(f,g))` under f = {b→∅, c→(c1, c2(c3)), v→v1(v2)} then g = {…2→∅, …3→two
  roots, v→vx} yields `…c3R, f1x, g1x` left-associated but `…c3R(f1x, g1x)`
  right-associated. **Mechanism: "under the last root" is not stable under composition,
  because a later bind can erase the root you attached to** — left-associated the children
  were attached to c2 and then promoted out of it by g; right-associated the composite
  selector had already erased c2, so attachment fell to c3R. No fixed-root attachment rule
  survives downstream erasure; the continuity argument that chose last-root does not
  survive the laws. The failure is structural, not a tuning error.

**Options for the decision-holder** (the 2026-07-04 decision is amended only by its own
stated criterion — lawfulness):

1. **Restrict bind to tree-valued selectors** (k ≤ 1): the verified-lawful core, keeping
   `Where ≡ SelectMany(Return-or-Empty)` and `Select ≡ SelectMany(Return∘g)` as theorems.
   Wrinkle: the library has no tree-not-forest type (every `ITreenumerable` is a forest),
   so the restriction is a documented contract or a shaped API (e.g., selector returns a
   `Return`-family builder), not a type-system fact.
2. **Slot semantics**: expansions carry an explicit attachment marker that survives
   erasure by an inheritance rule — the "slotted" idea from the Empty-graft discussions;
   requires designing slot inheritance and re-running this verification.
3. **Ship k ≥ 2 as a documented non-law** — rejected by default: bind is the monad's
   definer and CLAUDE.md demands the laws; a non-associative bind is a contradiction, not
   a deviation.

Prediction post-mortem (audit record): the running "vanish beats promote" prediction was
**wrong about the boundary** — promote-at-k=0 is in the lawful fragment — and **right
about the mechanism** — the failure occurs where promotion (a later bind erasing a root)
meets attachment. The fork's true verdict: the *boundary rules* are fine; the *forest
attachment* is what resists algebra.

---

## VERIFICATION ADDENDUM II (2026-08-21) — option 2 realized and VERIFIED: pointed expansions

The sentinel completion (CATEGORY_THEORY_SURVEY.md §12) reframed the 2026-08-12 failure:
it is the same arity-one theorem that forced the unfocused stance — a forest has
zero-or-many roots, so no rule that *picks* a root can be lawful; the selector must
*designate* the attachment stance. That makes option 2 precise: **the selector returns a
POINTED expansion**, and the slot-inheritance rule the addendum above said "requires
designing" turns out to cost nothing.

**The representation: the point is a phantom child.** Each selector forest carries
exactly one marker leaf at its attachment stance. Attachment = splice the node's
rewritten children where the phantom sits. The phantom is inert under bind (selectors
never fire on it), so inheritance is bind's own child-handling: a later bind erasing the
phantom's parent PROMOTES the phantom with its siblings, position preserved, by the same
k = 0 rule real children ride. The point transforms exactly as an attached child would —
which is the commuting condition associativity demands, and why the Kleisli composite in
the reference model is one line with no bookkeeping.

**Spellings.** `Return(v)` = v with the slot as its only child (root-pointed — the
tree-valued fragment unchanged); `Empty` = the slot alone (the sentinel-pointed empty —
k = 0 promotion falls out of the same splice, no case analysis); k ≥ 2 = the caller says
where, including between roots.

**Results** (`PointedSelectManyLawVerificationTests`, same reference-model method,
grounded against the shipped `Where` and `Select` first): left identity, right identity,
and **associativity — including the exact selectors of the pinned counterexample, three
different slot placements, and the empty-heavy promotion-inside-promotion corner — ALL
PASS** across the corpus. The pinned counterexample converges: both association orders
route the attachment through the phantom's promotion instead of one landing on the
erased root and the other falling past it.

**The slot-OPTIONAL system (same day): the Empty-graft fork DISSOLVES.** The
origin-epiphany conversation (2026-08-12) held that with slot semantics the whole local
trio {`Where`, `PruneBefore`, `PruneAfter`} are bind-image candidates, with "one bind
can't serve both Empty rules (vanish vs promote)" left for associativity to arbitrate.
The arbitration's answer is **both** — because the two Empty rules were never competing
for one spelling. They are two different pointed values: **Empty-with-slot** (the bare
slot) promotes — `Where`'s rule — and **Empty-without-slot** (no slot anywhere: the
children have nowhere to go and are dropped) vanishes — `PruneBefore`'s rule. A
slotless single leaf keeps the node and drops the descendants — `PruneAfter`'s rule.
Verified in the same suite: bind over {slotless-empty, Return} reproduces the real
`PruneBefore` byte-for-byte; bind over {slotless-leaf, Return} reproduces the real
`PruneAfter`; associativity holds across MIXED slot arities, including the sharpest
corner (a slotless expansion pruning the subtree the phantom sits in — both association
orders agree the outer children vanish). The fork was an artifact of the unpointed
carrier, exactly as the conserved miss was an artifact of the uncompleted one.

**Standing:** the semantics question is answered — root-graft substitution over
slot-carrying forest-valued selectors (arity {0, 1}: none = drop, one = attach) is a
lawful monad, with the 2026-07-04 tree-valued rules as its `Return`-side sugar, and
**`Select`, `Where`, `PruneBefore`, and `PruneAfter` all bind-derivable theorems** —
"maximize derivable reshapings," fulfilled maximally. Arity ≥ 2 stays out (duplication
breaks right identity; a broadcast graft would be a different, separately-named
citizen). OPEN for the decision-holder: the SURFACE — how the API spells a pointed
expansion. The phantom is a model device; `TValue` admits no reserved value (the
generic wall), so the product spelling needs a structural point — a pointed-forest
builder family, a slot-bearing expansion type, or bind restricted to a
`Return`/`Empty`/pointed vocabulary. Naming and shape are the next ruling.

---

## ADDENDUM III (2026-08-21) — the sequence lab: the fragment boundary, found

Before implementing the tree operator, the pointed bind was built for `IEnumerable` in
`Copse.Linq.Experimental` (`Expansion<T>` + `SelectMany` + `Compose`;
`PointedEnumerableSelectManyTests`) — the ruled detour: the simplest carrier as the
reasoning model. On a sequence the slot is visibly THE CONTINUATION — "the rest of the
stream goes here" — so the special values read as familiar operators, verified against
LINQ's own as the groundings: `Return` = Select, bare slot = Where's drop arm,
slotless-empty = TakeWhile's cut, slotless-leaf = take-until-inclusive, slot-last =
classic `SelectMany`. Kleisli composition is implemented AS the operator: the slot rides
the item stream as an inert element — the phantom mechanism with no bookkeeping,
executable.

**The lab's finding: AFTER-SLOT items break associativity on the flat carrier.** Pinned
counterexample (`Finding_AfterSlotItems_BreakAssociativity_TheCounterexample`):
left-associated, the intermediate is flat, so the second bind reads a postponed suffix
item as part of the CONTINUATION and nests around it (`y'L, PL, PR, y'R`); but any
Kleisli composite emits per-element blocks (`y'L, y'R, PL, PR`), and the block-order
argument shows NO composite can interleave one element's derived material inside
another's block. Structural, not an implementation choice. Mechanism: an after-slot item
means "a SIBLING following the continuation," and **flattening destroys
sibling-versus-descendant** — the exact information the second bind needs.

Consequences:

1. **The sequence's lawful territory is the suffix-free fragment** — prefix items plus an
   optional trailing continuation (affine prefix transformers; associativity is function
   composition). That fragment is exactly the five grounded operators. Notably the TAKES
   enter the monad here: on the chain carrier the walk and the drawing coincide, so
   truncation — a walk-floor citizen on trees (survey §2's litmus) — becomes
   bind-derivable.
2. **The tree carrier is NECESSARY for general slot placement, not gratuitous
   generality.** "Siblings after the slot" only exists where the carrier keeps structure;
   the tree's intermediate remembers what the sequence's flattening forgets, which is why
   Addendum II's mixed-placement associativity holds there and fails here.
3. **A cost note for the tree wrappers**: the lab's streaming shape (suffix-free = O(1)
   state, source abandoned at the first slotless expansion; after-slot content = exactly
   the held state) is the sequence shadow of the tree DFT wrapper's cost profile —
   slot-last drains expansions contiguously; mid-structure placement pays a paused inner
   per level.

**The streaming spelling (same day, Jason's design — the stack of enumerators).**
`DeferredExpansion<T>` + the second `SelectMany` overload
(`DeferredPointedEnumerableSelectManyTests`, 10 pins): prefix and suffix are lazy
sequences with the slot STRUCTURAL between them (so "at most one slot" stays a property
of the type — a tagged-item-stream spelling would carry the same laziness but demote the
law to a runtime check), and the operator keeps a stack of OPEN BRACKETS, each a live
suffix enumerator acquired when its expansion's slot is reached and drained after
everything the rest of the stream expands to. Pinned item-for-item equivalent to the
value spelling across suffix-free and suffix-carrying selectors (so the laws and the
after-slot finding transfer), plus what only laziness can show: infinite prefixes and
suffixes stream; the value spelling enumerates at construction, the deferred one at bind
time and exactly once; selector work runs in stream order (element 1's prefix before
element 2's selector is called; suffixes last, in reverse). The bracket discipline is
pinned three ways — every acquired suffix enumerator is disposed on full drain, on early
termination, and on an exception mid-stream — and a bracket opens only when the stream
advances PAST its prefix (lazier than "when the selector returns"). This is the rehearsal
of the tree DFT wrapper's control flow: replace "enumerator" with "treenumerator" and the
stack of brackets is the stack of paused inner treenumerators, one per path level, with
the same disposal obligations. The designation direction is the law's whole content: the
expansion being replaced declares where its continuation goes (or declines); the filler
never chooses — "pin after the first 1 it sees" would be attachment by value, the
sentinel trap, and the case where the value never appears is exactly the undefined
attachment the structural slot makes unspellable.

**The unified vocabulary (same day — supersedes the two-spelling paragraph above; the
value and two-sequence spellings are retired into it).** One canonical form: the TAGGED
STREAM (`SlotOrValue<T>` items, at most one slot), mintable only through factories, so
"at most one slot" is a property of the type with no runtime policing reachable from
outside. Two factory families cover two kinds of placement: **a-priori** — the slot's
position is known before the expansion produces anything (`Slotted(before, after)` and its
sugar `Return`/`Promote`/`Drop`/`Leaf`; slot-first is `Slotted(empty, items)` and the
continuation runs without the expansion being touched) — and **discovered** — the position
depends on the items (`SlotAfter(items, predicate, ifNoMatch)`, value and indexed forms,
whose `IfNoMatch.SlotAtEnd` policy also spells classic flatten, and `Slotless`). One
driver underneath: the stack of PAUSED enumerators (Jason's design) — an expansion's
stream is driven until its slot, its items emitted as pulled, then paused and pushed;
brackets close in reverse on unwind; every paused enumerator disposed on drain, early
termination, and exception (pinned with a hand-rolled tracker, since iterator bodies
cannot witness a never-started enumerator). Kleisli composition is implemented AS the
operator over tagged streams, lazy end to end.

**The after-only ruling.** Discovered placement is `SlotAfter` only. A content-discovered
"before" would pull an item ahead of its emission (the item must be computed to be tested,
then held across the continuation) — side effects in a different order from enumeration,
which Jason did not love. Dropping it yields an OPERATOR CONTRACT, pinned: an expansion is
never pulled ahead of its emission; pull order, emission order, and effect order
coincide. Positions lost: none that are lookahead-free (before-first = a-priori; known
index = the indexed predicate on the preceding item; after-last = the no-match policy).
The one unspellable placement — before an item, decided by that item — is a one-item
lookahead by nature. Jason's caveat, recorded: "it's not hard to imagine a scenario where
you don't know that you want to attach before something until you've seen it" — safer,
not certainly better; revisit if a consumer demands it. The timing-vs-position
distinction is the residue that keeps both factory families: the predicate form is
position-complete; only the a-priori form places a slot without touching the expansion.

**The tree mapping.** Of the 2×2 placements relative to a matched node — {sibling,
child} × {before/first, after/last} — three are lookahead-free on trees
(`SlotUnderFirst`, `SlotUnderLast` [Data.Tree order], `SlotAfter` as a sibling — "between
roots" spelled), because a parent is pulled before its children; the lone lookahead
placement is the discovered `SlotBefore` as a sibling — the sequence's `Before`, same
case, same reason — whose lookahead-free version is a-priori slot-first at the forest
level. The candidate tree surface is therefore: plain `ITreenumerable` expansions plus
placement factories producing a tagged NODE stream, one wrapper driving it — no phantom
node type, no builder DSL, no reserved value.

**The distinction the contract protects (Jason's refinement).** Two kinds of "effects out
of step with enumeration": PHASE LAG — effects run early but in the same order as the
emissions that follow (every capture, `GetLevels`, `GetBranches`, per-root leaffix, DFT
`TakeLastTrees`: monotone shifts; cause order is always inferable from emission order)
— and INVERSION — an item's effect runs, other items are emitted, then the item appears,
so emission order contradicts cause order ("appears to violate causality"). The house
tolerates phase lag when disclosed; the library has ZERO single-source inversions in
streaming operators (the merge ops interleave across two independent sources, bounded at
one head, inherent to merging). A discovered `SlotBefore` would be the first — and a
quiet one. The never-pulled-ahead contract is stronger than the inversion-freedom it
exists to keep, but inversion-freedom is the invariant; a before-after-seeing placement,
if ever receipted, ships as a DECLARED inversion, never a default.

---

## ADDENDUM IV (2026-08-21) — the operator, shipped

`SelectMany` is built, both colors (async source, generated sync twin), with the sequence
lab as its rehearsal. Surface: `Expansion<TResult>` = a forest (`IDepthFirstTreenumerable`,
or none) + a `SlotPlacement`; factories `Return`/`Promote`/`Drop`/`Leaf` (the four
theorems) and `Of(forest, placement)`; `default` = `Drop`. Overloads: the depth-first
narrow form streams; the composite form's breadth-first dimension is a DOCUMENTED CAPTURE
(each BFT acquisition captures the DFT result, preorder, and replays it).

**The placements shipped are the lookahead-free set over a visit stream**:
`AfterRoots` (children as trailing roots beside the expansion's — promotion on an empty
forest), `UnderLastRoot` (under the last root, after its own children — the Data.Tree
order, `Return`'s rule), `None` (slotless — children never pulled). Slot-before-roots and
under-first-root are DEFERRED: they owe emission after the source node's subtree ends, and
a depth-first visit stream cannot announce a subtree's end without reading one event past
it — the tree's version of the lab's after-slot items, and a declared phase lag if ever
shipped (the operator's contract forbids it silently). Discovered placements (predicate
over the expansion's nodes) likewise wait for a receipt.

**The depth-first machine** (`SelectManyDepthFirstTreenumerator`): a FRAME per open
source node — its expansion's treenumerator, the slot its forest's roots go to, the slot
its children's replacements go to — is the lab's bracket stack one carrier up. A frame
drains its forest (roots re-indexed into the slot, deeper nodes offset by the slot's
depth), then splices the source subtree one event at a time; source nodes never appear,
their visits are structure (frames pop on the arrival of a shallower source event). The
one held thing is a forest ROOT's visit, released on the next forest event (a phase lag of
one event in the same order — a visit stream never marks a root's last visit, and the last
root's last visit is where `UnderLastRoot` opens its slot). Visits of a slot-owning node
are MANUFACTURED, one after each root emitted into its slot, continuing its own visit
count, so the output obeys the visit protocol whatever forest the children came from; a
pending queue serves manufactured and released emissions one per pull.

**Contract, pinned**: nothing is pulled ahead of its emission (every source schedule is
immediately followed by its replacement), and a dropped subtree is never pulled
(`SkipDescendants` at the source). Consumer strategies, v1: forwarded to the expansion
cursor that scheduled the node; `SkipDescendants` on a slot-bearing root also skips the
splice; the strategy conformance matrix is deferred — TraverseAll conformance is pinned.

**Verification**: `PointedBindReferenceModel` (the phantom-slot model, extracted as the
shared oracle) + `SelectManyOperatorTests` — every corpus tree × four selector sets,
depth-first AND breadth-first visit streams lockstepped against the flat family over the
expected forest (positions and visit counts included), stacked binds, the 2026-08-12
counterexample lawful on the real operator, the four derived operators byte-for-byte, the
two contract pins; `AsyncSelectManyTests` for the async color. Green first run.

**Queued**: the deferred placements (declared lookahead), discovered placements, the
strategy matrix, narrow BFT (a true streaming BFT wrapper — Where-BFT-class work),
`Subtrees` as join's coherence oracle (graft ↔ Subtrees), the `ExpandNodes`/`Graft`
experimental reconciliation, and the composition story (bind as the front door to the
local collapse lattice, survey §12's open item).

**Benchmarks (same day; `SelectMany` class, Streaming family, consulted before adding).**
Theorem rows on Mega Triangle DFT, same-run, local EPYC 7763 ShortRun: the first cut
measured bind at 3.8x `Select` / 2.2x `Where` with ~300 B allocated per node -- the
instrument naming its own target: `Return` minted a delegating treenumerable, a closure,
and a one-node treenumerator per node. **The structural single-value fast path**
(`Return`/`Leaf` carry the value in the expansion; the machine emits the schedule, holds
the visit, no treenumerator -- timing identical to the forest path, strategies honored on
the next pull) took them to **2.7x `Select` (132 vs 49 ms), 2.0x `Where` (114 vs 57 ms),
1.7x `PruneBefore` (131 vs 79 ms)** at ~140 B/node (`Dft_Triangle_PruneBefore`'s first
predicate pruned the root -- a 320 ns non-instrument -- fixed to `n % 7 == 6`). General
rows: two-root forest under the last root at every node -- Triangle 262 ms / Chain 513 ms
(Gen2 promotions: a million live frames) / Binary 379 ms, ~230-250 MB; the composite
overload's breadth-first capture 400 ms vs the DFT's 262 on the same work.

**The remaining allocation is the frame**: a `Frame` object per open source node plus a
`Slot` object per `UnderLastRoot` expansion -- ~140 B/node. The next distill is the house
pattern: frames and slots as structs in a `RefSemiDeque` (slots addressed by frame index
rather than shared by reference), which should take the theorem rows near the allocation
floor of the machines they reproduce. Not done in this pass -- it reshapes the machine's
state and wants its own review.
