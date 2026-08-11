# Walker Use-Case Catalog

**Status:** Working document — the evidence-gathering step ruled in
[WALKER_DESIGN.md](WALKER_DESIGN.md) §7. Call sites first, contract second.
**Branch:** `experimental/walker`

Every entry mocks up *the calling code we would want to write*, then classifies it:

- **Layer** — which floor of the region → walk → sequence tower the call site
  consumes at (a scalar result is classified by the layer it folds).
- **Re-enters source?** — would this code want to jump back into the live
  structure afterward? (The provenance evidence, design doc §5.)
- **Cost** — the lens/order price column the call site pays.

The syntax is a **strawman**. `tree.GetWalker()` / `dag.GetWalker()` yield a
walker; axes are methods on it taking a node handle; `Upstream`/`Downstream`
return regions; `.Walk(Order.X)` commits an order and yields a treenumerable /
dagnumerable; `.ToTree()` / `.ToDag()` reify; `.Nodes` enumerates a region as an
unordered set. The catalog exists to pressure-test this surface — expect the
spelling to move.

Harvest sources:

1. The 2016 library's 29 `TreeWalkerExtensions` — each one a use case with ten
   years of validation.
2. `OwnershipStructureScenarioTests` on `experimental/dag` — the origin
   problem's real queries, re-asked walker-shaped.
3. XPath's axis list as a completeness checklist.
4. Walker-only classics no stream can serve.

---

## A. Tree axes — the 2016 surface, re-spelled

### UC-01 Breadcrumb trail (`GetAncestors` / `GetAncestorsAndSelf`)

The UI question: where am I? Path from the current node back to the root,
displayed root-first.

```csharp
var breadcrumb = walker.AncestorsAndSelf(current)
  .Reverse()
  .Select(node => node.Name)
  .ToList();
```

**Layer:** sequence · **Re-enters source?** No · **Cost:** O(depth), zero
allocation (pure axis, struct lens).

### UC-02 Find the root (`GetRoot`)

```csharp
var root = walker.Root(current);   // last element of AncestorsAndSelf
```

**Layer:** sequence (folded to a node) · **Re-enters source?** No ·
**Cost:** O(depth).

### UC-03 Single-step navigation (`GetParent` / `TryGetParent` / `HasParent` / `HasChildren`)

The smallest possible consumers — one step, or a peek.

```csharp
if (walker.TryGetParent(current, out var parent)) { ... }
var isLeaf = !walker.HasChildren(current);
```

**Layer:** sequence (degenerate: length ≤ 1) · **Re-enters source?** No ·
**Cost:** O(1). These are the contract primitives showing through, and that is
fine.

### UC-04 Indexed and keyed child access (`GetChildAt` / `GetChildrenByKey`)

The XML-shaped consumers: children are *ordered* on the tree side, and sometimes
addressed by key.

```csharp
var second = walker.ChildAt(current, 1);
var titles = walker.ChildrenWhere(current, child => child.Tag == "title");
```

**Layer:** sequence · **Re-enters source?** No · **Cost:** O(degree). Flags a
contract fact: **tree children are an ordered sequence; DAG in-edge groups are
a distribution** — the collapse law has an ordering rider on the tree side.

### UC-05 Sibling navigation (`GetSiblings` / `GetPrecedingSiblings` / `GetFollowingSiblings`, `AndSelf` variants)

XPath's `following-sibling` / `preceding-sibling`, validated by six of the 29
old extensions.

```csharp
var next = walker.FollowingSiblings(current).FirstOrDefault();
```

**Layer:** sequence · **Re-enters source?** No · **Cost:** one parent step +
O(degree). Note the old library needed both `AndSelf` variants *and* an
`ExcludeOption` enum — pick **one** self-inclusion grammar this time.

### UC-06 Subtree search (`GetDescendants` / `GetDescendantsOfType`)

The Roslyn-shaped consumer — and the entry that shows the whole tower in one
use case, because "descendants" means three different things at three prices:

