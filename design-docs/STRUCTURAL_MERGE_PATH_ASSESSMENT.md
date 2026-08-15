# StructuralMerge — FINAL ASSESSMENT + DESIGN

## VERDICT

The sans-I/O …Path encapsulation **helps put StructuralMerge to bed, but only as a vehicle — not as the fix.** It buys exactly two things: (1) a single authoritative home for the merged-path structural state and position math, killing the three-sources-of-truth tangle and the `FixUpNodeVisitsStack` reconciliation; and (2) async-readiness — the 2-wide `await` seam isolated behind a synchronous core. It buys **zero correctness**: the base `DepthFirstTreenumerator` is already a thin driver over a correct `DepthFirstPath`, yet the merge engine is broken in its *algorithm*, and relocating broken recurrence logic behind a seam relocates the bugs. This is the direct lesson of the Where-extraction NO-GO (`design-docs/DFT_WHERE_EXTRACTION_SPIKE.md`): a cleaner state model is orthogonal to an algorithmic win. The red-team forced three corrections to my original design that I am adopting wholesale: **(a) my proposed O(1) effective-root formula was concretely wrong** — all three skeptics produced the same counterexample and it must be replaced by the proven `Count`-based test; **(b) the DFT seam is *not* as cleanly separable as I claimed** — the advance-selection gate *is* the merge-join's core structural decision, a bidirectional driver↔path dependency, so the boundary must be redrawn around a pure selector; and **(c) the no-LOH checkmark was too confident** — at the default reference-type payload a full `RefSemiDeque` partition already crosses the 85KB LOH threshold and must be called out, not dismissed. With those folded in, the three success criteria (O(n), no whole-tree cache, no per-node alloc) are **achievable for a *correct* algorithm** — but the current code is not correct, so the criteria describe a design that does not yet exist and are conditional on the correctness work landing. **There is no algorithmic wall** in either DFT or BFT; the obstruction is recurrence-logic defects plus a missing oracle.

---

## 1. CURRENT STATE

- **DFT: effectively unvalidated.** `UnionTests.cs` has every non-empty DFT row (lines 21–34) commented out; only the empty∪empty case runs. "DFT works" is aspirational — current automated DFT validation is essentially zero.
- **BFT: broken and `[Ignore]`d** (line 161). Three independent algorithm defects, verified against source:
  - **VisitCount shortcut** (lines 135–141): revisits gated on `_LeftTreenumerator.VisitCount > _NodeVisits[0].VisitCount`, equating the *merged* node's revisit count with *one source's* count. Wrong whenever left/right disagree on child structure.
  - **Single `[0]` lead slot** (line 46): one FIFO slot stands in for "currently-visiting parent," but BFT must revisit *every* node at a level interleaved with scheduling. One slot cannot express "schedule b, visit a, schedule c, visit a."
  - **Missing propagation**: no `SkipSiblings` cross-propagation, no multi-level retire/backtrack reconciliation, `SkipNode`/`SkipNodeAndDescendants` precedence not handled.
- **DFT defects unfixed by any refactor** (verified, will survive an algorithm-preserving extraction):
  - **`SkipNodeAndDescendants` precedence**: it is `SkipNode | SkipDescendants`, and `FixUpNodeVisitsStack` line 116's `HasNodeTraversalStrategies(SkipNode)` fires for the compound → promotes descendants meant to be pruned. The base engine guards this (DepthFirstTreenumerator lines 76–79); the merge does not.
  - **`SkipNode` child-promotion**: line 117 pops only the skipped top; promoted children keep raw inner depth/sibling positions → wrong published siblings.
  - **Multi-level divergent backtrack**: the `max`-collapse in `FixUpNodeVisitsStack` must re-emit between-children visits as one side unwinds while the other is exhausted (`-1`).
  - **O(n²) scan**: `HasAncestorInNodeVisits` (lines 207–217) is a genuine `foreach` over the whole stack → O(depth)/step → O(n²) on a degenerate chain under adversarial `SkipSiblings`.

---

## 2. DFT-FIRST DESIGN — `MergeDepthFirstPath`

### Seam vs. core — corrected boundary

