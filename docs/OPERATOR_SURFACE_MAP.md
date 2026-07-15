# Operator Surface & Flat-Family Map

> **Status: LIVING INVENTORY** (established 2026-07-13 from a full survey; last verified
> 2026-07-13). Companion to
> [LAZINESS_AND_BUFFERING_POLICY.md](LAZINESS_AND_BUFFERING_POLICY.md) — this is the
> reality that policy is checked against: what every operator returns, what it buffers and
> when, how the flat store family hangs together, and where ad-hoc store construction is
> duplicated (the cohesion-pass work list).
>
> **Maintenance convention:** any change that adds, removes, or re-shapes an operator,
> store, stream, decoder, or wrapper updates its row/entry here in the same commit — the
> same discipline the benchmark families follow. Sync/async twins share one row (the async
> source is the edit surface; the `.g.cs` is generated). When a policy-audit flag below is
> fixed, delete the flag and update the operator's row. Bump "last verified" whenever a
> full re-check happens; keep entries free of line numbers (files and shapes, not lines).

## 1. Operator surface — what returns what, what buffers when

Dims key: **F** = `ITreenumerable`, **D** = `IDepthFirstTreenumerable`, **B** =
`IBreadthFirstTreenumerable`. Behavior key:

- **streams** — wrapper treenumerator, bounded state (bound noted).
- **capture(deferred-once)** — full O(n) flat capture, construction pinned to first
  acquisition (`Tree.Lazy`), built on first pull, shared by all replays and both dimensions.
- **capture(eager)** — full O(n) at call time.
- **drains** — terminal; consumes the source to produce an enumerable/scalar.

### Tree-returning operators (Copse.Linq)