```csharp
// As an unordered set: find all method declarations somewhere under this node.
var methods = walker.Descendants(typeDecl).Nodes.OfType<MethodDeclaration>();

// As a document-order sequence: XPath's descendant axis.
var inOrder = walker.Descendants(typeDecl).Walk(Order.DepthFirst).ToNodeSequence();

// As a region: keep composing before committing to anything.
var publicApi = walker.Descendants(typeDecl).Where(m => m.IsPublic);
```

**Layer:** all three, caller's choice · **Re-enters source?** Sometimes (a
found node is often a jump-off point) · **Cost:** set = O(subtree) no order
guarantee; walk = O(subtree) ordered; region = free until consumed.

### UC-07 Leaves under a node (`GetLeaves`)

```csharp
var files = walker.Descendants(dir).Nodes.Where(n => !walker.HasChildren(n));
```

**Layer:** sequence · **Re-enters source?** No · **Cost:** O(subtree). A
derived spelling, not a primitive — `GetLeaves` earns extension status, not
contract status.

### UC-08 Root-to-leaf paths (`GetBranches`) — the path-semantics canary

```csharp
var branches = walker.EnumerateBranches(current);   // IEnumerable<Path<TNode>>
```

**Layer:** sequence (of paths) · **Re-enters source?** No · **Cost:** on a
tree, O(paths) = O(leaves) — benign. **On a DAG this exact operation is
exponential.** The 2016 name `GetBranches` gives no warning because trees never
needed one; the ported name must be loud (`EnumeratePaths…`). This is the
set-vs-path divergence (design doc §5) caught in a real, shipped API.

### UC-09 Measures (`GetDepth` / `GetHeight` / `GetDegree` / `GetLevel`)

```csharp
var depth = walker.Depth(current);    // ancestors, counted — O(depth)
var height = walker.Height(current);  // deepest descendant — O(subtree)
```

**Layer:** sequence, folded to scalars · **Re-enters source?** No · **Cost:**
depth is cheap, height is a subtree sweep — the asymmetry should be visible in
the docs, invisible in the API.

### UC-10 The classic traversals (`PreOrderTraversal` / `PostOrderTraversal` / `LevelOrderTraversal`)

The 2016 library's biggest methods — and in the new design they stop being
special: a traversal is just an order-commit on the everything-region.

```csharp
var preorder = walker.Descendants(root).Walk(Order.DepthFirst);   // a treenumerable!
var byLevel  = walker.Descendants(root).Walk(Order.LevelOrder);
```

**Layer:** walk · **Re-enters source?** No · **Cost:** O(subtree), frontier
state per the order. The 2016 versions went straight to `IEnumerable` because
no algebra existed to consume an ordered-structured stream. Now one does — see
section C.

---

## B. Relations between nodes — the walker-only classics

### UC-11 Is-descendant-of / is-ancestor-of

```csharp
var inScope = walker.Ancestors(current).Contains(scopeRoot);
```

**Layer:** sequence (early-exit fold) · **Re-enters source?** No · **Cost:**
O(depth) with early exit. No stream can answer this for two arbitrary handles
without a sweep.

### UC-12 Lowest common ancestor

```csharp
var ancestorSet = walker.AncestorsAndSelf(m).ToHashSet();
var lca = walker.AncestorsAndSelf(n).First(a => ancestorSet.Contains(a));
```

**Layer:** sequence · **Re-enters source?** Often — the LCA is usually a
jump-off point for the next query · **Cost:** O(depth(m) + depth(n)). Candidate
for a built-in (`walker.LowestCommonAncestor(m, n)`) since the naive spelling
allocates a set the primitive-aware version can avoid.

### UC-13 Distance and path between two nodes

```csharp
var distance = walker.Depth(m) + walker.Depth(n) - 2 * walker.Depth(walker.LowestCommonAncestor(m, n));
var path = walker.PathBetween(m, n);   // up to the LCA, down to n
```

**Layer:** sequence · **Re-enters source?** No · **Cost:** O(depth). The
"distance between two files in a repo tree" question; unanswerable in the order
algebra at any price short of a rootfix sweep.

---

## C. Mid-structure sweeps — the walk layer earning its floor

The uniquely new power: position with the walker, then hand the walk to the
existing operator algebra. Neighborhood-priced scans.

### UC-14 Aggregate one subtree (Leaffix over `Descendants(n)`)

