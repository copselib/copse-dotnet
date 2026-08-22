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

**BFT streaming is possible, not blocked -- deferred on demand, route recorded (ruled
2026-08-21).** The output's level order is well defined and the reference model already
computes it (the oracle is free). What makes it hard is three difficulties stacked, one
of them solved: (1) promotion's unbounded pull-ahead -- `AfterRoots` puts a source node's
children at its own roots' depth, so a chain of promotes lands a deep node on level 0 and
a level cannot close until no pending promoted node can still contribute to it -- is
exactly Where-BFT's problem, solved there by the skipped-ancestor prefix carry
(`WhereBreadthFirstPath`, O(width)), to be INHERITED, not re-derived; (2) forests with
depth -- a replacement spans levels L..L+k, so its nodes at L+j merge in positional order
with other forests' nodes and with source descendants' roots landing there: a live
breadth-first cursor per source node whose forest is still open (O(width × forest depth)
cursors where Where holds O(width) structs), which requires BFT-enumerable forests -- the
composite overload's expansion would carry a composite forest (`Expansion.Forest` is
depth-first-typed today); (3) the visit protocol over mixed parents -- in BFT, parent
visits interleave with the next level's schedulings, and a level-(L+1) node's parent may
be a forest node, a forest's last root (its own children first, then the splice), or a
slot-owner from a different source node: Where-BFT's deferred/manufactured/suppressed
visits, generalized. Sizing: the hardest single machine in the library -- Where-BFT along
two axes at once; an arc with its own benchmark consult, not a pass. Why it waits: the
workloads that most want it are theorem-shaped, and those already stream breadth-first as
`Select`/`Where`/`PruneBefore`/`PruneAfter` -- the composition story gets them for free,
both colors, no new machinery; the capture is correct and benches at ~1.6x the DFT (348 vs
220 ms), a tax, not a cliff. Build it when a consumer arrives with general forests AND a
BFT requirement AND a tree too big to materialize, all three; then: composite forests in
the composite overload's expansion, the machine grown from `WhereBreadthFirstPath`, and a
level-order conformance suite from the existing oracle before a line of the merge.

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
strategy matrix, the streaming BFT wrapper (possible, deferred on demand -- route above),
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

**The struct-frame distill (same day, the pass after the commit).** The ~140 B/node left was
two heap objects per node: a `Frame` per open source node and a `Slot` per `UnderLastRoot`
expansion, shared by reference down the frame chain. The house pattern replaced both:
frames are structs in a `RefSemiDeque`, and slots are a SECOND stack (the root slot at its
bottom) rather than index-addressed -- a frame that realizes a slot pushes one and pops it
when it pops, so the top slot is always where the top frame's children go, and the slot
beneath (when the top frame owns the top slot) is where its own roots went. Two fields fell
out as derivable: `SourceDepth` (the frame stack IS the open source path, count = depth + 1)
and `Emission.ScheduledBy` (consumer strategies always bind to the top frame, because nothing
is pulled between a scheduling and the next pull). Shape constraint: by-reference locals are
illegal in async methods (CS8177), so the async methods are thin seams around the awaits and
every mutation is a synchronous helper taking refs into the deques -- which is also the
cleaner codegen story. Same-run ShortRun, local EPYC: **2.3x `Select` (114 vs 49 ms), 1.7x
`Where` (104 vs 62 ms), 1.4x `PruneBefore` (117 vs 83 ms)**, theorem rows at **220 KB total,
zero Gen0** (the shipped neighbours: 58-115 KB; the rest is deque partitions). Forest rows
Triangle 220 / Chain 240 (was 513: the Gen2 promotions of a million frame objects are gone)
/ Binary 325 ms at ~82 MB (was ~230: what remains is a real treenumerator per node); the BFT
capture 348. The allocation floor is reached; the time left over a collapsed operator is the
general machine's per-event work (pending queue, two stacks), the baseline the composition
dispatch is measured against.

