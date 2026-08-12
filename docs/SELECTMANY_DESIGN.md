# SelectMany (bind) — semantics decision

> **Status: SEMANTICS DECIDED 2026-07-04 — implementation deferred.** Ends a ~3-year
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

The categorical audit's phase 3 (docs/CATEGORY_THEORY_SURVEY.md §6) ran the monad-law
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