Directory size without sweeping the file system root:

```csharp
var totalBytes = walker
  .Descendants(projectDir)              // region: just this subtree
  .Walk(Order.DepthFirst)               // commit an order -> a treenumerable
  .LeaffixScan((dir, children) => dir.FileBytes + children.Sum(child => child.Value))
  .Root().Value;
```

**Layer:** walk · **Re-enters source?** No · **Cost:** O(subtree) — the point:
*not* O(tree).

### UC-15 Rootfix from the middle

Accumulated path state (namespace qualification, inherited permissions,
cascading styles) starting at an arbitrary node, seeded by an upward peek:

```csharp
var seed = walker.Ancestors(node).Aggregate(Permissions.Default, (acc, a) => acc.Apply(a.Grants));
var effective = walker
  .Descendants(node)
  .Walk(Order.DepthFirst)
  .RootfixScan(seed, (permissions, child) => permissions.Apply(child.Grants));
```

**Layer:** sequence (the upward seed) + walk (the downward scan) — a
two-floor call site · **Re-enters source?** No · **Cost:** O(depth) +
O(subtree).

### UC-16 Prune and process within a region

The full operator algebra composes onto a walk, because a walk *is* a
treenumerable:

```csharp
var visible = walker
  .Descendants(sectionRoot)
  .Walk(Order.DepthFirst)
  .PruneBefore(node => node.IsCollapsed)
  .Select(node => node.Render());
```

**Layer:** walk · **Re-enters source?** No · **Cost:** O(rendered), thanks to
the prune.

---

## D. DAG regions — the ownership showcase re-asked

The showcase (`OwnershipStructureScenarioTests`) answers its questions with
whole-structure sweeps, which is right for reports over everything. These are
the *same domain's* neighborhood-shaped questions — the ones `TakeUpstreamWhere`
was groping toward.

### UC-17 Who ultimately owns this entity? (`Upstream` closure)

```csharp
var owners = walker.Upstream(opCo).Nodes.Where(entity => entity.ContributionCents > 0);
// { FundA, FundB } — through the diamond, deduped: set semantics.
```

**Layer:** region → set · **Re-enters source?** No · **Cost:** O(reached),
memoized closure, one allocation for the memo.

### UC-18 Everything a fund touches (`Downstream` closure)

```csharp
var exposure = walker.Downstream(fundA).Nodes.Sum(entity => entity.HoldingCents);
```

**Layer:** region → set fold · **Re-enters source?** No · **Cost:** O(reached).

### UC-19 Reachability membership, early-exit

```csharp
if (walker.Upstream(blockedCo).Contains(fundB)) { /* FundB has blocked exposure */ }
```

**Layer:** region (membership only — never enumerated) · **Re-enters source?**
No · **Cost:** anytime — the lazy closure grows only until the answer is found.
This call site is why closure membership must be *monotonic and interruptible*,
not computed eagerly on region construction.

### UC-20 What lies between? — region intersection

Everything on any route from FundA to OpCo; then, the blockers among it:

```csharp
var between = walker.Downstream(fundA).Intersect(walker.Upstream(opCo));
var blockers = between.Nodes.Where(entity => entity.IsBlocker);
```

**Layer:** region (composed!) · **Re-enters source?** Yes — a found blocker is
exactly where you jump into the live structure next · **Cost:** two memos +
O(1)-per-step intersection lens.

**Catalog discovery:** region ∩ region was not in the design doc's lens list,
and it works precisely because a region is an **edge set, not a node set**:
`Downstream(a).edges ∩ Upstream(b).edges` = the edges lying on a→b paths. The
edges-atomic ruling from the DAG contract pays off again.

### UC-21 `TakeUpstreamWhere`, decomposed

The operation that started this workstream, in its uncoupled spelling:

```csharp
var frozen = walker.Upstream(startEntity).Where(entity => !entity.IsBlocker).ToDag();
```

**Layer:** region → reify · **Re-enters source?** **Yes** — see UC-27 ·
**Cost:** O(reached) once, flat-store steps thereafter.

### UC-22 One fund's effective stake in one entity — path semantics, loudly

