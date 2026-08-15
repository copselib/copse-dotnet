# Memoize — design spec (two-dimensional, incremental, eager-skip)

> **SUPERSEDING UPDATE — THE SINGLE-CAPTURE MEMO (2026-07-15).** The "two-dimensional" half
> of this spec — one capture per dimension, the four-case serving rule, drop-on-completion,
> ref-counted straggler replays — is retired. The memo now holds ONE capture whose layout the
> first acquisition (or a strategy-declaring Consume/Materialize on a fresh memo) pins;
> replays in the other dimension ride the same capture cross-order, over a still-growing
> capture too. What this buys: the source is enumerated AT MOST ONCE — full stop — so side
> effects upstream of the memo fire at most once per node (the dual-buffer design opened a
> second feed whenever both dimensions had partial work, silently double-firing effects);
> the strategy arguments to Consume/Materialize become fresh-memo PIN requests (an existing
> pin outranks them); and a page of race/drop machinery deletes. What it costs: off-pin
> replays pay the cross-order locality tax, and an off-pin PARTIAL drain may over-pull. This
> is the model every later capture op (Invert-F's first-dimension pin, the narrow-source
> memos) had already converged on. Everything below about incremental capture,
> eager-skip/no-holes, retain-all, and disposal semantics still stands; read
> "two-dimensional" as historical.
>
> **Status: DECIDED 2026-07-03, not yet implemented.** Supersedes the 2026-06-27 preorder-only
> spec. Of that spec's four locked decisions, two carry forward unchanged (eager-skip/no-holes,
> no child-realization), one is softened (the finite-tree precondition), and one is overturned
> (preorder as the single representation). The dead experimental `MemoizeTreenumerator` stub
> remains abandoned — do **not** resurrect it. The capability-interface lattice
> ([TREE_CAPABILITY_INTERFACES.md](TREE_CAPABILITY_INTERFACES.md)) remains out of scope.

## What `Memoize` is

`Memoize()` turns a tree into a **re-traversable, shared snapshot of its current shape**. The
caller is declaring "I will enumerate this tree more than once; capture it as I go." Subsequent
enumerations replay from the captured buffers instead of re-running the (possibly expensive)
source, and each enumeration may prune differently.

```csharp
var memo = expensiveTree.Memoize();
foreach over memo (DFS, follow branch A)   // builds only the preorder prefix it reaches
foreach over memo (DFS, follow branch B)   // reuses the shared buffer, extends it as needed
foreach over memo (BFS)                    // lazily builds ITS OWN dimension — no full-build wall
```

## The governing principle: a memo is only as lazy as the dimension of its feed

A linear buffer filled from a traversal stream can be served incrementally only in that
stream's order. A preorder buffer fed by DFT serves DFS replay lazily, but BFS replay hits a
wall: reaching the *last child of the root* requires hopping completed subtree sizes, i.e.
buffering essentially the whole tree first. The dual holds: a level-order buffer fed by BFT
serves BFS lazily, but DFS descent to depth *d* must buffer every level above it. **No single
layout is lazy in both dimensions.**

The DFT/BFT duality is this library's core promise — traverse in the optimal dimension. The
old spec's "preorder is enough" quietly forfeited that promise for BFS consumers. This design
does not: **each dimension gets its own feed and its own native-order buffer, created lazily on
first use.** The locality question ("BFS over a preorder array is cache-hostile") turned out to
be the small half of the problem; the *laziness* asymmetry was the real one.

## Locked decisions

1. **No holes / eager-skip (kept, now per dimension).** Each dimension's feed drives the source
   with `NodeTraversalStrategies.TraverseAll`. Consumer pruning is applied only at *replay*, as
   a view — it changes what an enumeration *yields*, never what is *cached*. Each buffer is
   always a contiguous prefix of its own full traversal stream.

2. **No child-realization (kept).** No holes means no out-of-order realization. No
   `IChildVisitableTree`, no capability lattice.

3. **Two dimensions, no strategy parameter (new).** `Memoize()` takes no arguments. Each
   dimension's buffer — (source feed, native-layout buffer, complete flag, replay ref-count) —
   is created lazily on the first replay request in that dimension. A consumer who only ever
   does DFS never pays a cent for BFT. "Which dimensions to memoize" answers itself by usage;
   an explicit `MemoizeStrategy` enum was considered and dropped because the lazy-buffer model
   makes every point of that enum the automatic behavior.