| Operator | Source dims | Returns | Behavior | State bound |
|---|---|---|---|---|
| Select | F, D, B | same-dim | streams | O(1); consecutive Selects fuse |
| Where / PruneBefore / PruneAfter | F, D, B | same-dim | streams | O(depth) DFT / O(width) BFT |
| TakeNodesUntil / TakeNodesWhile | F, D, B | same-dim | streams | O(1) |
| TakeTrees / SkipTrees | F, D, B | same-dim | streams | sugar over take/prune |
| TakeLastTrees / SkipLastTrees | F, D, B | same-dim | **eager count at call time** | two-pass by design (count the roots, then take/skip; decided 2026-07-13 — a single-pass form must buffer k whole subtrees); B's counting pass drains level 0 only |
| Union / Intersection / Subtract / SymmetricDifference | F×F, D×D, B×B | merge/narrow | streams | lockstep co-traversal, O(depth) DFT / O(width) BFT |
| Do / Hide | F, D, B | same-dim | streams | O(1) |
| RootfixScan (seed / rootNodeSelector) | F, D, B | same-dim | streams | O(depth) DFT / O(width) BFT |
| Invert | **B-narrow** | IBreadthFirstTreenumerable | **streams** | O(width) — the one genuinely streaming mirror (`InvertedLevelOrderStream`) |
| Invert | D-narrow; buffer | ITreenumerableBuffer | capture(deferred-once) | mirrored preorder arrays. **Specialization KEPT (decided 2026-07-15)**: Invert ≡ OrderChildrenByDescending by source sibling index (pinned by OrderChildrenByTests' subsumption law), but the specialized build is measured ~1.15x faster, 2.4x leaner on wide trees (no keys channel, LIFO emit, no per-group sort), and its B arm streams O(width) with NO capture — a cost class the keyed general operator cannot reach. Both families share trees on the Buffer leg, so the premium stays continuously measured; reopen only if the rows converge. |
| Invert | F | ITreenumerableBuffer | capture(deferred-once) | dimension-dispatched: DFT-first → mirrored preorder arrays; BFT-first → the streaming mirror drained once into level-order arrays (2026-07-13; both arms now share the build-on-first-pull cost shape) |
| LeaffixScan | D; **B**; F(→D) | ITreenumerableBuffer | capture(deferred-once) | O(n) result arrays, O(depth) build working set; **B overload Materializes the source first** (see flags) |
| OrderChildrenBy / …Descending (±comparer) | D; **B**; F(→D) | ITreenumerableBuffer | capture(deferred-once) | key selector once per node at capture, source context; stable per-group sort; D rides the keyed `PreorderCapture.CaptureFrom` → preorder layout; **B STREAMS (2026-07-15): one source walk, one buffered level (O(width) aux), level-order layout** — flag 4 |
| Memoize | F, D, B | **IMemoizeTreenumerableBuffer (IDisposable)** | capture(lazy, incremental) | ONE capture (2026-07-15): the first pull pins the layout; off-pin replays ride it cross-order; **source enumerated at most once** — upstream side effects fire at most once per node; pays only for the region reached; idempotent on a live memo; **the only disposable return on the surface** |
| Materialize | F(±strategy), D, B | ITreenumerableBuffer | **capture(eager)** | probes first (2026-07-13): a live memo is consumed in place; a compliant buffer returned as-is; otherwise `Memoize()+Consume()`. The strategy overload is a layout GUARANTEE (2026-07-15): never ignored — fresh memo pins it, mismatched buffer is TRANSPOSED from the buffer (new instance, source untouched); the both-layouts recipe = materialize twice, one source pass |

### Enumerable / scalar consumers (Copse.Linq)

| Operator | Source dims | Returns | Behavior | Notes |
|---|---|---|---|---|
| PreorderTraversal / LevelOrderTraversal | D / B | IEnumerable | streams | O(1)–O(depth) |
| PostorderTraversal | D | IEnumerable | streams | O(depth) pending path |
| GetRoots / GetLeaves | D (GetLeaves also B, F) | IEnumerable | streams | O(1) |
| GetLevels | B only | IEnumerable\<TNode[]\> | streams per level | O(width) reused deque; one array alloc per level |
| GetBranches | D only | IEnumerable\<TNode[]\> | streams per branch | O(depth); array per yield |
| Get\*Traversal (visit streams) | D, B, F (±strategy selector) | IEnumerable\<NodeVisit\> | streams | |
| RootfixAggregate (seed / selector) | D, B, F(→D) | IEnumerable | streams | RootfixScan + GetLeaves |
| LeaffixAggregate | D; **B** (documented capture, 2026-07-13); F(→D) | IEnumerable | streams per root (D) / **capture then fold (B)** | D peak = **largest root subtree**, buffers reused across roots; B (FUSED 2026-07-15) captures once into the memo's chunked level-order buffer, then an index-chasing DFS fold over the child spans — no visit stream decoded between the encodings; measured −39% time at near-baseline allocation (the factory-array variant was equally fast but 3x alloc — D4c evidence); cost class unchanged (peak = whole capture, first value after it) |
| AnyNodes / AllNodes / CountNodes / CountTrees | F, D, B | scalar | drains | Any short-circuits; CountTrees gained its B + F entries 2026-07-13 (B counting = a level-0 drain via SkipNodeAndDescendants) |
| Consume | F(±strategy), D, B | void | **drains, unconditionally** | MECHANICAL again (2026-07-15, probes REVERTED): walks a treenumerator to exhaustion whatever the receiver — buffers replay inertly, deferred captures are FORCED, a lazy capture completes as a side effect. The probe episode (2026-07-14→15) optimized for a caller that does not exist and silently broke the benchmarks; minimum-work settling lives on the lazy buffer's Complete() member and in Materialize. One word one meaning: Consume walks, Complete finishes, Materialize delivers |
| ToFormattedLines / ToFormattedString | D | IReadOnlyList\<string\> / string | **eager terminal** | honest since 2026-07-15 (flag 2): walks the source ONCE at the call — `To*` name, return shape, and cost now agree; one `(text, depth)` record buffer, reverse-rendered into the pre-sized result (formatter once per node, preorder); glyph contract pinned by `FormattedLinesTests` |
| ~~To\*TreeTokenizer~~ | D / B | tokenizer | streams | DEMOTED to Copse.Linq.Experimental 2026-07-15 (sync only; async deleted, codegen rows dropped): lost its last product consumer when ToFormattedLines went record-based, and shipping now would lock in the token shape — tokens carry less context than a treenumerator (no positions/visit counts), and a real consumer may want richer tokens. Revisit shape-first if a consumer appears. |
| ToDegenerateTree / ToTrivialForest | IEnumerable | ITreenumerable | streams | fresh enumerator per acquisition |

### Tree-source factories (Copse)

| Factory | Behavior |
|---|---|
| Tree.Defer / DeferDepthFirst / DeferBreadthFirst | factory re-runs **per acquisition** (call-by-name — that's Defer's contract) |
| Tree.Lazy / LazyDepthFirst / LazyBreadthFirst (+ dimension-observing form) | factory runs **once**, pinned for both dimensions (call-by-need); the deferral seam every capture op rides |
| Tree.Using (× dims) | resource per acquisition; treenumerator Dispose is the release point |
| Tree.Empty | singleton |
| Preorder/LevelOrderTreenumerable | full citizen over a random-access store (off-native dimension rides cross-order, ~1.08x tax) |
| Preorder/LevelOrderStreamTreenumerable | narrow-dimension over a forward-only stream; fresh stream per acquisition, treenumerator owns/disposes it |

### Experimental

`ExpandNode` and `Graft` (F ×5 overloads each) return `ITreenumerable` but their
breadth-first treenumerator factory is `() => throw new NotImplementedException()` — see flags.

### Policy-audit flags (2026-07-13)

Checked against [the policy](LAZINESS_AND_BUFFERING_POLICY.md). The good news first: **no
per-traversal re-capture exists anywhere** (every capture op is `Tree.Lazy`-pinned), and the
**disposable audit is clean** (`Memoize` is the sole disposable return). The strains:

1. **`ExpandNode` / `Graft` break the dimension split's guarantee at runtime**: full
   `ITreenumerable` return type, `NotImplementedException` on any BFT acquisition. Under the
   split's own rules these should be D-narrow until their BFT arms exist. (Experimental, but
   the type is still lying.)