The showcase computes effective ownership for *all* nodes with a
`SourcefixScan` sweep. The neighborhood-shaped version — one fund, one entity —
is a **path product**, and it must wear the loud name:

```csharp
var stake = walker
  .EnumeratePathsBetween(fundA, opCo)      // loud: path semantics, exponential in general
  .Sum(path => path.Edges.Aggregate(1m, (product, edge) => product * edge.Weight));
// FundA -> HoldCo (1.00) -> JV (0.60) -> OpCo (1.00) : one path, stake 0.60.
```

**Layer:** sequence (of paths) · **Re-enters source?** No · **Cost:**
exponential in the worst case, small in real ownership structures — the cost is
*structure-dependent* and the docs must say so. UC-08's canary, now with money
on it.

### UC-23 One fund's NAV — region-restricted sweep

The showcase's per-fund lookthrough prunes the *other* root and sweeps. The
walker spelling restricts to the fund's region and scans — and these two
spellings must agree:

```csharp
var nav = walker
  .Downstream(fundA)                       // region: FundA's routes only
  .Walk(Order.Topological)                 // in-degree accounting within the region: memo price
  .SinkfixScan((entity, upflows) => entity.HoldingCents + upflows.Sum(u => u.Value * u.Edge))
  .SourceValues().Sum();
```

**Layer:** region + walk · **Re-enters source?** No · **Cost:** O(reached) +
the topological-order memo — the priciest order-commit, visibly.

**Catalog discovery:** *region-restricted scan ≡ prune-the-complement sweep.*
The showcase's `PruneBefore(other fund)` + `SourcefixScan` computes the same
answer at O(structure); this computes it at O(reached). That equivalence is
free conformance-test material — the streaming spelling is the **oracle** for
the walker spelling, exactly the builder+oracle pattern the DAG spike used.

### UC-24 Attribution as a transpose — the free lens

UC-23 runs a Sinkfix (upward) scan. The same call site can be spelled as a
*downward* scan over the transposed walker — swap `GetInEdges`/`GetOutEdges`
and Sourcefix/Sinkfix trade places:

```csharp
var nav = walker.Transpose()
  .Downstream(opCo)                        // was Upstream, before the flip
  .Walk(Order.Topological)
  .SourcefixScan(/* the same lambda */ ...);
```

**Layer:** region + walk · **Re-enters source?** No · **Cost:** the transpose
itself is free — two method references trade places. Contrast: Transpose in the
order algebra is a pending phase-4 problem. Duality receipts.

---

## E. Reification, interchange, and provenance

### UC-25 The documented materialization: stream → store → walker

The deferred surface has no navigation; the conversion is explicit and priced:

```csharp
var store = deferredTree.ToStore();        // one sweep, documented (laziness policy)
var walker = store.GetWalker();            // (store, ordinal) — zero further allocation
```

**Layer:** entry to the tower · **Re-enters source?** n/a · **Cost:** O(n)
once. The core `ITreenumerable`/`ITreenumerator` contracts are untouched.

### UC-26 The degenerate tower — the existing library, recovered

```csharp
var stream = store.GetWalker().Descendants(store.Root).Walk(Order.DepthFirst);
// ≡ the store's own preorder treenumerable: everything-region, walked from the root.
```

**Layer:** walk · **Re-enters source?** No · **Cost:** identical to the native
stream — and it *must* be, which makes this a conformance pin: the walker's
everything-walk and the store's native traversal must agree element-for-element.

### UC-27 Analyze frozen, jump back live — provenance earning its default

The ownership workflow that wants provenance: freeze the blocker subgraph,
analyze the cheap copy, re-enter the live structure at the finding.

```csharp
var frozen = walker.Upstream(opCo).ToDag();                     // provenance carried
var suspect = frozen.GetTopologicalOrder().First(n => n.Value.IsBlocker);
var live = walker.At(frozen.SourceHandle(suspect));             // re-enter the live graph
var currentInflows = live.InEdges();                            // fresh, not frozen
```

**Layer:** region → reify → re-entry · **Re-enters source?** **Yes — the whole
point** · **Cost:** provenance is an ordinal per node, nearly free to carry,
impossible to reconstruct if dropped.