My original framing ("the gate stays driver-side because it is about ordering emissions, not tree structure") is **withdrawn**. The skeptics are right: unlike `DepthFirstPath`/`WhereDepthFirstPath` — which have **one** inner that is **always** advanced and a trivial advance decision — the merge's advance-*selection* (`callMoveNextOnLeft = Node.HasLeft && _LeftTreenumerator.Position == Position`) **is** the merge-join's core structural question, computed by the **same** min-position comparator (`CreateNextNodeVisit`, lines 140–152) that builds the frame. The interesting structure is *upstream* of the pull, not downstream as in Where. That is a bidirectional dependency.

**Corrected boundary — the path exposes a pure selector; the driver does only literal I/O:**

- **Core (`MergeDepthFirstPath`) — pure, testable, async-shareable:**
  - `Select(in InnerSnapshot<TLeft>, in InnerSnapshot<TRight>)` → which side(s) are on the merged path (the min-position comparator) **plus** the next frame. This is the algorithm and it lives in the core where the oracle can hit it directly.
  - `_NodeVisits` as **`RefSemiDeque<NodeVisit<MergeNode<…>>>`** (replacing the BCL `Stack`, which is copy-semantics and off-pattern). `RefSemiDeque` already exposes `ref GetLast/GetFromBack/RemoveLast` and `AddLast`.
  - `Unwind(leftDepth, rightDepth, consumerSkipNode)` — the `FixUpNodeVisitsStack` body, pure structural unwind off two depths + a skip bit, with the `SkipNodeAndDescendants`-before-`SkipNode` precedence fix baked in.
  - `AdvanceFrontier(...)` → pushes and returns the frame **by `ref`** so the driver publishes with no second lookup.
  - The O(1) effective-root test (below).
- **Driver (shell) — the only I/O:** the two `MoveNext` calls dictated by the selector; `_LeftTreenumeratorFinished`/`_RightTreenumeratorFinished`; the published quad and `Publish(ref frame)`; the `SkipSiblings` cadence tokens (`_*SiblingSkipPending`, `_SiblingSkipDepth`) that *sequence* manufactured-skip injection. The driver computes nothing structural — it asks the core's selector and performs the literal pulls. **This is the only split that makes the async story pay off:** the comparator (the algorithm) is in the synchronous core; only the `MoveNext`s are awaited.

### The O(1) replacement for `HasAncestorInNodeVisits` — corrected

My original sketch — `CurrentIsEffectiveRoot() => Count <= 1 || GetFromBack(1).Position.Depth >= GetLast().Position.Depth` — is **wrong and is dropped.** All three skeptics produced the same refutation, confirmed against source:

`HasAncestorInNodeVisits` returns true iff **any** frame is strictly shallower than the **current published** `Position.Depth` — a *global* existence query. Under `SkipNode` the merge stack is **non-contiguous in depth** (line 117 pops only the skipped top while deeper accepted frames remain). Counterexample: frames at depths `[0, 2, 3]`, current depth 3. Real answer: depth-0 and depth-2 are shallower → **has ancestor** (not effective root). My formula: `GetFromBack(1)=2 >= GetLast()=3` is false, `Count>1` → **effective root**. Exactly inverted — it would silently mis-arm `SkipSiblings` cross-propagation.

**The proven O(1) idiom is `Count`-based, not a one-frame depth peek.** `DepthFirstPath.SkipRemainingSiblings` (line 168) uses `_AcceptedNodes.Count == 1` for exactly this effective-root test. The replacement:

```csharp
// Effective root iff the accepted path holds only this node (everything above was skipped).
// O(1); matches DepthFirstPath.SkipRemainingSiblings line 168.
public bool CurrentIsEffectiveRoot() => _NodeVisits.Count == 1;
```

If the merge's `_NodeVisits` is found to retain skipped frames the way the legacy stack does (rather than the accepted-only discipline `DepthFirstPath` uses), the correct O(1) form instead **maintains a running shallowest-live-frame depth** updated on push/pop and compares it against the current published depth — still O(1), still scan-free. **Which of the two is correct is decided empirically in Step 1**: add a regression test with a non-contiguous skip stack (frames `[0,2,3]`) asserting parity with the original scan *before* deleting `HasAncestorInNodeVisits`. `GetFromBack(1)` is genuinely O(1) (RefSemiDeque fast path), so the complexity goal was always reachable — only my formula was wrong.

