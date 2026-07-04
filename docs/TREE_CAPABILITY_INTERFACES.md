# Tree Capability Interfaces (design note)

> **Status: DESIGN ONLY — not implemented.** Captures a direction discussed 2026-06-23.
> This is a large undertaking to do correctly; it is deferred until the current
> tree-snapshot / serialization work (`PreorderTree`, `Materialize`, flat `Deserialize`)
> is shored up. Nothing here is committed to in code yet.
>
> **See also:** [TRAVERSAL_DIMENSION_SPLIT.md](TRAVERSAL_DIMENSION_SPLIT.md) (2026-07-04)
> — a new, orthogonal axis discovered during the serialization redesign: splitting
> `ITreenumerable` into `IDepthFirstTreenumerable` / `IBreadthFirstTreenumerable`
> (compile-time traversal-dimension affordances, sitting *underneath* this doc's
> node-navigation lattice). Division of labor: **types for "impossible without
> escalation"; this doc's runtime probing for "cheaper when rich."**

## Motivation

`ITreenumerable<TNode>` is a *stream from the roots*: you can traverse the whole tree,
but you cannot ask "give me the children of *this* node" without re-traversing. That is
enough for `map` / `bind` / `prune` / traversal transforms, but it blocks a class of
operations that need richer access — most pressingly **true (incremental) memoization**.

.NET LINQ solves the analogous problem by *probing* for richer capabilities at runtime:
`Enumerable.Count()` shortcuts to `ICollection.Count`, `ElementAt` / `Last` / `Reverse`
shortcut via `IList` / `IReadOnlyList`, and everything falls back to streaming when the
capability isn't present. We want the same shape for trees: a small set of **optional
capability interfaces** above `ITreenumerable<TNode>`, with operations that probe for them
and shortcut, always keeping a correct streaming fallback.

## It is a lattice, not a chain

The capabilities split along **three orthogonal axes**, so they will *not* collapse into a
single `IList : ICollection : IEnumerable` spine. A tree can be child-indexable but not
parent-navigable, or parent-navigable but stream-only on its children. (.NET already lives
with this — `IReadOnlyList` runs parallel to `IList`.)

- **navigation direction** — parent (up), sibling (lateral), child (down)
- **child-access richness** — visitable (stream/push) → indexable (count + random access)
- **mutability** — a separate axis entirely

So: design these as **small, composable capability interfaces**, and let each concrete tree
implement whatever subset it actually supports. Do **not** build a deep inheritance spine.

## The capabilities (preliminary)

| Interface (working name)     | Capability                                | Enables                                                  |
| ---------------------------- | ----------------------------------------- | ------------------------------------------------------- |
| `ITreenumerable<TNode>`      | traversal only (the stream)               | map, bind, prune, traversal transforms                  |
| `IParentNavigableTree<TNode>`| child can find its parent                 | upward navigation (ancestors, path-to-root, LCA), stack reduction in DFS |
| `ISiblingNavigableTree<TNode>`| first-child / next-sibling navigation    | forward iteration without a child collection; with parent → stackless DFS |
| `IChildVisitableTree<TNode>` | realize a node's immediate children       | **incremental memoization**, child filtering, local reordering, scheduling transforms |
| `IChildIndexableTree<TNode>` | count / index a node's immediate children | reverse, random child access, indexed transforms        |
| `IMutableChildTree<TNode>`   | modify child structure (later)            | in-place rotations, moves, replacement                  |

### Refinements from discussion

- **Stackless DFS needs sibling *and* parent.** first-child / next-sibling gives forward +
  lateral movement, but backtracking *up* needs parent (or a stack). `ISiblingNavigableTree`
  alone → forward iteration; `ISiblingNavigableTree` + `IParentNavigableTree` → genuinely
  stackless DFS.
- **`IChildVisitableTree` is the keystone for memoization.** It is the "random child access
  by node" capability: given a node the tree just presented, realize its immediate children.
  That is what lets incremental memoization fill a cache *hole* in O(children) instead of
  re-traversing from the root — see below.

## The big connection: `Materialize` / `Memoize` are the *upgrade* operation

`ToList()` upgrades `IEnumerable<T>` → `IList<T>`. **`Materialize` / `Memoize` upgrade a
poor-capability stream tree into a rich-capability one.** `PreorderTree` (the flat snapshot
we just built) can implement most of the top rows — child-visitable (the subtree-size hop),
parent-navigable (add a parent-index array), even child-indexable (with child offsets).

So memoization is **not a separate feature from this hierarchy** — it is the *bridge from the
bottom of the lattice to the top*, and for **lazy / infinite** sources it is the only bridge.
That also resolves the `Memoize` design question cleanly:

> **`Memoize` is gated on a capability.** If the source can realize a node's children from
> that node (`IChildVisitableTree`), true incremental memoization is possible (fill holes in
> O(children), works on infinite trees, pay only for what you explore). If the source is
> stream-only, `Memoize` cannot be incremental — it should degrade to `Materialize` (finite
> only) or not be offered.

### Why this is the *third way* for memoization

Memoizing a *prunable* tree has a hole problem: a pruned traversal never realizes some
subtrees, so the cache has holes; a later, less-pruned traversal walks into them. The two
obvious handlings are both bad:

1. **Re-traverse the inner from the root to fill the hole** — recomputes the expensive thing
   you were memoizing, costs O(path) to even reach the hole, and forces a splice into the
   middle of the cached structure.
2. **Never prune the inner — always drive it `TraverseAll`** — no holes, but that is just
   `Materialize` in disguise (finite only, pays for the whole tree even when every consumer
   prunes).

The third way only exists if the source has `IChildVisitableTree`: a hole is just "haven't
realized this node's children yet," and you fill it by asking the node for its children —
**O(children), no re-traversal, no eager full build, infinite-tree friendly.** That is
exactly the class of trees where memoizing an infinite/expensive tree is the point
(Collatz, generators, per-node queries).

## Representation ↔ capability (reconnects to the pluggable layouts)

Which interfaces a structure can implement *cheaply* depends on its memory layout — the same
axis as the pluggable snapshot-layout idea, seen from the other side:

- **`PreorderTree`** (pre-order `values[]` + `subtreeSizes[]`): child *visiting* is cheap (the
  span hop); child *count / index* is O(degree) unless augmented with child offsets; parent
  needs an aux parent-index array.
- **LOUDS** (level-order succinct): O(1) child index, O(1) degree, O(1) parent via rank/select
  — it implements `IChildIndexableTree` + `IParentNavigableTree` *natively*.

So "pick a snapshot layout" and "which capability interfaces does it satisfy" are the **same
decision**, viewed from two sides.

## Push vs pull for child realization (open question)

The engine today speaks **pull**: it drives `IChildEnumerator<TNode>` via a
`Func<NodeContext<TNode>, TChildEnumerator>` that `Treenumerable<TValue,TNode,TChildEnumerator>`
*already holds internally* (just unexposed). So the child-realization capability effectively
exists — it only needs surfacing.

The tension: type-probing (`source is IChildVisitableTree<T>`) requires **erasing the concrete
`TChildEnumerator` struct type**. That forces a choice:

- **Pull** — `IChildEnumerator<TNode> GetChildEnumerator(NodeContext<TNode>)`. Simple, reuses
  the existing contract, but boxes the struct enumerator (loses the unboxed-struct perf the
  generic engine relies on).
- **Push** — `void VisitChildren(NodeContext<TNode>, <visitor>)`, the tree calling back per
  child. Can be **allocation-free** (struct/reused visitor), at the cost of inverted control
  flow (no external pacing / short-circuit unless the visitor signals stop).

**Leaning push** for the capability, precisely because probing erases the enumerator type:
push keeps it allocation-free where pull would box. (This is the reasoning behind the original
`IChildVisitableTree = "stream children into a visitor"` framing — it holds up.) For the
*memoize* use specifically, perf of realization is not the bottleneck (each node realizes
once, then reads from cache), so a boxed pull form would also be acceptable there; the push
choice matters more if the capability gets shared into hot paths.

## Design principles to carry forward

- **`Materialize` / `Memoize` are the upgrade op** — poor-capability stream → rich-capability
  structure. Treat memoization as part of this hierarchy, not separate from it.
- **Grow the lattice from real demand.** Add a capability interface when a concrete operation
  needs it (memoize → child-realization; reverse / indexed → child-indexable; ancestors / LCA
  → parent-navigable), the way .NET added `IReadOnlyList` years later. Do not pour the whole
  hierarchy up front.
- **The discipline tax.** Every shortcut becomes `if (source is ICapability<T> x) { fast }
  else { fallback }`, and the fallback must stay correct forever — the rich path is pure
  optimization, never the only path.
- **No node equality.** A memoization cache must key on **path/position**, not node value
  (the library never compares nodes). That means the memo has to be engine-aware (track the
  path as it descends), not a simple factory wrapper — part of why it is a large undertaking.

## Open questions

- **Push vs pull** for the child-realization capability (above).
- **Cache keying** for incremental memoize (path-keyed trie; engine-aware path tracking).
- **Where these interfaces live** — ties into the unresolved `Copse.Core` /
  `Copse` / `Copse.Linq` boundary (the project-split discussion). Capability interfaces
  are contracts → probably `Core`-adjacent; the snapshot structures that implement them are
  concrete → `Copse`.

---

*Discussed and recorded 2026-06-23. Revisit after the snapshot/serialization foundation is
solid. This is a deliberately large, careful piece of work — interfaces are hard to change
once published.*