### UC-28 Detached reification — the isolation case

```csharp
var snapshot = walker.Downstream(fundA).ToDag(Reify.Detached);
mutableSource.AddEdge(...);        // live walkers fail fast; the snapshot does not care
```

**Layer:** region → reify · **Re-enters source?** No — deliberately · **Cost:**
same as UC-27 minus the handles. The fail-fast staleness rule (design doc §5)
is exactly why this variant exists.

---

## F. Native and calculated adjacency — no materialization anywhere

### UC-29 The Collatz walker — the 2016 library's showpiece, ported

Adjacency as functions; the delegate convenience provider; nothing stored:

```csharp
var collatz = TreeWalker.Create<long>(
  parent: n => n == 1 ? default(long?) : (n % 2 == 0 ? n / 2 : 3 * n + 1),
  children: n => CollatzPredecessors(n));

var trajectory = collatz.AncestorsAndSelf(27).ToList();   // 27's 111-step ride to 1
```

**Layer:** sequence · **Re-enters source?** No · **Cost:** O(steps), zero
materialization — the proof that the walker's requirement is *adjacency*, not
storage.

### UC-30 The file system walker — native object adjacency

```csharp
var fileSystem = TreeWalker.Create<DirectoryInfo>(
  parent: dir => dir.Parent,
  children: dir => dir.GetDirectories());

var depth = fileSystem.Depth(someDir);
var repoRoot = fileSystem.AncestorsAndSelf(cwd).First(dir => fileSystem.Children(dir).Any(d => d.Name == ".git"));
```

**Layer:** sequence · **Re-enters source?** Yes — found directories get used ·
**Cost:** adjacency calls hit the OS; the walker adds nothing on top.

### UC-31 The stateful cursor — interactive navigation

The TreeView/debugger shape: hold a position, step on user input.

```csharp
var position = walker.At(selectedNode);
listView.Items = position.Children().Select(child => child.Render());
breadcrumbBar.Items = position.AncestorsAndSelf().Reverse();
```

**Layer:** sequence, but through a *position-holding* surface · **Re-enters
source?** Continuously — the cursor lives inside the structure · **Cost:**
O(step). Whether `At(n)` returning a position object is v1 surface or sugar
over the axes is an open call the catalog flags but does not settle.

---

## G. Capstone — the spanning subtree of a node set (every floor at once)

### UC-32 Minimum spanning subtree of k nodes, over a filtered pipeline

The end-to-end workflow that exercises every part of the design in one arc. Given a
*derived* tree — a filtered pipeline, so the structure being walked exists nowhere
but as composition — and k nodes of interest found in it, produce the **unique
minimal subtree containing all of them**, as a treenumerable again, with
provenance back to the source.

(Terminology: on a tree this is the Steiner tree of the node set, and it is
unique — the union of the paths from each target up to the targets' collective
LCA — so "minimum" costs nothing. That uniqueness is exactly what does *not*
survive the DAG, see the fence below.)

What the calling code wants to write:

```csharp
// The order algebra half: the tree is DERIVED -- filtered with child promotion,
// pruned at depth. It has no storage; it is a composition.
var relevant = sourceTree
  .Where(context => context.Node.IsRelevant)
  .PruneAfter(context => context.Position.Depth == 5);

// The documented escalation, organic: under the lazy-Materialize law the first
// consumer pins the layout, and this use case's first act -- the handle
// acquisition sweep just below -- is preorder-shaped, so the ancestry-cheap
// capture (subtree spans, O(1) ancestry on ordinal handles) arrives with
// nobody choosing it. MaterializeWalkable(BufferLayout.Preorder) remains the
// escape hatch for when the first act and the query mix disagree -- see "How
// the escalation chooses" below.
var walkable = relevant.MaterializeWalkable();

// Handle acquisition: the ROWID SCAN. Iterates HANDLE-SPACE (on a store: 0..n-1) and
// derefs each -- every (handle, value) row of the labeling -- so predicates over
// values can pick out handles. The direction is the point: handles are enumerated,
// never computed from values, which is why equality appears in no signature -- the
// predicate is consumer code, and an outside-supplied target list becomes a
// consumer-side set INSIDE the lambda, built with whatever comparer the consumer
// likes. (Handles are opaque claim tickets: walkable-local, held not interpreted,
// passed back to the walkable that minted them. Naming ruled 2026-08-10: if the thing
// is a handle, call it a handle -- GetHandles() yields all handles, WithValues is the
// pair convenience, the pair is HandleAndValue.)
var targets = walkable
  .GetHandlesWithValues()
  .Where(pair => pair.Value.IsFlagged)
  .Select(pair => pair.Handle)
  .ToList();

// The packaged query: k handles in, a treenumerable out.
var spanningTree = walkable.GetSpanningSubtree(targets);
```