2. *(RESOLVED 2026-07-15)* `ToFormattedLines` was `To`-named but lazy-shaped and lazy-shaped
   but eager-costed (full forest drain before the first yield, re-drained per enumeration,
   forest buffered twice: token list + rendered-line stack). Fixed by making the shape honest
   rather than the implementation lazy: returns `IReadOnlyList<string>`
   (`ValueTask<IReadOnlyList<string>>` async, now `ToFormattedLinesAsync`) — eager like every
   `To*` terminal, work exactly once. The per-node glyph gap is O(subtree) (a node's ├/└
   needs "does a later sibling follow"), so a streaming shape was never owed under rule 2;
   the per-tree-streaming TODO was deliberately dropped (no consumer). The rewrite also
   halved the buffering: one `(text, depth)` record list (depth deltas carry the full tree
   shape — the tokenizer emits group tokens only on depth changes, so records reconstruct
   it), reverse-rendered bottom-up into the pre-sized result array. Output pinned by
   `FormattedLinesTests` goldens captured from the old renderer.
3. *(RESOLVED 2026-07-13)* `TakeLastTrees` / `SkipLastTrees`: D and B narrow overloads
   added (with the `CountTrees` B twin + F disambiguator they require). The two-pass shape
   was examined and kept deliberately: the sequence-style single-pass trick exists but its
   queue slots hold k whole *subtrees* (the parked preorder-window at tree granularity), so
   two passes at O(1) space is the better default; impure-`Defer` callers can `Materialize`
   first. B's counting pass drains level 0 only.
4. **BFT-narrow `LeaffixScan` / `OrderChildrenBy` double-capture**: the deferred build
   `Materialize()`s the source into a full memo, then walks that capture into the result
   arrays — two O(n) allocations transiently vs one on the DFT path. Correct under the
   disclosure rule, but a candidate for a fused single capture.
   *(RESOLVED for `OrderChildrenBy` 2026-07-15, and better than fused — STREAMING: the
   level-permutation build walks the BFT source ONCE straight into the ordered level-order
   encoding. Sibling groups are contiguous in level-order arrival and no level-d node is
   visited before level d finishes scheduling, so each level's permutation settles before its
   children arrive — one buffered level (O(width) auxiliary) suffices, refuting the original
   commit's "cannot generalize." The result's native layout is level-order; both entries now
   replay their own arrival dimension natively. `LeaffixScan`-B still hoists — its fold needs
   depth-first close order; the fused-fold candidate is the LeaffixAggregate-B index-chasing
   pattern.)*
