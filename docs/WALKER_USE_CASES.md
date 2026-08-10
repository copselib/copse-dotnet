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
tower stands; no invented floors.

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
2. **Region intersection (and presumably union/difference) is a lens** the
   design doc's cost table did not list. O(1) per step over two memos.
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

### What the catalog did *not* find

No use case wanted the walker to mutate topology. No use case wanted a
non-fail-fast answer over a mutated source. No use case needed the deferred
streaming surface to navigate. The invariants survived contact with the
evidence.