And its decomposition (revised by the 2026-08-10 walkthrough — the original union-fold
spelling survives below as the region-algebra equivalent):

```csharp
// 1. The upward fold: climb from each target to the collective LCA -- and RECORD the
//    climbs, because the visited handles ARE the spanning skeleton's node set. The
//    membership memo arrives as a BYPRODUCT of the fold; no extra pass exists.
//    (Preorder-store receipts: presorted targets collapse the whole fold to
//    LCA(first, last), and is-ancestor is O(1) span containment.)
var spanningRoot = targets.Aggregate(walkable.GetLowestCommonAncestor);

// 2. The view: three lenses over the SAME walkable -- no copy, no capture.
//      re-root    : GetRootAt(0) answers spanningRoot
//      clamp      : GetParent(spanningRoot) answers nothing
//      membership : child k of n survives iff it leads toward a target
var spanning = walkable.ReRoot(spanningRoot).WhereReachable(targets);   // strawman spelling

// 3. There is no step 3. The view IS a walkable, and a walkable IS a treenumerable --
//    the ladder's free direction. "A treenumerable is a bottled walk": a region and a
//    committed order, sealed behind the factory contract; the view just bottles new
//    ingredients over old terrain.
ITreenumerable<Entity> result = spanning;

// The explicit alternative ending: reify with provenance (UC-27) when you want a
// frozen copy whose handles map back -- view vs. record, the laziness policy's pair,
// the caller's choice.
var frozen = spanning.ToTree();
```

Region-algebra equivalent of step 2 (the provider-agnostic spelling; ∪ still demanded):
`targets.Select(t => walkable.GetPathRegion(spanningRoot, t)).Aggregate((l, r) => l.Union(r))`.

**The membership predicate is the memoized lens class, by law.** "Leads toward a
target" is a fact about the child's SUBTREE, not the child — and non-local predicates
need stored descendant-knowledge. Where it lives is the parent-information law's
mirror: in the **record** (preorder spans: binary search over sorted targets, zero
memo), in the **handle** (address prefixes: arithmetic), in the **function** (a
computed tree climbs from the target), or in a **memo** (the fold's recorded climbs,
O(Σ depth), already paid). Local predicates (the PruneAfter lens) stay in the free
class; the boundary between the classes is locality. The view relabels sibling
indexes (view-local positions, source handles — the streaming Where's own relabeling
rule applied to adjacency), and "view" never meant zero memory: it means memory
proportional to the REGION, source untouched.

**The one build dependency: the `Walk()` adapter** — a generic treenumerator driven
off any walkable's own answers. The PruneAfter lens borrowed its stream half from the
streaming operator; re-root + membership has no operator twin, so the view's
treenumerable citizenship is served by the adapter — the same machine as the
constant-space walkable-native engine, arriving with two jobs.

**Layer:** all of them, deliberately · **Re-enters source?** Yes — the result's
whole purpose is pointing back into the source structure · **Cost:** after
acquisition, the classic auxiliary-tree build is O(k log k) comparisons plus the
path walks — never O(tree). On preorder-store handles the constants collapse:
ordinal sort IS preorder sort, and is-ancestor is span containment, O(1).

**How the escalation chooses its layout** (ruled 2026-08-10): the walker
escalation mints no new knob — it inherits `Materialize`'s, both forms.

