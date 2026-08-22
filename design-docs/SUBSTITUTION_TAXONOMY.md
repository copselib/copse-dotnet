# The substitution taxonomy — node replacement across both families

> **Status: RATIFIED IN DISCUSSION 2026-08-08 (the algebra session); first DAG rows built
> same day (`ReplaceNodes`, `ExpandNodesWhere`, `Where`).** Companion to
> [SELECTMANY_DESIGN.md](SELECTMANY_DESIGN.md) (the tree bind's semantics decision, which
> this document generalizes) and the EDGE REPLACEMENT section of
> [DAG_CONTRACT_DESIGN.md](DAG_CONTRACT_DESIGN.md) (the edge-channel row, built first).
> The session's finding, stated up front: **every structural operator in both families is
> one schema — replace each node with a shape, wire the incident edges by a rule, choose
> an Empty convention — and an operator is lawful exactly when its rule is local.**

## The schema

Three coordinates classify every substitution operator:

1. **Replacement shape** — what one node becomes: one node (`Select`), Return-or-Empty
   (the filters), a chain, a flat split, an arbitrary subgraph.
2. **Wiring rule** — how the node's incident edges reconnect to the replacement:
   in-edges to its sources; out-edges from its last root / its sinks / its every node.
3. **Empty convention** — what "replaced by nothing" means: cascade (prune), contraction
   (promote/bypass), or liveness (the DAG's sharing-aware cascade).

## The locality thesis

The monad laws are a locality test and nothing else. Associativity says: running `f`
over the whole structure and then `g` over the result equals running the fused selector
`x ⇒ f(x) » g` in one pass — i.e. **the rewrite can be performed one node at a time, in
any grouping, and the pieces agree.** Every lawfulness failure catalogued below has the
same anatomy: the correct answer depended on a *global* fact (a surviving leaf set, a
composite path structure) that no per-node rule can see. Every lawful rule has the same
anatomy too: each node's fate is decidable at that node.

## The tree placement matrix

For a forest-valued selector `f`, where do a node's original children re-hang on its
expansion `f(x)`? (Multiplicative = a copy per attachment point; the copies are the
tree's rendering of DAG sharing — see the unfolding section.)

| Attachment | Lawful? | Empty convention | Boundary filter (`{Return, Empty}` restriction) |
|---|---|---|---|
| Last root (linear) | ✓ (proof sketch below) | promotion | `Where` — **the chosen tree `SelectMany`** (SELECTMANY_DESIGN.md) |
| Every root | ✓ | deletion | `PruneBefore` |
| Every leaf | only with non-Empty selectors | **neither convention survives** | — |
| Every node | ✓, Empty included | deletion | `PruneBefore` |

**The two counterexamples** (both: source `a(b)`, chain expansions, `g` = Return except
where noted). They pull in opposite directions, so no Empty convention fixes every-leaf:

- *Deletion fails on single-leaf expansions.* `f(a) = p(q)`, `g(q) = Empty`. Two-step:
  `b` was grafted under `q`, dies with it → `p`. Fused: `q` dies before grafting, `b`
  attaches to the survivor → `p(b)`.
- *Promotion fails on multi-leaf expansions.* `f(a) = p(q, r)`, `g(q) = Empty`.
  Two-step: `p(q(b), r(b))`, promotion rescues `q`'s copy → `p(b, r(b))`. Fused: the
  composite expansion is `p(r)`, one leaf, one copy → `p(r(b))`.

The structural reason no rule can work: after one pass, grafted copies are
indistinguishable from expansion structure, and the correct redistribution depends on
the expansion's surviving leaf set — non-local. (The principled fix — marked graft
points — is the free monad's move and a different type; not this library's road.)

**The proof identity.** Writing `A ⊛ F` for "attach `F` under every node of `A`, after
each node's own children," associativity of the every-node placement reduces to

    (B ⊛ U) ⊛ V  =  B ⊛ ((U ⊛ V) ++ V)

— the second pass lands `V` inside the `U`-copies as well as beside them. The same
induction style discharges the last-root case (the `⊕`-append identity), settling
SELECTMANY_DESIGN.md's "asserted, not proven" k ≥ 2 associativity. Both identities are
the law batteries' content.

**Lawful fragments of the unlawful row.** Two selector classes are closed under fusion
and lawful *with* Empty even at every-leaf placement: flat splits (node → k values;
Empty = prune) and chains (node → path; Empty = contraction). Their mixtures compose
out of both fragments in one fusion step — that is where the dragons live, and why the
general tree operator, if ever built, disclaims Empty rather than picking a poison.

**Caveats common to every row**: laws quantify over *value*-dependent selectors
(position/ordinal-reading selectors break associativity in every placement — LINQ's
indexed overloads sit outside the query pattern for the same reason), and multiplicative
implementations must replay subtrees from a capture, not by re-running selectors (purity
becomes load-bearing, not advisory).

## The DAG reframing: trees observe these operators through the unfolding

In a DAG, when a node divides, **only edges multiply — never subgraphs**, because
sharing is representable: `a → a0, a1` gives `b` a second in-edge, not a second copy.
The tree operators' exponential duplication is the price of *unfolding* sharing — copies
per root-path — and the tree placement matrix is the unfolding of a DAG wiring menu:

| Tree placement | DAG wiring (out-edges reconnect from…) |
|---|---|
| every root | the replacement's sources |
| every leaf | the replacement's sinks (interposition — the transverse cell division) |
| every node | every replacement node (the longitudinal cell division: each copy keeps an edge to each neighbor) |

The cell-division invariant — *edges divide with the cell* — is the locality thesis
stated physically: every copy owning its own edge to each neighbor is exactly what makes
deletion local, which is what makes the every-node row lawful with Empty. The DAG does
NOT rescue the sinks row (the tree counterexamples are its unfoldings); it makes the
lawful rows *linear* instead of exponential.

## The DAG rows

| Operator | Shape | Wiring | Empty | Status |
|---|---|---|---|---|
| `Select` | one node (value) | unchanged | — | built (streaming) |
| `PruneBefore` / `PruneAfter` / `PruneEdges` | Return-or-nothing | severs | liveness | built (streaming) |
| `ReplaceEdges` / `ExpandEdgesWhere` | edge → path (fixed endpoints) | interposition | `Drop` + liveness | built 2026-08-07 (capture) |
| **`ReplaceNodes`** / **`ExpandNodesWhere`** | node → subgraph | **in: sources; out: every node** | `Drop` + liveness | built 2026-08-08 (capture) |
| **`Where`** | Return-or-bypass | vertex bypass (in×out, composed payloads) | — (bypass, never removal of kept nodes) | built 2026-08-08 (capture) |
| true contraction (merge endpoints) | — | — | — | **EXCLUDED BY PRINCIPLE**: merging requires value identity/combination; recorded so nobody re-derives it |

`ReplaceNodes` decisions, recorded:

- **Wiring**: in-edges fan to the replacement's sources; out-edges fan from every
  replacement node — the lawful multiplicative pair. Payloads duplicate across a fan
  (payload semantics are caller algebra — the `PruneEdges` constraint caveat's posture).
- **Empty**: `Drop` deletes the node; downstream survival follows the family's one
  liveness rule. A dead node's selector is never consulted (the `ReplaceEdges` pin).
- **Seat preservation**: a single-node replacement (`Keep`) occupies the original's seat
  (its `SourceOrdinal` carries; value rewrite is `Select`'s content); multi-node
  replacements are wholly born-here (−1) — fresh identity, cycle-safe by construction.
- **`SelectMany` BUILT (2026-08-22; DAG_CONTRACT_DESIGN.md, THE BIND).** The tension
  dissolved the way the tree's did: neither root-graft nor every-node is the bind -- the
  POINTED form is, with a structural slot (attachments from fragment nodes or from outside,
  payload-optional). Every-node wiring stays as `ReplaceNodes`, the broadcast citizen.

`Where` decisions, recorded (resolving DAG_CONTRACT_DESIGN.md open question 5):

- **Named `Where`** — it is the family homolog (the carry table's own mapping: "`Where`
  child promotion → contraction with caller edge-composition"). `Contract` was rejected
  for accuracy (graph theory's contraction merges endpoints — the sources/sinks ruling's
  standard); graph vocabulary for what this does is *vertex bypass / smoothing*.
- **LINQ polarity** (true = keep), per the family's 2026-07-06 unification.
- **Bypass, not removal**: kept nodes never die. A kept node whose every in-path ran
  through bypassed sources becomes a source — the tree's filtered-root promotion,
  dag-side. Payloads compose along each bypassed through-path via the required
  `(inEdge, outEdge) ⇒ edge` combiner — payload composition is domain semantics
  (60% × 50% = 30%); parallel result edges are permitted and expected.
- **Cost class, stated honestly**: a bypassed node manufactures in-degree × out-degree
  edges; a bypassed *region* manufactures one edge per through-path. The output is big
  because the answer is big — same honesty as the multiplicative rows.

## The streaming ledger (why the new rows capture, and what would unlock them)

- **`ReplaceNodes`**: same blocker as `ReplaceEdges` — synthesized nodes need ordinals a
  wrapper cannot know are free. Unlocked by the logged reserved-ordinal-range amendment.
- **`Where`**: a NEW blocker, identified this sitting — **dispatch contiguity**. A
  manufactured `p → c` discovery is learned at the bypassed node's entry, long after
  `p`'s dispatch block closed; emitting it there violates the contiguity clause that
  `DagRelationshipTracker` enforces (it throws) and `DagBuffer.From` exploits. Streaming
  `Where` therefore waits on a contiguity amendment (licensing late attributed
  discoveries or re-attributing manufactured edges), bundled into the amendment sitting
  alongside the ordinal range and the streaming-sources clause. No ordinal blocker:
  bypass synthesizes edges only.

## The tree family's roadmap, ordered by this document

1. Tree `SelectMany` per SELECTMANY_DESIGN.md — unchanged; its k ≥ 2 associativity now
   has a proof sketch to encode as property tests.
2. Tree `ExpandNode` (every-leaf, non-Empty selectors, DFT-only, composite-only) — **on
   demand only**: it is the exponential unfolding of `ReplaceNodes`, and workloads that
   can live DAG-side should. Its spec is fully written above the day a workload asks.
3. `SplitNodes` / `ExpandChain` sugars — same posture; the edge channel already covers
   chains dag-side.