5. *(RESOLVED 2026-07-13)* `Invert(F)`'s BFT-first arm now builds its whole capture on the
   first replay pull (a one-shot drain of the streaming mirror via the stream-shaped
   `LevelOrderCapture.CaptureFrom`), matching the preorder arm's cost shape. The
   dispose-completes-capture surprise is gone (dispose owes nothing; the source is released
   inside the build), `StreamFedLevelOrderStore` was **deleted**, and the orphaned
   `LazyLevelOrderStore` became the arm's deferral seam. The tier-by-tier laziness this
   traded away was only ever real for a replay abandoned *without* disposal.
6. *(FIXED 2026-07-13)* `Materialize` now probes before memoizing: a live memo is consumed
   in place (the aliasing is by design and documented in the XML docs); a completed buffer
   is returned as-is instead of being wrapped in a fresh memo and copied node-by-node.

## 2. Flat-family dependency map

Async twins are the codegen **sources** (in `Copse.Async` / `Copse.Linq.Async`); the sync
`.g.cs` files are transcriptions. Sync SPIs and array stores live in **Copse.Primitives**
(which has no async layer — a fact the cohesion pass must reckon with).

```
SPIs — per-color since the de-share (2026-07-14): async sources in Copse.Async/Stores
(namespace Copse.Async.Stores), sync twins GENERATED into Copse/Stores (Copse.Stores);
the read structs (PreorderRead/LevelOrderRead) and completed array stores
(Preorder/LevelOrderArrayStore + their new Async* twins) follow the same pattern.
Copse.Primitives/FlatStores is retired.
├─ I(Async)PreorderStore   random-access preorder; growable (Ensure* may pull a feed)
├─ I(Async)LevelOrderStore random-access level-order dual
├─ I(Async)PreorderStream  forward-only preorder; TrySkipToDepth skip seam; disposable
└─ I(Async)LevelOrderStream forward-only level-order groups; disposable

Decoders (Copse/Treenumerators ← Copse.Async/Treenumerators)
├─ PreorderStoreDepthFirstTreenumerator      NATIVE   (span arithmetic)
├─ PreorderStoreBreadthFirstTreenumerator    cross-order (visit queue + schedule stack)
├─ LevelOrderStoreBreadthFirstTreenumerator  NATIVE   (sequential index)
├─ LevelOrderStoreDepthFirstTreenumerator    cross-order (child-span chasing, O(depth))
├─ PreorderStreamDepthFirstTreenumerator     NATIVE only (O(depth) path + lookahead)
└─ LevelOrderStreamBreadthFirstTreenumerator NATIVE only (masked-ring window, O(width))
   (no stream cross-order decoders — the dimension split, by design)

Wrappers (Copse/Treenumerables)
├─ PreorderTreenumerable<TStore>        full citizen → both preorder decoders
├─ LevelOrderTreenumerable<TStore>      full citizen → both level-order decoders
├─ PreorderStreamTreenumerable<TStream>   D-narrow → stream DFT decoder (owns stream)
└─ LevelOrderStreamTreenumerable<TStream> B-narrow → stream BFT decoder (owns stream)

Capture factories (Copse/Stores ← Copse.Async/Stores; public statics; ADDED 2026-07-13)
├─ PreorderCapture.CaptureFrom(source[, sideChannelSelector])  the ENCODE direction, written
│    once: shape A hoisted from the operator builds → PreorderArrayStore (+ preorder-parallel
│    side array — OrderChildrenBy's keys hook). Consumers: Invert's build; OrderChildrenBy
│    adopts at its rebase. LeaffixScan stays bespoke (its close-hook needs ChildAccumulations,
│    a Copse.Linq type this layer cannot see).
└─ LevelOrderCapture.CaptureFrom(source)      shape B in one-shot form (the memo's front-cursor
     parse) → LevelOrderArrayStore. No consumer yet; first candidates are the LeaffixScan-B /
     LeaffixAggregate-B fusions. No side-channel overload until a consumer exists.

Concrete stores/streams                       consumers
├─ (Async)PreorderArrayStore (readonly structs)  Invert-D/F, OrderChildrenBy, LeaffixScan
│    (per-color since the de-share: Copse/Stores builds all terminate here; benchmarks; tests
│     ← Copse.Async/Stores, completed arrays)
├─ (Async)LevelOrderArrayStore (readonly structs)  Invert-F BFT arm; benchmarks/tests
├─ LazyPreorderStore (internal, Linq)    THE deferral seam: Invert-D, OrderChildrenBy,
│    runs a Func<PreorderArrayStore> once     LeaffixScan all ride it
├─ LazyLevelOrderStore                   Invert-F BFT-first arm's deferral seam (orphan
│                                             ADOPTED 2026-07-13, flag 5)
│  (StreamFedLevelOrderStore DELETED 2026-07-13 — its incremental drain became the
│   stream-shaped LevelOrderCapture.CaptureFrom, one-shot; no preorder dual, still)
├─ Memoize{Preorder,LevelOrder}Store     the memo's resumable captures (preorder /
│    + …Store readonly-struct SPI adapters    level-order encodings, PullOne/Consume)
├─ InvertedLevelOrderStream                   the streaming mirror (O(width) tier transform)
├─ PreorderStringStore / LevelOrderStringStore   serializer string tier (hand-written sync-
│    + nested .Handle struct adapters            only; a string can't suspend)
└─ PreorderTextStream / LevelOrderTextStream     serializer streaming tier (forward-only)

Outside the family, on purpose:
└─ TestUtils PreorderTree — same (values[], subtreeSizes[]) encoding but rides the DFS/BFS
   ENGINE via PreorderChildEnumerator: the conformance oracle must not route through the
   flat-family playback it referees. (PreorderArrayStore's header still claims PreorderTree
   "dissolves into" it — aspirational, not current.)
```