- **Organic** (`MaterializeWalkable()`): the walkable wraps whatever layout the
  source's own story produced — and under the lazy-Materialize ruling
  (WALKER_DESIGN.md §4, landed on main 2026-08-10) that story is one sentence:
  **the first consumer pins the layout.** Both forms of `Materialize` defer
  construction to first pull; the organic form defers the pin too, since first
  pull is the earliest moment it is knowable. A completed buffer keeps its
  `NativeLayout`; an adjacency-first use of the walkable (no dimension named)
  pins the walker default, preorder — though the realistic first act is the
  handle-acquisition sweep, which pins its own dimension organically. Because
  both layouts have walkable citizens, the organic form always succeeds and
  never transposes; what it does not promise is a *particular* axis-cost
  profile.
- **Deliberate** (`MaterializeWalkable(BufferLayout)`): the escape
  hatch for callers who truly know their query shape, riding
  `Materialize(layout)`'s existing guarantee — the argument is never ignored,
  a wrong-layout buffer is transposed *from the buffer* (O(n), source
  untouched, at-most-once enumeration preserved), "the layout IS the
  deliverable." Under the lazy ruling: the pin lands at call (free — it pulls
  zero nodes, and protects native-capture odds on a shared live memo), the
  O(n) construction lands at first pull. The parameter speaks STORAGE
  vocabulary (retyped from `TreeTraversalStrategy` 2026-08-10 — the layout is
  the deliverable, and it is also exactly what the walker caller reasons
  about): `Preorder` buys subtree spans and cheap ancestry; `LevelOrder` buys
  contiguous sibling runs and levels — the axis-cost table, named directly.

The choice is cost-only — the cross-family pins prove both walkables present
the identical tree — and revisable at O(n) from the buffer, so the knob is a
tuning decision with an escape hatch, not a commitment.

**The checklist — one use case, every part:**

| Step | Design machinery exercised |
|---|---|
| `Where` + `PruneAfter` upstream | The streaming operator algebra, untouched, feeding the walker a derived tree |
| `MaterializeWalkable()` | The documented escalation; laziness policy; query-shaped store choice |
| Handle acquisition (the rowid scan) | Value-space → handle-space bridge; predicates are consumer code, equality in no signature; handles = opaque claim tickets |
| LCA fold | The relations floor; plain `Aggregate` as the algebra — and the climbs recorded ARE the membership memo, free |
| The three-lens view (re-root, clamp, membership) | The region floor's direct spelling (∪ of path regions = the equivalent algebra spelling); membership = the memoized lens class, priced by the descendant-information law |
| The free upcast | The ladder's free direction: view IS walkable IS treenumerable ("a bottled walk"); streaming served by the `Walk()` adapter — the one build dependency |
| `ToTree()` with provenance | The alternative ending: reification, the interchange citizen, re-entry receipts — view vs. record, caller's choice |

**The compressed variant — the contraction lens's first named consumer.** The
auxiliary-tree of competitive-programming lore drops the pass-through nodes
(degree-2 chain nodes on connecting paths), keeping only targets and their
pairwise LCAs — at most 2k−1 nodes — with edges remembering the hop counts they
elide:

```csharp
var compressed = spanningRegion.ContractPassThrough();   // lookthrough lens, priced honestly
```

Until now the lookthrough cost class (design doc §4) had no concrete tenant;
this is it, and it arrives with its own justification for the buffer: a
compressed spanning tree queried repeatedly is exactly the "reify when the lens
stack goes lookthrough-heavy" case.

**The address-provider bonus.** On the deferred-address walker, the compressed
spanning *skeleton* is computable from handle arithmetic alone: sort the target
addresses lexicographically, and the longest common prefixes of adjacent pairs
ARE the internal nodes. No tree access until values are dereferenced — the XML
twig-join literature does precisely this over Dewey IDs, which is prior art both
for the algorithm and for the provider it runs on.

**The DAG fence — sharper than the path-semantics canary.** The same words on a
DAG — "minimum structure spanning k nodes" — name the **Steiner tree problem,
which is NP-hard**. The tree case is linear precisely because the spanning
subtree is unique; the diamond destroys uniqueness and with it tractability. So
the operation is tree-only by *complexity class*, not by implementation
laziness, and the API must say so. The polynomial DAG analog is a different
operation with different words: the multi-terminal generalization of UC-20's
between-region (⋃ of `Downstream(sᵢ)` ∩ ⋃ of `Upstream(tⱼ)`) — everything lying
on any route among the set — which is honest, cheap, and *not* minimal. UC-08
flagged names that silently change cost on the DAG; this one silently changes
complexity class, the loudest fence in the catalog.