## ADDENDUM V (2026-08-21) — the composition story: the quartet is bind's sub-monoid

> **Status: IN PROGRESS on `feature/bind-composition`.** The account below is settled and
> pinned (`QuartetKleisliClosureTests`); the doors are the work.

**The closure fact.** The four special expansions -- `Return`, `Promote`, `Drop`, `Leaf`,
the library's reshapings as theorems -- are closed under Kleisli composition. Bind an
expansion of one kind through a selector of another and the composite is again one of the
four:

| `f(x)` \ `g(a)` | `Return(b)` | `Promote` | `Drop` | `Leaf(b)` |
|---|---|---|---|---|
| `Return(a)` | `Return(b)` | `Promote` | `Drop` | `Leaf(b)` |
| `Leaf(a)` | `Leaf(b)` | `Drop` | `Drop` | `Leaf(b)` |
| `Promote` | `Promote` | `Promote` | `Promote` | `Promote` |
| `Drop` | `Drop` | `Drop` | `Drop` | `Drop` |

The phantom's inheritance does the work: `Leaf` then `Promote` is `Drop` because `Leaf`
already dropped the slot the promotion would have handed the children to; `Promote` and
`Drop` absorb because nothing of theirs reaches `g`. `Return` is the unit and the table is
associative -- a monoid, the quartet as a sub-monoid of the Kleisli category. Pinned on the
real operator: all sixteen cases on the law-test forest set, the pointwise form (selectors
choosing a kind per node compose by the table per node), and the monoid laws on the table.

**This is the composition design's result monad, recognized.** OPERATOR_COMPOSITION_DESIGN.md's
carrier is `Result<T> = (value, strategies)`, a Writer over the strategy monoid with
`Rejected ⇔ SkipNode ∈ strategies`. That carrier IS the quartet, encoded:

| expansion | result |
|---|---|
| `Return(v)` | `(v, ∅)` |
| `Leaf(v)` | `(v, SkipDescendants)` |
| `Promote` | `(_, SkipNode)` |
| `Drop` | `(_, SkipNodeAndDescendants)` |

and its composition law -- `Accepted(v, s₁)` then `Accepted(v₂, s₂)` is `Accepted(v₂, s₁ ∪ s₂)`,
a rejection short-circuits -- is the table above, read off the bit-union
(`SkipDescendants ∪ SkipNode = SkipNodeAndDescendants` is the `Leaf`-then-`Promote` cell).
So "any chain of Select/Where/PruneBefore/PruneAfter collapses to one struct-composed arrow"
was never an operator-by-operator discovery: the collapse lattice is bind restricted to the
quartet, and `ComposedResultSelector` is Kleisli composition on the sub-monoid. Two design
docs, one algebra.

**What the doors buy, and what they do not.** No dispatch can see inside a delegate, so
`SelectMany(x => Return(f(x)))` always pays the general machine over `Select(f)` -- the
theorem rows' 2.3x / 1.7x / 1.4x are the honest price of writing bind where a collapsed
operator would do, and they stay on the dashboard as exactly that. What composition
recovers is bind participating in a chain at zero added layers:

1. **The left door** -- operators BEFORE bind fold into its selector, pointwise, because a
   quartet-valued left factor composes into a general selector with no machinery:
   `Where(p).SelectMany(f)` is `SelectMany(x => p(x) ? f(x) : Promote())`; `Select(g)` is
   `f ∘ g`; `PruneBefore` is `Drop`; `PruneAfter(q)` is `f(x)` with its placement overridden
   to `None` (the `Leaf` row of the table, for a general `g`: `(F, P)` becomes `(F, None)`).
   Generally: the collapsed chain's arrow gives `(v, s)` per source context; `SkipNode ∈ s`
   yields `Promote` or (with `SkipDescendants`) `Drop`; otherwise `f(v)`, slotless when
   `SkipDescendants ∈ s`. The chain surrenders its raw inner and its struct arrow (the
   rootfix door's shape, `IAsyncProjectionSource`, generalized to the result arrow) and ONE
   bind machine runs over the raw inner with the folded selector. Correct by the theorems
   plus associativity. The bind's selector is value-only, so it joins any chain under the
   join rule; the chain's positional legs keep reading the source context they always read.
2. **`Select` on the right** -- re-plant the projection at the bind's emission (the
   projection-citizen pattern): one selector call per emission, no wrapper layer.