**This fix changes output**, so per the skeptics it lands in **Step 1 (correctness)**, not Step 2 (extraction). Step 2's zero-diff guard is measured against the *end of Step 1*, not against today's code.

### LOH — corrected from a checkmark to a stated cost

My unqualified "no LOH concern" is **downgraded.** `NodeVisit<MergeNode<TLeft,TRight>>` is ~40 bytes even for **reference-type** `TLeft`/`TRight` (Mode 4 + {Left 8 + Right 8 + 2 bools padded} + VisitCount 4 + Position 8). `RefSemiDeque` caps partitions at `MaxPartitionSize = 4096` **elements** (line 40), so a full partition is ~164KB — **~2× over the 85KB LOH threshold at the default reference case**, before any value-type payload. For **DFT** this is bounded by depth (needs a 4096+ deep chain) → tail risk. For **BFT** a Θ(n)-wide level with n ≥ 4096 distinct merged nodes **will** allocate multi-hundred-KB LOH partitions — exactly the "wider frames hit 85KB LOH" regression originally flagged. This does **not** violate the "no per-node heap alloc" criterion (the deque is one amortized array; frames are structs; `AdvanceFrontier` returns by `ref`). It is a separate, real cost. **Mitigation, to decide during implementation:** (a) lower `MaxPartitionSize` for the merge-frame type so 4096×sizeof stays sub-85KB (≤ ~20 bytes/element implies a smaller cap), (b) split `MergeNode` storage so the hot deque holds ≤20-byte frames, or (c) explicitly accept LOH partitions as a small bounded long-lived set. Do not ship criterion #3 as an unqualified checkmark.

### Criteria confirmation (DFT) — what survives the red-team

1. **O(n): HOLDS, conditional on correctness.** With the scan replaced by the O(1) `Count` test, every step is O(1) amortized (O(1) inner reads, O(1) push/peek/pop, amortized unwind — each frame popped once). `GetFromBack`/`Count` confirmed O(1) (no hidden scan). Conditional because the claim is about the *corrected* algorithm.
2. **No whole-tree caching: HOLDS.** `_NodeVisits` holds only root→current (`Unwind` pops on backtrack) → working set O(depth); each inner DFT treenumerator is O(depth).
3. **No per-node heap alloc: HOLDS, with the LOH cost stated above.** Structs + reused amortized array + by-`ref` return + stack-local `InnerSnapshot`. The LOH partition cost is a separate, acknowledged item, not a per-node allocation.

**Ref-into-deque safety (confirmed by the perf skeptic):** `Publish(ref AdvanceFrontier(...))` returns a `ref` into the deque backing array; `RefSemiDeque` partitions are **append-only, never reallocated**, so a held `ref` survives a subsequent `AddLast` growth (unlike `List<T>`). Discipline note: publish must consume the `ref` before any further `AddLast` in the same step (the sketch does). In the async variant the by-value `InnerSnapshot` is **mandatory** — never hold a `ref` to `_LeftTreenumerator.Node` across the right-side `await`.

---

## 3. DFT vs BFT — one shared zip-core, or split?

**The zip model is correct and is the right framing.** `Union` is a merge-join of two `NodePosition`-ordered visit streams: advance the side(s) whose pending head is the join-extremum, emit one merged visit, join on equal position, pass through on an unmatched one. `Intersection`/`Subtract`/`SymmetricDifference` are pure downstream `MergeNode`-payload filters — confirmed in production (`Intersection = Union(...).PruneBefore(!HasLeftAndRight)`).

**Split the cores. Do NOT build a shared `MergeJoinCore`.** All three skeptics agree, and the divergence is *strictly larger* than Where's (which already split):

1. **Inverted comparator.** DFT selects the **min** position (lines 143–145, `SiblingIndex <=`); BFT selects the **max** (`>=`). DFT descends/unwinds; BFT fans out by breadth.
2. **Driver discipline differs structurally.** DFT: stack push-back + pop-when-deeper. BFT: deque add-back / overwrite-front / retire-when-front-fully-visited.
3. **BFT-only visit choreography.** BFT must manufacture between-children parent revisits and suppress the inner's redundant ones (the merge analogue of `WhereBreadthFirstTreenumerator._PendingParentVisit`/`_ConsumeNextInnerParentVisit`). DFT gets parent revisits free from stack unwinding.