---

## Tally and observations

### Layer distribution

| Layer consumed | Use cases | Count |
|---|---|---|
| Sequence | UC-01–05, 07–09, 11–13, 22, 29–31 | 15 |
| Walk | UC-10, 14–16, 26, and the walk halves of 23–24 | 6 |
| Region | UC-06, 17–21, 23–24, 27–28 | 10 |

**Every floor is occupied.** The sequence floor dominates by count (the 2016
library lives there), the region floor carries the DAG-native value, and the
walk floor is the thinnest but owns the capabilities neither other floor can
express (neighborhood-priced scans, UC-14/15/16/23). Provisional verdict: the
tower stands; no invented floors. UC-32 sits outside the table deliberately: it
consumes at every floor in one arc — the capstone check that the floors
*compose*, not merely that each is separately inhabited.

### Provenance tally

Re-entry wanted: UC-06 (sometimes), UC-12, UC-20, UC-21/27 (the workflow), UC-30,
UC-31 (continuously). Every reification case except the deliberate snapshot
(UC-28) wants its way back in. **Evidence supports provenance-by-default with a
detached variant** — the design doc's lean, now with receipts.

### New findings the catalog surfaced

1. **Regions are edge sets, not node sets** (UC-20, UC-23). Intersection and
   region-restricted scans only work cleanly because an edge is in
   `Downstream(a) ∩ Upstream(b)` iff it lies on an a→b path. Consistent with —
   and further evidence for — the DAG contract's edges-atomic ruling.
2. **Region intersection is a lens** the design doc's cost table did not list —
   O(1) per step over two memos. Union is no longer "presumably": UC-32's
   spanning region is a fold of path regions under ∪, so both combinators are
   demanded by real call sites.
3. **The oracle equivalence:** region-restricted scan ≡ prune-the-complement
   sweep (UC-23). Free conformance tests: the streaming spelling is the oracle
   for the walker spelling, in the DAG spike's builder+oracle tradition.
4. **Unordered set enumeration (`.Nodes`) is a real fourth consumption mode** —
   cheaper than any order-commit, no order guarantees, and most DAG-region call
   sites want exactly it. Decide whether it is its own surface or a
   `Walk(Order.Unspecified)`.
5. **Self-inclusion grammar:** the 2016 library paid for both `AndSelf`
   variants *and* an `ExcludeOption` enum. Pick one mechanism.
6. **Tree children are ordered; DAG in-edge groups are distributions** (UC-04).
   The collapse law needs an ordering rider on the tree side.
7. **The measures hide an asymmetry** (UC-09): `Depth` is O(depth) but `Height`
   is a subtree sweep. Same shape of API, very different price — docs must
   carry it.
8. **The cursor question** (UC-31): interactive consumers want a
   position-holding object. In or out of v1?
9. **The contraction lens has a tenant** (UC-32): `ContractPassThrough` — the
   compressed auxiliary tree — is the first named consumer of the lookthrough
   cost class, and it doubles as the motivating case for reify-when-
   lookthrough-heavy.
10. **A complexity-class fence, above the cost fences** (UC-32): spanning-of-a-
   set is unique and linear on trees, NP-hard on DAGs (Steiner). UC-08 caught
   names whose *cost* silently changes on the DAG; this catches a name whose
   *complexity class* changes. The DAG gets a different operation with
   different words (the multi-terminal between-region), never this name.
11. **Handle arithmetic can precede tree access entirely** (UC-32 on the
   address provider): the compressed spanning skeleton falls out of sorting
   target addresses and taking adjacent longest-common-prefixes — the XML
   twig-join technique over Dewey IDs — with the tree touched only to
   dereference values.

### What the catalog did *not* find

No use case wanted the walker to mutate topology. No use case wanted a
non-fail-fast answer over a mutated source. No use case needed the deferred
streaming surface to navigate. The invariants survived contact with the
evidence.