## 3. Ad-hoc store construction sites (the cohesion-pass work list)

Two canonical loops are re-implemented across the codebase:

- **Shape A — the DFT capture loop**: walk `SchedulingNode` visits; open-index stack;
  backfill `subtreeSizes[closed] = values.Count - closed` on depth retreat; `0` = still open.
- **Shape B — the BFT capture loop**: append `(value, firstChildIndex=-1, childCount=0)`;
  wire into the front node's span; advance the front on first visit.

| # | Site | Shape | Builds | Variation |
|---|---|---|---|---|
| 1 | `Treenumerable.Invert.g.cs` `BuildMirror` | ~~A~~ **factory** + span-hop emit | `PreorderArrayStore` | *(2026-07-13)* phase 1 now rides `PreorderCapture.CaptureFrom`; the zero-key LIFO emit stays specialized (CI benchmark rows) |
| 2 | ~~`Treenumerable.OrderChildrenBy.g.cs` `BuildOrderedChildren`~~ | ~~A + span-hop emit~~ | `PreorderArrayStore` | *(RESOLVED on the branch, 2026-07-15)* the shape-A half rides `PreorderCapture.CaptureFrom(source, keySelector)` — the keyed side-channel overload built for this consumer; the operator keeps only the sibling-group-sort emission |
| 3 | `Treenumerable.LeaffixScan.g.cs` `BuildLeaffixScan` | A | `PreorderArrayStore` | richer close: pending-node stack carries NodeContext, close computes the accumulation |
| 4 | `Treenumerable.LeaffixAggregate.g.cs` | A | **no store** | same loop, per-root reused buffers, lazy yield — bounds what a store factory can absorb |
| 5 | `Memoize{Preorder,LevelOrder}Store.g.cs` | A / B, resumable | memo buffers | `PullOne` = one loop iteration suspended; `Consume` = the loop with guards hoisted; selector `VisitCount==1` instead of `SchedulingNode` (equivalent in DFT — documented there) |
| 6 | ~~`StreamFedLevelOrderStore.g.cs`~~ | B (append wiring) | — | *(2026-07-13)* deleted; its drain lives on as the stream-shaped `LevelOrderCapture.CaptureFrom(ILevelOrderStream)` |
| 7 | `PreorderStringStore` / `LevelOrderStringStore` (serializer) | A / B arrays from **text** | themselves | open stack driven by `(`/`)` or group terminators; leaves committed `subtreeSizes=1` immediately (vs backfill) |
| 8 | `TestUtils EngineTree.ParseArrays` | A from text | raw arrays for `PreorderTree` | intentionally independent (oracle) |
| 9 | `Benchmarks FlatDecode.FlatEncodings` | A verbatim; plus a preorder→level-order **transpose** that exists nowhere in product | both array stores | transpose was measured out of product (~1.08x cross-decode tax vs ~5-replay break-even) |
| 10 | ~~`Copse.Linq.Tests FlatFamilyConformanceTests`~~ | ~~A / B verbatim~~ | — | *(RESOLVED 2026-07-15)* the duplicate stores fell to the public types, then the hand-rolled A/B build loops fell to `PreorderCapture`/`LevelOrderCapture.CaptureFrom` — conformance now runs the product chain (factory → store → decoder) against the engine oracle |