A parameterized core would parameterize **exactly** the complexity-bearing parts (min-vs-max, driver discipline, BFT choreography), leaving only `MergeNode` construction + `InnerSnapshot<T>` genuinely shared — trivial residue. **Share those two helpers; split the path core.**

### BFT feasibility — honest verdict: ACHIEVABLE, no wall

A correct, **O(n)-total, O(max-merged-level-width) working set, no-per-node-alloc** BFT merge is achievable. Both inners are themselves streaming BFT treenumerators holding O(level-width); the merge zips them by the totally-ordered `NodePosition`. The union of two level-*k* frontiers is itself a level-*k* frontier of width ≤ |left_k| + |right_k| ≤ merged_k width. The merge never holds two non-adjacent levels. **The Where reorder wall does not apply:** it was (a) a *time* wall from an O(N²) skipped-ancestor scan, and (b) merge has **no depth compression at all** (both sources carry correct positions).

**Corrections folded in:**
- **Dropped:** my "BFT was O(N²) before the prefix carry" line. The perf skeptic is right — that was copy-pasted from the Where analysis and **does not apply**: merge BFT has no skipped-ancestor scan. The honest statement is **merge BFT runtime is O(n) modulo the correctness bugs**; per-step work is O(1) (queue add / remove-front / indexer reads).
- **Stated precisely:** criterion #2 for BFT is **O(max merged level width)**. For star/wide trees that is **Θ(n)** — *permitted* by the success criteria (the criterion forbids *whole-tree caching*, not O(width) frontiers), and *not* sublinear. A reviewer must not mistake O(width) for sublinear nor for whole-tree caching.

**The deferred-visit machinery** mirrors BFT Where: `_PendingParentVisit` (manufacture the front's revisit between every enqueuing merged-child slot — the fix for the `[0]` single-slot bug), `_ConsumeNextInnerLeftParentVisit`/`…Right` (suppress a source's now-redundant between-children visit — the fix for the `VisitCount` shortcut), and a BFT `SkipSiblings`/`SkipNodeAndDescendants` cross-propagation pair (with `SkipNodeAndDescendants` checked **first**). This is O(width) state, O(1)/step. No wall.

---

## 4. ALTERNATIVE MODELS CONSIDERED & REJECTED

Recorded so the dead ends are not re-explored (mirroring how the project records spikes).

- **ALT-1 — synthetic merged `IChildEnumerator` co-traversal. BLOCKED — same gap as Where.** `ITreenumerable<T>` exposes only `GetDepthFirstTreenumerator()`/`GetBreadthFirstTreenumerator()` (the interface is 7 lines). `IChildEnumerator` factories are supplied **only by leaf sources** and are never reachable from an operator holding two `ITreenumerable`s. An operator sees the *visit stream*, not a child-pull seam. **Do not re-explore.**
- **ALT-2 — feed the base engine a merged node source. BLOCKED for the streaming contract.** A `childEnumeratorFactory: NodeContext<MergeNode> → IChildEnumerator<MergeNode>` would have to navigate both inner trees to the co-located node and zip their children — i.e. random-access child-pull the abstraction does not provide. Reachable only by first materializing both inners (→ ALT-3).
- **ALT-3 — materialize both operands, structurally overlay into a `PreorderTree<MergeNode>`, let the existing base engine traverse it. VIABLE BUT EAGER.** `Materialize.cs` proves the round-trip. This would **delete both bespoke merge treenumerators and every bug in them** and keep `Intersection`/`Subtract`/`SymmetricDifference` as downstream filters — but it is O(n) eager materialization (working set O(n_left + n_right)), violating criterion #2 and breaking infinite/lazy sources (Collatz etc.). **This is the dominant path *if and only if* lazy/streaming Union is not a hard requirement** — a load-bearing assumption I originally left unstated. **Decision required before Step 0** (see Open Questions). Regardless of that decision, **build ALT-3 now as the independent oracle** (Step 0): it is the strongest available independence (different engine, zero shared code with the merge treenumerators).
- **ALT-4 — one shared zip-core parameterized by a `PositionComparer` + driver discipline. REJECTED** (see §3): the parameterization points carry all the complexity.

