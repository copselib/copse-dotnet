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

*Decided 2026-07-04. History: root-graft-after was Jason's original instinct, held for
years; the promotion alternative arose from Where's forest behavior a few months back
and was eliminated here by the right-identity counterexample; before/after was settled
by streaming mechanics + precedent.*