4. **Native layout per dimension (overturns preorder-only).**
   - DFT buffer: **preorder** — `values`, `subtreeSizes` (node *i*'s subtree spans
     `[i, i + subtreeSizes[i])`), built with the open-parent-stack construction `Materialize`
     and `TreeSerializer.Parse` already use; `subtreeSizes[i]` is backfilled when the subtree
     closes (next scheduling visit at depth ≤ node *i*'s, or stream end).
   - BFT buffer: **level-order** (LOUDS-adjacent) — `values` in arrival order (BFT scheduling
     order *is* level order), plus child-span bookkeeping (`firstChildIndex`, `childCount`),
     backfilled as children arrive. The parent of each scheduled node is identified by the
     interleaved visiting node in the BFT stream (BFT visits a parent between scheduling each
     of its children — see CLAUDE.md's DFT/BFT cadence section). A node's children occupy a
     contiguous level-order span, closed when the parent's scheduling completes.
   - The two builders are deliberate structural duals, in the same spirit as
     `DepthFirstPath`/`BreadthFirstPath`.

5. **Once either dimension completes, the source is never touched for new work again.** The
   four-case serving rule below. On completion, the *other* (incomplete) dimension buffer is **dropped, not
   completed**: it takes no new customers, and is released when its ref-count of outstanding
   replay enumerators drains to zero. In-flight replays in the dropped dimension keep their own
   buffer — its indices are stable (append-only) and the dimension's own feed remains available to
   them, so no mid-enumeration cut-over is ever attempted (that remapping would be genuinely
   hard; we don't do it). Worst case they finish at source cost, which is what they'd have paid
   anyway.

6. **Backing store: an internal chunked append-only ref store (new; lives in `Copse`).**
   Neither existing structure fits:
   - `RefSemiDeque` has the right ref discipline but O(#partitions) indexed reads
     (`GetFromBack` walks a linked list of non-uniform partitions) — wrong for replay's
     index-hammering — and its LOH-capped partitions serve *churning* Path buffers, the
     opposite lifecycle of a monotonic memo buffer.
   - Aocl (`github.com/jasonmcboyd/Aocl`) has the right layout — doubling power-of-two
     partitions, preallocated 31-slot outer array, O(1) index→(partition, offset) via integer
     log2 — but its get-only, by-value indexer is the load-bearing wall of its lock-free
     concurrency contract ("a published element never changes"), and the memo *must* mutate
     published slots (the size/span backfill). Its per-append `lock`/`Volatile`/doorbell is
     pure tax for a single-threaded builder. And Copse stays dependency-free.

   So: crib Aocl's layout math into an internal Copse type with a **ref-returning indexer** and
   **zero synchronization**. It is append-only *structurally* (slots never move — no
   `List<T>`-style relocation copies) while permitting in-place content mutation — exactly the
   distinction that separates it from Aocl. Uncapped doubling partitions are correct here (a
   handful of large long-lived blocks; LOH is the cheap outcome), mirror-commented against
   `RefSemiDeque`'s cap rationale. `BitOperations.Log2` on net8.0; Aocl's De Bruijn table on
   net48.

## The four-case serving rule

A replay request in dimension **D** (with **O** the other dimension) resolves:

| # | State | Serve by | Source touched? |
|---|-------|----------|-----------------|
| 1 | D's buffer complete | D's engine over D's buffer, natively | no |
| 2 | O's buffer complete | D's engine over O's buffer, **cross-order** | no |
| 3 | D's buffer exists, partial | D's engine over D's buffer, extending D's feed lazily | only past the frontier |
| 4 | D has no buffer; O absent or partial | create D's buffer, as (3) | yes — D's own feed |

Case 2 is the payoff of "be smart": a consumer who fully DFS-enumerates once and then asks for
BFS pays zero further source cost — the engine rides the completed preorder buffer in BFS
order (correct today: `PreorderTree` already rides both engines). The cross-order **locality
tax is accepted**; completing D's native buffer from O's completed one is strictly-in-memory,
purely additive, and **deferred until benchmarks demand it**.

Case 4 is the only path to enumerating the source twice, and it requires genuinely partial
work in *both* dimensions. Each dimension's feed still runs at most once, only to its own
frontier. Corollary: an impure source could be captured differently by the two feeds — the
same stability-across-the-lazy-build assumption the single-feed design already made, just
wider. Documented precondition, not machinery.

## Replay mechanism

Replay treenumerators are **the standard DFT/BFT engines** over a lazy child enumerator — we
do not hand-write replay traversal or replay pruning; every `NodeTraversalStrategies` flag
comes free, positions come from the engine, and the buffers store only values + structure.

The lazy child enumerator is the *open-span dual* of `PreorderChildEnumerator`: that type
precomputes `_End = parentIndex + subtreeSizes[parentIndex]` in its constructor, which does not
exist while the parent's subtree is still open. Instead, the memo's child enumerators consult
the dimension buffer's **fill primitive** on each advance:

- **Fill primitive** (per dimension buffer): pull the feed's `MoveNext(TraverseAll)`, appending on
  scheduling visits and backfilling closes, until a requested boundary is buffered — "value at
  index *k* exists" or "node *k*'s span is closed."
- **Preorder child advance**: next sibling = `cursor + subtreeSizes[cursor]`. In natural DFS
  replay the engine only asks after the child's subtree was just traversed, so the size is
  already backfilled and the fill is a no-op. A consumer `SkipDescendants`/`SkipSiblings` hop
  over an *untraversed* subtree fills until that subtree closes — the eager-skip cost, paid
  lazily and only on the skipped span.
- **Level-order child advance**: children are the contiguous span under the parent's
  (`firstChildIndex`, `childCount`); fill until the span closes if the parent is still
  scheduling.

Multiple live replay enumerators per dimension buffer are safe (single-threaded): the buffer is append-only
and monotonic, each replay reads by index, fills cannot re-enter fills, and nobody rewinds
anybody.

## Infinite trees (precondition softened)

The old spec required a finite source outright. Lazy per-dimension fill dissolves most of that:
each dimension fills only to its consumer's frontier, so **a memoized traversal terminates
whenever the same un-memoized traversal terminates**, with two honest exceptions, both
consequences of eager-skip (decision 1):

- `SkipDescendants`/`SkipSiblings` on the **DFT dimension** over an unbounded subtree hangs:
  the hop needs the skipped subtree's size, which never closes. (Un-memoized DFS would just
  pop.) Bound first — `tree.PruneAfter(d).Memoize()` — if you need skip-hops over unbounded
  regions.
- **Cross-order serving never activates** on an unbounded source (nothing ever completes), so
  both dimensions run their own feeds. Correct, just no case-2 reuse.

The optional node-count guard (throw instead of hang) remains a deferred nicety.

## Surface and semantics

- **Operator**: `Memoize<TValue>()` extension in `Copse.Linq`
  (`Treenumerable/Treenumerable.Memoize.cs`). No parameters.
- **Return type**: `ITreenumerableBuffer<TValue> : ITreenumerable<TValue>, IDisposable` —
  promoted out of `Copse.Linq.Experimental` into `Copse.Linq`. The concrete memo type is
  `internal`. No `ToPreorderTree()` for now (deferred; additive).
- **Why `IDisposable`**: the memo holds up to two live inner treenumerators (the feeds) paused
  mid-source-traversal; the buffers themselves are just managed memory.
- **Dispose semantics**: disposing the memo disposes both feeds immediately. Outstanding replay
  enumerators keep working over already-buffered regions (they never touch the feeds there); a
  replay that hits its buffer frontier after dispose gets `ObjectDisposedException`. Replay
  enumerators' own `Dispose` decrements their dimension buffer's ref-count — this is what releases a
  dropped dimension buffer (decision 5), so replay disposal is semantically load-bearing, consistent with
  how child-enumerator `Dispose` already signals skips to the engine.
- **Thread-safety**: none; single-threaded by contract, documented. The buffers could be made
  read-concurrent, but the shared feeds cannot be cheaply, and per-node synchronization on the
  replay path is exactly the tax we declined to import from Aocl.
- **Experimental stub retirement**: delete `MemoizeTreenumerable`,
  `Treenumerators/Memoize/*`, the Experimental `Treenumerable.Memoize.cs`, and the
  Experimental `ITreenumerableBuffer` (superseded by the `Copse.Linq` one).

## Edge cases the implementation must cover

- **Empty tree** — replay yields nothing; either feed exhausts immediately; both "complete."
- **Single node**; **multi-root forest** (preorder builder closes to depth 0; level-order
  builder's roots are the depth-0 prefix).
- **Every consumer strategy on replay, in both dimensions** — inherited from the engines, but
  verify each against fixtures; `SkipNode` (promote children) is structurally different from
  `SkipDescendants`.
- **Partial-then-deeper enumeration** — buffer extends; shared feed resumes.
- **DFS-then-BFS and BFS-then-DFS after full enumeration** — case 2; assert the source is not
  re-enumerated (counting source).
- **Both-partial** — case 4; both feeds live; both extend correctly.
- **Completion with an outstanding straggler replay in the dropped dimension** — straggler
  finishes on its own feed; its dimension buffer releases when it disposes.
- **Post-dispose replay** — buffered region works; frontier throws.
- **Unbounded source (Collatz)** — terminating consumers terminate in both dimensions.

## Deferred (recorded, not blocking)

- Cross-order **locality tax**: revisit only if benchmarked demand appears; the remedy (finish
  the native buffer from the completed other buffer, in memory) is purely additive.
- **In-memory feed adapter** (BFS/DFS over a completed buffer *impersonating* a source feed to
  a straggler dimension buffer) — same machinery as the locality remedy; if either is ever built, the
  other is nearly free.
- `ToPreorderTree()` / exposing the completed snapshot.
- Node-count guard for unbounded sources.
- Thread-safe replay.

## Connection to serialization (recorded, not actioned)

The two buffer layouts are the two linear tree encodings: preorder + `subtreeSizes` is
balanced-parentheses in array form; level-order + child spans is LOUDS-adjacent. A linear
*store* (disk) is lazy in exactly one dimension for the same reason a linear *buffer* is — so
serialization wants the same layout knob, and **streaming deserialization is memoization whose
feed is a disk stream**: the file's layout order determines which dimension replays lazily.
The two incremental builders written for Memoize are, respectively, the cores of a preorder
and a level-order serializer. Getting them right here funds that track.