Each product site (1–6) exists twice on disk, once in source — the async file in
`Copse.Linq.Async` is the edit surface; the sync `.g.cs` is generated.

## 4. Coherence observations (feeding discussion-queue item 3)

1. **The array stores are the natural factory home.** All three operator builds terminate in
   `new PreorderArrayStore<T>(values, subtreeSizes)` after restating the store's own
   documented invariant. A `PreorderArrayStore.CaptureFrom(source[, per-node selector])`
   factory plus a sibling-group-reorder emission would collapse sites 1, 2, and the
   benchmark/test copies; `LazyPreorderStore(() => PreorderArrayStore.CaptureFrom(...))`
   is exactly today's call pattern with the loop named and moved. LeaffixScan needs a
   close-hook (accumulator) and LeaffixAggregate needs the no-store reusable-buffer form —
   they mark the boundary of what one factory can absorb.
2. **Placement problem — DECIDED 2026-07-13** (see
   [STORE_FAMILY_REVIEW.md](STORE_FAMILY_REVIEW.md)): `Copse.Primitives` references only
   `Copse.Vocabulary` and cannot see treenumerators at all, so the factories go in the
   `Copse`/`Copse.Async` codegen pair (the layer that already owns the decoders). Not yet
   built — sequenced after the OrderChildrenBy-B streaming spike (flag #4).
3. **Naming seams**: (a) *(RESOLVED 2026-07-13)* the memo cluster's storage types were
   renamed to encoding names (`MemoizePreorderStore`/`MemoizeLevelOrderStore` +
   `MemoizePreorderStore`/`MemoizeLevelOrderStore`) under the adopted rule — traversal
   things carry dimension names, storage things carry encoding names; every store now has a
   one-line taxonomy header; (b) *(RESOLVED by the de-share 2026-07-14)* the nested
   `.Handle` adapter convention is now universal (memoize stores and serializer alike);
   (c) *(RESOLVED 2026-07-15)* the test-side store re-implementations are gone —
   `FlatFamilyConformanceTests` rides the public stores and the public capture factories.
4. **Missing duals** (cross-check [dual-symmetry backlog]): ~~`LazyLevelOrderStore`
   orphan~~ *(adopted 2026-07-13 — it is now Invert-F's BFT-first deferral seam; the
   stream-fed store it displaced was deleted, its drain preserved as the stream-shaped
   `LevelOrderCapture.CaptureFrom`)*; no stream-shaped `PreorderCapture.CaptureFrom`
   (`IPreorderStream`) dual yet — nothing needs it; ~~no public sync→async completed-store
   adapter~~ *(dissolved by the de-share 2026-07-14: `Async{Preorder,LevelOrder}ArrayStore`
   are real types now, and the benchmark-private hack is deleted)*; the
   preorder→level-order transpose lives only in benchmarks (deliberately).
5. **Selector inconsistency inside shape A**: operator builds filter on
   `Mode == SchedulingNode`, memo/tests on `VisitCount == 1`. Equivalent in DFT, but a
   hoisted factory should pick one and document why.