---

## 5. RECOMMENDED SEQUENCE

Oracle first → DFT correctness → algorithm-preserving extraction → BFT correctness → BFT extraction. Mirrors the Where rework; separates the two orthogonal concerns (correctness vs. state relocation). **Do not advance until a step's gate is green.**

| # | Step | Validation gate |
|---|------|-----------------|
| **0** | **Build the merge oracle AS ALT-3, and settle two contracts.** Materialize left and right, zip their children **by sibling-index path** using **only `List` operations / the base engine — never any merge-treenumerator code**, emit a `PreorderTree<MergeNode>`, traverse via the trusted base engine. **Decide and document two contracts here, as named expected outputs, not as scan outputs to match:** (1) single-sided `SkipSiblings` semantics (does `SkipSiblings` on a side-exclusive node prune the *other* tree's co-positioned siblings?); (2) `SkipNodeAndDescendants` precedence. **Key on the full sibling-index PATH from root** (not `(depth,sibling)` — merge zips two independently-positioned trees where `(0,1)` under root-0 collides with `(0,1)` under root-1) **AND a projection that asserts `HasLeft`/`HasRight` directly** (Intersection/Subtract/SymmetricDifference are pure flag filters — both keying changes move to Step 0, *not* Step 4). Must be **in-process** (left×right squares the case count; `[DynamicData]` overwhelms discovery). Cache deserialization and per-(left,right) oracle strings. | Oracle compiles, runs in-process with zero host-overwhelm, and is RED against current code (the baseline). The two contract decisions are written down. |
| **1** | **DFT correctness pass, no refactor.** Fix in the existing class: multi-level backtrack `max`-collapse; `SkipNode` child-promotion + sibling renumber; `SkipNodeAndDescendants`-before-`SkipNode` precedence; divergent-shape lockstep; and the O(n²) scan → the **`Count`-based** O(1) effective-root test (with the `[0,2,3]` non-contiguous-stack parity regression test landing *before* the old scan is deleted). | `DepthFirstMatchesOracle` green for `TraverseAll` across the curated mismatched-shape set (left-deeper, right-wider, one-empty, empty-empty) **plus the four named DFT failure scenarios** (`a(b(c))∪0`; `a(b,c)∪0(1,2)` SkipNode; `a,b,c∪0` SkipSiblings; `a(b)∪0(1)` SkipNodeAndDescendants), then green under the 2-deep `(node,strategy)` cross-product. Re-enable the 14 `UnionTests` rows as a human-readable smoke set. |
| **2** | **Extract `MergeDepthFirstPath` — algorithm-preserving.** Move `_NodeVisits`→`RefSemiDeque`, `Select`, `Unwind`, `AdvanceFrontier`, `CurrentIsEffectiveRoot` into the struct core (non-`readonly` field; return frame by `ref`); introduce `InnerSnapshot<T>`; keep the seam + finished flags + `SkipSiblings` tokens + output quad in the driver. | **Byte-identical oracle result vs. the END of Step 1** (NOT vs. today's code — the O(1) fix legitimately changed output in Step 1). Zero-diff is the NO-GO guardrail. Optional BenchmarkDotNet check on a **clean worktree** (no nested prototype worktrees) confirming no regression. |
| **3** | **BFT correctness pass — HIGHEST-RISK STEP.** Net-new algorithm invention with no correct version to diff against (the merge analogue of the hardest part of BFT Where). Replace the `VisitCount` shortcut with merged-child-slot accounting; add `_PendingParentVisit`/`_ConsumeNextInner*ParentVisit`; add BFT `SkipSiblings`/`SkipNodeAndDescendants` cross-propagation (compound checked first); add multi-level retire. Migrate `Nito.Deque`→`RefSemiDeque`. | `BreadthFirstMatchesOracle` green for the same curated set + strategy cross-product, **AND the cross-engine invariant as a HARD GATE: DFT and BFT must produce the same visit *multiset* (different order).** This is the best available independent check for a step with no correct baseline. |
| **4** | **Extract `MergeBreadthFirstPath`** (algorithm-preserving), then widen the corpus toward `AllTreeStrings × AllTreeStrings` for both engines. | Both engines green on the widened corpus with the flag-asserting projection from Step 0. |

The user asked to **analyze DFT first** — Steps 0–2 are the DFT spine and land before any BFT work.

---

## 6. OPEN QUESTIONS

1. **Is lazy/streaming Union over (possibly infinite) sources a HARD requirement? (decide before Step 0.)** This is the load-bearing assumption that forces the bespoke treenumerator to exist at all. If **no**, ALT-3 (materialize + overlay + base engine) dominates the entire plan — delete both treenumerators and all their bugs. If **yes**, proceed with the sequence above.
2. **Single-sided `SkipSiblings` contract (Step-0 deliverable, architect-level).** Does `SkipSiblings` on a left-only node prune the right tree's co-positioned siblings? The oracle prunes one overlay tree; the engine dual-forwards to two inners. A *semantic decision*, not a bug — decide and encode identically in oracle and engine before Step 1, or Step 1 chases a phantom.
3. **Which O(1) effective-root form is correct?** `_NodeVisits.Count == 1` (if the merge path keeps accepted-only frames, like `DepthFirstPath`) vs. a running shallowest-live-frame depth (if it retains skipped frames). Settle empirically in Step 1 via the `[0,2,3]` parity test before deleting the old scan.
4. **LOH partition sizing.** Compute `sizeof(NodeVisit<MergeNode>)` for the reference-type and largest expected value-type cases; choose mitigation (a)/(b)/(c) from §2. Especially relevant for BFT star trees (Θ(n)-wide levels).
5. **Comparator boundary at equal-depth / different-sibling ties.** DFT `<=` (lower-sibling-first); BFT `>=`. The most likely failure surface. Stress: at one level, left siblings {0,2}, right {1} — verify merged renumbering and both-vs-one inclusion per index.
6. **`HasLeft`/`HasRight` invisible under `$"{Left}{Right}"`.** Closed by the Step-0 flag-asserting projection; until then shape+values are validated but not the flags the derived operators depend on. Attack: flags wrong but concatenation coincidentally right.
7. **Is `NodePosition` a total order across divergent subtrees?** Confirm the engine never compares positions from divergent subtrees (left `(0,1)` under root-0 vs. right `(0,1)` under root-1) — benign only because a side stops advancing once it diverges. Canonical stress: `a(d(e)),b,c ∪ 0,1,2(3(4))`.

---

### Relevant files (absolute)

- `/c/Users/jason/source/repos/Copse/src/Copse.Linq/Treenumerators/StructuralMerge/StructuralMergeDepthFirstTreenumerator.cs`
- `/c/Users/jason/source/repos/Copse/src/Copse.Linq/Treenumerators/StructuralMerge/StructuralMergeBreadthFirstTreenumerator.cs`
- `/c/Users/jason/source/repos/Copse/src/Copse.Linq/Treenumerators/StructuralMerge/MergeNode.cs`
- `/c/Users/jason/source/repos/Copse/src/Copse.Core/NodePosition.cs`
- `/c/Users/jason/source/repos/Copse/src/Copse/RefSemiDeque.cs` (partition cap `MaxPartitionSize = 4096`, line 40 — LOH analysis)
- `/c/Users/jason/source/repos/Copse/src/Copse/Treenumerators/DepthFirstPath.cs` (pattern to mirror; `Count==1` effective-root idiom at line 168)
- `/c/Users/jason/source/repos/Copse/src/Copse.Linq/Treenumerators/Filter/WhereBreadthFirstTreenumerator.cs` (`_PendingParentVisit`/`_ConsumeNextInnerParentVisit` to mirror for BFT merge)
- `/c/Users/jason/source/repos/Copse/src/Copse.Linq.Tests/UnionTests.cs` (14 commented oracle rows; BFT `[Ignore]`d line 161)
- `/c/Users/jason/source/repos/Copse/src/Copse.Linq.Tests/Where2InProcessScan.cs` (scan to model)
- `/c/Users/jason/source/repos/Copse/src/Copse.Linq/Materialize.cs` (proves the ALT-3 round-trip for the oracle)
- New: `/c/Users/jason/source/repos/Copse/src/Copse.Linq.Tests/MergeInProcessScan.cs`
