# Preorder Buffer — parked idea (streaming window / tee)

> **Status: PARKED 2026-06-28.** Recorded so it isn't re-derived; not scheduled. The
> `StructuralMerge` work takes priority. Revisit deliberately, with a chosen first consumer.

## The recurring confusion it dissolves

Three distinct things kept getting collapsed into "lazy materialize":

1. **Merge ENGINE** — must stream; needs **no buffer**. Lockstep co-traversal is already maximally
   lazy: the two paused inner treenumerators *are* the only buffer, bounded O(depth) (DFT) /
   O(width) (BFT). A buffer-based merge would have to emit the merged visit stream from buffered
   structure, which needs **subtree sizes** → needs subtrees *closed* → eager → collapses back into
   `Materialize`/ALT-3. Lockstep never asks for subtree size. **Merge stays lockstep.**
2. **Test ORACLE** — may be eager (it runs on finite fixtures; it is a test, not the library, so it
   does not violate the library's laziness contract).
3. **MEMOIZE** — the DECIDED design ([MEMOIZE_DESIGN.md](MEMOIZE_DESIGN.md)) is **retain-all**
   (append-only `PreorderTree`, finite-only) and that is what buys rewind-to-root + differently-pruned
   replays. The preorder buffer below is a *different capability*, not a replacement.

## The idea

A bounded, **head-ejecting** window over a treenumerator's preorder visit stream:

- A `RefSemiDeque` of `(value, depth)` slots indexed by a **monotonic preorder index**.
- **Build the tail on demand** (pull the source); **evict the head below the low-water mark** = the
  minimum preorder index any live cursor still needs.
- Working set = the **spread between cursors**, not the tree. Backing: `RefSemiDeque` is exactly
  right (O(1) add-tail / remove-front / indexed access).

## Where it wins, and the wall

- **Wins:** multiple *synchronized forward* readers (tee / fork), bounded lookahead, and **forward-DFS
  memoize over INFINITE sources** — the capability the retain-all memoize explicitly traded away.
- **Head ejection bounds the PAST cleanly.**
- **The wall is at the TAIL:** `SkipDescendants` / `SkipSiblings` jump *past* a subtree → need its
  subtree size → need it *closed* → **unbounded on an infinite subtree.** `SkipNode` and plain
  forward reads are fine (no hop). A **single forward consumer needs no buffer** (just drive the
  source); the buffer only earns its keep with **multiple readers / re-reads**, and only while they
  stay close together.
- **So it is not a drop-in for the decided memoize:** it trades rewind-before-the-window +
  divergent-pruning replay for **bounded memory + infinite-source support**.

## First-consumer options (deferred — the consumer fixes the eviction trigger + API)

- **Tee / fork** — one source → N synchronized forward cursors; evict below the slowest. Cleanest
  eviction, smallest test surface.
- **Streaming (infinite-capable) memoize** — forward-only, no rewind-before-window.
- **Generic peekable / bounded-lookahead preorder stream** — a primitive others build on.

Pick one before designing the struct.