3. **`Where`/prune on the right** -- by associativity, bind each expansion forest through
   the arrow; but that decides the PLACEMENT at drain time (a rejected last root promotes
   its phantom into root position: `UnderLastRoot` becomes `AfterRoots`), which is the
   discovered-placements item in another hat. Waits for it. The alternative -- Where
   machinery over bind's output in one driver -- removes a layer, not work.

Expectation going in (Jason's, shared): none of this beats the hand-built drivers when the
user writes `Select`/`Where`; those stay the fast path. The measurement is whether bind can
sit in a chain without adding a layer.

**The left door, built (same day).** Three surrender parts and one recursive consumer:

- `IAsyncResultSource<TResult>` / `IAsyncResultConsumer<TResult, TOut>` -- the rejecting twin of
  the projection door: `Consume<TInner, TArrow>(inner, arrow)`, the arrow a struct leg. Implemented
  as explicit partial parts (outside the CompositeToNarrow fan-out, composite-width, internal) by
  the collapsed chain (`AsyncSelectWhereTreenumerable`: its `_Source` and its nested struct arrow),
  the prune-after light wrapper (`AsyncPruneAfterTreenumerable`: its predicate as the
  `PruneAfterResultSelector` it already is -- the lattice keeps prune-after as a layer because
  joining would demote its representation, but a consumer that ENDS the chain has no representation
  to demote), and the middle tier (`AsyncSelectPruneAfterTreenumerable`: its delegate-bound
  in-tier arrow as the `FuncResultSelector` leaf it already rides under a splice). Plain `Select`
  surrenders through the existing projection door.
- The bind driver is generic over a struct `IAsyncExpansionSelector` (context-shaped): the bare
  leg `AsyncFuncExpansionSelector` (the user's selector, value-only), or
  `AsyncFoldedExpansionSelector<TInner, TMid, TResult, TArrow>` -- the table lookup above the
  user's selector. `AsyncExpansion.WithoutSlot()` is the Leaf row for a general expansion.
- The consumer RECURSES: the first hop takes the source's arrow; every later hop composes the newly
  surrendered arrow INSIDE the one carried so far (`ComposedResultSelector` -- the lattice's own
  Kleisli composition, nested in the type) and asks the surrendered inner whether it can
  surrender too. So any stack of chain / prune-after layer / projection wrapper folds down to the
  raw source and ONE bind machine. The composite overload returns a named
  `AsyncSelectManyTreenumerable<TSource, TResult, TExpansionSelector>` so the seam pins can read
  the folded leg off the type.

**Pinned (`SelectManyLeftDoorTests`, twelve chain shapes × eight trees)**: the door equals the
stacked spelling (the same chain behind an opaque `Tree.Create`, which cannot surrender)
byte-for-byte in both dimensions, under a general selector exercising forests, every placement
and the quartet; the door is TAKEN for every shape (the type carries `FoldedExpansionSelector`,
the bare source carries `FuncExpansionSelector`); and the fold changes no effect order.

**A finding from the effect pin.** The middle-tier passthrough driver re-evaluates its pure legs
(projection, prune-after predicate) on EVERY emission event of a node, while the fold evaluates
each leg once per node at its scheduling -- as the lattice's Where driver does. The door's effect
log is therefore the stacked log's first-occurrence sequence: same effects, same order, each once.
Fewer calls, never reordered; the pin states exactly that.
