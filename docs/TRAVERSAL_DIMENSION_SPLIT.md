# Traversal Dimension Split & Serialization Redesign (design note)

> **Status: LARGELY LANDED (2026-07-05), on `feature/traversal-dimension-split`.** Shipped: the
> interface split, the flat family (stores + streams + treenumerables), de-engined memoize, the
> narrow operator spine, the `Invert`/`LeaffixScan` reworks, `PreorderTree`'s dissolution, and
> the header-free layout-named serializer. Still open (future passes): the layout-typed buffer
> interfaces + zero-copy mirror views, the `AsBreadthFirst()` re-scan opt-in, and the
> namespace-alignment leftovers. NOTE: this document is a session-by-session palimpsest
> ("discussed → decided → revised"); it is accurate but not yet rewritten into a clean as-built
> spec — that consolidation is itself a pending cleanup item.
>
> Motivating context: the post-Memoize serialization cleanup. The serializer discussion
> kept colliding with one structural question — what should happen when a consumer asks a
> source for a traversal order it cannot affordably provide — and the split is the answer
> that fell out.

## Pick-up decisions (2026-07-04, second session)

1. **The "layout streamers" are reframed as concrete treenumerables for flat-stored
   trees, and they live in `Copse`.** Stop thinking of them as stream adapters: the
   existing `Treenumerable<,,>` enumerates trees stored *hierarchically* (via the
   child-enumerator protocol); these enumerate trees stored in *flat* preorder or
   level-order form. Same citizenship, same package. Names by **layout vocabulary, not
   traversal vocabulary** (a layout can serve both dimensions when the store allows):
   `PreorderTreenumerable`, `LevelOrderTreenumerable`. Consequences:
   - The serializer-placement question dissolves (`Copse.SimpleSerializer` already
     references `Copse`).
   - The Linq→Copse edge *persists* (memoize replays instantiate `Copse` flat-family
     types) — a deliberate reversal of the ledger's "edge breaks anyway" payoff, sanctioned
     by the pay-for-itself rule; the edge is now honest ("Linq uses concrete
     treenumerables"), not incidental.
   - `PreorderTree` retires by **dissolving** into `PreorderTreenumerable` over a
     completed store (not by moving to `Copse.Trees`).
2. **Layout fixes the native dimension; store capability decides full vs narrow
   citizenship.** Each flat treenumerable always implements its native narrow interface
   (preorder → `IDepthFirstTreenumerable`, level-order → `IBreadthFirstTreenumerable`).
   Over a **random-access** store (arrays, memo capture, string) it implements full
   `ITreenumerable` (cross-order rides subtree-size hops / child spans — what the memo
   buffers already do). Over a **forward-only** store (reader over a file) it implements
   the narrow interface only. Since C# fixes interface lists at compile time, that means
   two concrete types per layout (one per capability tier). The store SPI mirrors
   `IChildEnumerator`'s role (the flat family's adapter protocol, defined in `Copse`) and
   splits along the same line — forward-only vs random-access — with the random-access
   shape carrying an "ensure index available" hook because the memo's store is
   random-access but *growing* during capture.
3. **Envelope: one layout per file.** Reconfirmed with a second argument: even with both
   layouts in one file, streaming the second section requires reading (not just parsing)
   past the entire first — the 10 GB case again. Tiny dependency-free header per file
   (layout axis + shape stats), no JSON dependency; JSON interop only ever as a separate
   package on real demand.
4. **One static axis, ever.** The dimension split spends the library's entire budget for
   axes expressed in operator signatures (~2× overloads on the narrow set, once). The
   capability lattice (TREE_CAPABILITY_INTERFACES.md) must stay **runtime-probed** —
   if capabilities became overloads too, the surface goes to 2 × 2^k. The split doesn't
   just tolerate that division of labor; it depends on it. (Capability-based
   optimizations like stackless DFS via parent navigation live *inside* concrete
   `Get*Treenumerator` implementations — invisible to the type system. Synergy note: the
   level-order store is LOUDS-adjacent → O(1) parent → the DFT-over-level-order
   treenumerator is a candidate first stackless DFS.)
5. **Audit outcome (supersedes the containment guess).** The narrow-overloadable set is
   defined by the factory shape, not streaming-ness: 25 operators are cleanly
   both-dimension, 7 are dimension-fixed *by semantics*, and only `Invert` +
   `LeaffixScan` are RETHINK. `GetLeaves`/`GetRoots`/`CountTrees`/`RootfixAggregate`
   were dimension-fixed **by implementation accident only** (leaf detection is O(1) in
   either dimension) and join the both-dimension set. See OPERATOR_DIMENSION_AUDIT.md.
6. **Wave scope.** This wave: split + flat-family treenumerables + serializer. Next
   wave: `Invert`/`LeaffixScan` rework and PreorderTree's dissolution. Deletion
   candidates identified during review (no current value, zero internal references):
   `WithContext`, `WithLevelIndex`, `WithParent` — the real requirement behind them is a
   general context-attachment mechanism (path-threaded = `RootfixScan` already;
   traversal-order-threaded = a possible future per-enumeration-state Select/Do
   primitive, admitted only on real demand).

## The organizing principle

Everything in this note is a corollary of one observation:

> **Whenever the required emission order reverses the arrival order, you must buffer the
> entire gap between them.**

- LINQ's `Reverse` is the degenerate case: gap = the whole sequence.
- `Invert` (mirror) consumed depth-first is the same cliff: the root emits instantly, but
  the *second* node owed to the consumer (the root's mirrored first child = original last
  child) arrives from the source almost **last**. Gap = the whole tree, hit between the
  first and second `MoveNext`.
- `Invert` consumed breadth-first is the benign case: mirroring reverses every sibling
  group, and reversing every sibling group at level *k* produces exactly the original
  level-*k* sequence reversed end-to-end. Gap = one level → O(width), BFT's native
  frontier class. **Mirror is windowed under BFT and total under DFT.**
- Cross-order traversal over a forward-only source is the same phenomenon expressed
  *between* dimensions rather than within one.

The interface split makes "which arrival orders can this source offer" a **compile-time
fact**, so that reversals against an unavailable order cannot be requested silently.

## The no-silent-escalation principle (as refined through debate)

First attempt: "never silently change memory class; extra I/O passes are OK." Jason
rejected this as inconsistent — it privileges space over time arbitrarily.

Second attempt: "no silent complexity-class change in either space or time; all
escalations explicit." Jason countered with precedent: the library already escalates
implicitly (`Invert` materializes internally; cf. LINQ `Reverse`), and we trust users to
know their tools — while acknowledging that treenumerables are far less intuitive than
enumerables, so the foot-gun bar is genuinely higher here.

Resolved form: **an API's defaults may not silently violate that API's own contract.**

- `Reverse`/`Invert`-style costs are *implied by the operator's semantics* — anyone who
  understands what "mirror" means can deduce the buffering. Local reasoning. Tolerable.
- The sharp foot-gun is the *source-dependent* escalation: the same call
  (`GetBreadthFirstTreenumerator()`) being O(width) on one tree and catastrophic on
  another depending on a hidden property of where the tree came from. Non-local
  reasoning. Not tolerable as a silent default.
- A *streaming* deserializer's contract is a **memory contract** (memory tracks the
  traversal frontier, not the tree). Re-reading the file more times keeps that contract;
  silently memoizing breaks it.

The split is the strongest enforcement of this: unaffordable operations don't typecheck,
so there is nothing to be silent *about*.

## The split

```
IDepthFirstTreenumerable<T>    — GetDepthFirstTreenumerator()
IBreadthFirstTreenumerable<T>  — GetBreadthFirstTreenumerator()
ITreenumerable<T> : IDepthFirstTreenumerable<T>, IBreadthFirstTreenumerable<T>   // pure composite
```

- **Zero disruption by construction**: `ITreenumerable` as a pure composite means every
  existing tree, operator, and test keeps its exact typing. Escape hatch: if the split
  proves a mistake, deprecate the narrow types and collapse — nothing typed against
  `ITreenumerable` ever notices.
- **`Memoize()` is the typed upgrade op**: on either narrow interface it returns
  `ITreenumerableBuffer<T> : ITreenumerable<T>`. This makes "memoization is what
  purchases the other dimension" a fact in the signatures — exactly matching the memo's
  runtime semantics (cross-order riding is the memo's job) and the
  `TREE_CAPABILITY_INTERFACES.md` principle that Materialize/Memoize are the
  poor-capability → rich-capability bridge. Same for `Materialize`.
- **Every `ITreenumerable` is a full citizen by construction** — the tree-monad contract
  is never violated by a partial implementation; partiality lives in honestly-named
  narrower types.
- Signatures start encoding cost semantics (a slice of WhatIf absorbed statically):
  `Invert : IBreadthFirstTreenumerable → IBreadthFirstTreenumerable` (windowed) vs. the
  DFT mirror simply not existing without a visible `.Memoize()`.

### Containment (why this doesn't explode)

C# has no higher-kinded types: one `Where` cannot return "the same interface you gave
me." Dimension-preserving operators need per-interface overloads *and* single-dimension
wrapper classes. Across 45+ operators that's a 2–3× surface explosion — the reason this
split was avoided for so long — plus the segregated-interface failure mode (users
reflexively `Memoize()` to satisfy `ITreenumerable`-demanding code, silently
re-introducing the escalation the types were meant to make deliberate).

Containment strategy: **split only the interfaces; give narrow overloads only to the
operators a streaming consumer actually chains.** Roughly: `Select`, `Where`, the
prune/take family, and the aggregates (`RootfixAggregate`/`LeaffixAggregate`/
`CountNodes`/`GetLeaves`) — call it 8–10 operators, whose per-dimension treenumerators
*already exist* (the unified interface was always a veneer over per-dimension machinery;
the split is with the architectural grain). Everything else stays `ITreenumerable`-only;
a streaming consumer who wants `Union` or `Invert`-over-DFT memoizes first, which is
almost certainly semantically necessary anyway.

Overload resolution: with all three overloads present, passing an `ITreenumerable` picks
the `ITreenumerable` overload by standard betterness (identity conversion beats
base-interface conversion). Cost: noisier intellisense, longer error messages.

### Relationship to TREE_CAPABILITY_INTERFACES.md

The dimension split is a **new axis, orthogonal to** that doc's lattice: the lattice
subdivides what you can ask of a *node* (parent/sibling/child navigation, indexability);
this subdivides which *traversal streams* exist at all — it sits underneath the lattice's
bottom row. The two mechanisms divide cleanly:

> **Types for "impossible without escalation"; runtime probing for "cheaper when rich."**

Where no acceptable streaming fallback exists (mirror-over-DFT, cross-order-over-stream),
don't probe — make it untypeable, and there's no fallback to maintain (no "discipline
tax"). Where a fallback exists and a rich source is merely faster (buffer-aware `Invert`
views, LINQ-style `ICollection` sniffing), probe at runtime. The lattice doc's own
principles transfer intact: *grow from real demand* (ship exactly the two dimension
interfaces now, nothing else), and its deferral condition ("revisit after the
snapshot/serialization foundation is solid") is now met — serialization is the real
demand arriving.

## Serialization redesign

Framing: **serialization = persisting a memo capture; deserialization = rehydrating
one.** The dft layout ≡ `MemoizePreorderBuffer`'s representation (pre-order values +
subtreeSizes; balanced-parens-adjacent). The bft layout ≡ `MemoizeLevelOrderBuffer`'s
(level-order values + child spans; LOUDS-adjacent).

### Deserialize

> **2026-07-04 addendum:** the traversal layer for deserialization is expected to come
> for free from the **layout streamers** planned in
> [PACKAGE_ARCHITECTURE.md](PACKAGE_ARCHITECTURE.md) (memoize-replay de-engining): four
> storage-specialized treenumerators (DFT/BFT × preorder-store/level-order-store),
> parameterized over store access. A lazily-parsed serialized source is just another
> store; memo buffers and PreorderTree are the in-memory ones. Build once, reuse here.

- Returns a **streaming lazy view** — never a buffer. (A buffer-returning
  `Deserialize(TextReader)` was considered and rejected: a memo's capture grows
  monotonically, so it's just deferred materialization — fatal for the motivating case,
  aggregating over a 10 GB file. Memory must track the traversal frontier.)
- `Memoize()`/`Materialize()` are the caller's explicit escalation. The old eager parse
  doesn't die — it *is* the memo's DFT capture construction already.
- **Overloads**:
  - `Deserialize(string, ...)` — lazy, freely re-enumerable (the string is its own
    random-access buffer; no ownership problem). Both dimensions available; cross-order
    costs more per step but memory stays bounded. Currently eager (`PreorderTree`);
    becomes incremental.
  - `Deserialize(Func<TextReader> readerFactory, ...)` (+ file-path convenience wrapping
    `File.OpenText`) — the primary stream entry point. Each `GetTreenumerator` opens a
    fresh reader, **owns it, disposes it in the treenumerator's `Dispose`**
    (`ITreenumerator` is already `IDisposable` and already disposed by every consumer —
    that's the natural ownership hook; no new concept needed). Re-enumeration re-reads
    the file: the standard lazy contract.
  - `Deserialize(TextReader, ...)` — single-shot convenience; first treenumerator takes
    ownership, second `GetTreenumerator` throws.
- **Return type under the split**: the single-dimension interface matching the stored
  layout. Cross-order over a factory source becomes a compile error, not a runtime
  throw.
- Streaming costs: native DFT over dft-layout = O(depth) memory (the root-to-current
  path, because DFT revisits parents between children). Native BFT over bft-layout =
  O(width) (the frontier window between visiting front and scheduling cursor).
  `SkipDescendants` on a forward-only reader can't seek: O(subtree bytes) of I/O, O(1)
  memory, never invokes the value map.

### Cross-order escalations (explicit, typed)

- **BFT-over-dft-layout has a bounded strategy**: level re-scans (BFS via iterative
  deepening). Pass *k* scans the whole file tracking depth via paren structure and emits
  "visits of level-*k* nodes interleaved with schedules of their level-*k+1* children" —
  correct in one forward scan because children sit contiguously after their parent in
  pre-order (skip-counting deeper subtrees) and equal-depth nodes appear in pre-order in
  level-order sequence. Cost: **depth+1 full file reads, O(depth) memory.** Honest
  framing: ~maxDepth × file size of sequential I/O — bounded, predictable, fails soft
  (vs. the single-pass alternative's O(N) memory, which OOMs). Degenerate spine-shaped
  trees make it quadratic — gate on header shape stats.
  - Under the split this is an **explicit opt-in extension** on the factory-deserialized
    dft source (e.g. `AsBreadthFirst()`), returning `IBreadthFirstTreenumerable<T>` — the
    time escalation as visible in code as `Memoize()` is for space. It's a property of
    *rewindable sources* (the factory), not of dft-ness generally.
- **DFT-over-bft-layout has no bounded strategy** (DFS emission vs level-order arrival:
  either ~O(N) buffering in one pass or O(N) re-scans on backtracking). It simply doesn't
  exist; escalate via `Memoize()` or re-serialize in dft layout.

### Format

- Structured envelope with a **layout axis: `dft | bft`**. NOT "both" as a storage
  layout: both = duplicated values or a permutation array, and it only pays for lazy
  streaming in both dimensions from one file — which a forward-only stream can't deliver
  anyway. Rehydrated captures already serve the other dimension cheaply
  (`subtreeSizes`-guided O(1) child hops), and "both dimensions materialized" is just
  `Materialize`+`Consume` at deserialize time.
- **Shape stats in the envelope**: node count, max depth, max width, recorded at
  serialize time (backpatch a fixed-width header on seekable destinations, or a
  trailer). Uses: gate the re-scan strategy against spine-shaped trees; cheap `Memoize()`
  feasibility check; concrete numbers for WhatIf estimates.
- `Serialize` takes a `TreeTraversalStrategy` so a BFT-native source (or a memo with only
  its BFT dimension captured) serializes without cross-order cost. Add
  writer/stream-destination overloads for symmetry. The bft payload grammar should emit
  each front node's child *group* as it's scheduled (LOUDS-style) — streams with O(1)
  writer memory, whereas per-node child counts force buffering a full level.
- Keep the terse `a(b,c)` grammar as the dft payload — it's load-bearing for tests,
  TestUtils, and benchmarks.
- **Open: JSON vs dependency-free.** The packages just went dependency-free;
  `System.Text.Json` on net48/netstandard2.0 reintroduces a package dependency. If
  "structured" only needs self-description + versioning, a tiny hand-parsed header does
  it (e.g. `copse/1;layout=dft;stats=...;` + payload). If external-tooling interop is the
  goal, real JSON in a *separate* package (e.g. `Copse.Serialization.Json`). Motivation
  not yet pinned down.
- Open: where the structured serializer lives (`Copse.SimpleSerializer` gaining a
  `Copse.Linq` reference, vs. a new package). Currently SimpleSerializer references only
  `Copse.Core` + `Copse`.

## Serializer surface (2026-07-04, second session — IMPLEMENTED)

Signed off and landed (`TreeSerializer.Envelope.cs`, `PreorderTextStream`,
`LevelOrderTextStream`; round-trips conformance-tested via the shared battery). The bft payload
grammar is Jason's `BreadthFirstTreeTokenizer` tokenization rendered to text — independently
equivalent to LOUDS-with-inlined-values (`a;b,c;d,e` for `a(b(d,e),c)`: ',' in-family, '|'
family terminator, ';' generation mark, trailing empties elided) — the dft payload is the paren
grammar (BP-with-inlined-values). As-built details below (items 1–6 all hold as written, with
the two-separator grammar chosen in item 2):

1. **Header** (per the agreed one-layout-per-file envelope): first line `copse/1;layout=dft` or
   `copse/1;layout=bft`, then the payload. **Shape stats deferred** — nothing consumes them yet
   (the `AsBreadthFirst()` re-scan gate and WhatIf are both future), and a forward-only writer
   can't backpatch them; add them (as header-on-seekable/trailer-otherwise) when the re-scan
   opt-in lands. **Bare payloads stay valid for the string overloads** — the terse `a(b,c)`
   grammar is load-bearing for tests and stays exactly as-is.
2. **BFT payload grammar** (new; the dft payload keeps the paren grammar): level-order child
   groups, LOUDS-style — the roots group first, then one group per node in level order (its
   children), `|` between groups, `,` within, trailing empty groups elided, EOF = all remaining
   groups empty. `a(b(d,e),c)` ⇒ `a|b,c|d,e`. Writes with O(1) memory (each front node's group
   emits as it's visited); reads with O(width).
3. **API shape**: C# cannot give `Deserialize(factory)` a return type that depends on the stored
   layout, so the stream entry points are layout-specific —
   `DeserializeDepthFirst(Func<TextReader>) : IDepthFirstTreenumerable<T>` and
   `DeserializeBreadthFirst(Func<TextReader>) : IBreadthFirstTreenumerable<T>` — each validating
   the header's layout axis at treenumerator acquisition (plus a cheap `ReadHeader(...)` for
   callers who need to dispatch). File-path conveniences wrap `File.OpenText`; the
   single-`TextReader` overload is single-shot (second acquisition throws); every treenumerator
   owns and disposes its reader.
4. **Forward-only treenumerators live in Copse.SimpleSerializer** (they are grammar-bound
   decoders, not store riders): DFT-over-paren-tokens at O(depth) memory with `SkipDescendants`
   as a discard-scan (O(subtree bytes) I/O, O(1) memory, value map never invoked); BFT-over-group
   -tokens at O(width) with its own eviction window (values before the visit front are dead).
   The random-access string case instead parses lazily into a growing `IPreorderStore` and rides
   the existing flat treenumerables.
5. **Escaping is out of scope for v1**: the value alphabet excludes the structure characters
   (`(`, `)`, `,`, `|`, `;`, newline), as today. Real escaping (or length-prefixed values) is a
   format rev when demand arrives.
6. **Oracle guard**: `Deserialize(string)` keeps returning the engine-backed `PreorderTree` until
   the retirement wave — switching it to flat playback now would silently flip the comparison
   oracle under most of Copse.Linq.Tests (ContractTree conformance explicitly diffs against "the
   engine's streams"). When PreorderTree dissolves, those tests need an explicitly-constructed
   engine side first.

## Layout-typed buffers (2026-07-04 exchange — direction, not yet code)

Jason floated splitting `ITreenumerableBuffer` into `IPreorderTreenumerableBuffer` /
`ILevelOrderTreenumerableBuffer`. Refined outcome:

- **`ITreenumerableBuffer` stays layout-NEUTRAL**: a memo's layout is a runtime fact (it grows
  whichever dimension is requested first and drops the loser), and a type's interface list can't
  change mid-life — so `Memoize()` cannot honestly return a layout-typed buffer. This is also
  what makes Memoize/Materialize the de facto `IDepthFirst`/`IBreadthFirst` → `ITreenumerable`
  upgrade (Jason's observation): the promise is the composite, regardless of internal layout.
- **Layout-typed interfaces arrive as capability advertisements on top** — e.g.
  `IPreorderTreenumerableBuffer : ITreenumerableBuffer` exposing its `IPreorderStore` — for
  sources whose layout IS statically known: `Materialize(strategy)` results, deserialized
  sources, PreorderTree's successor. Their consumer is composition-time sniffing (the
  Invert-as-view fast path), so they ship with the Invert rework, not before. Layering: neutral
  contract → Core (with the namespace wave); layout-typed subtypes → Copse, beside the store
  SPIs they expose.

## The Invert rethink (condition attached to the split) — IMPLEMENTED 2026-07-05

> **As built** (clarified with Jason 2026-07-05): `Invert(IBreadthFirstTreenumerable) :
> IBreadthFirstTreenumerable` — a level-reversing `ILevelOrderStream` transform over the
> existing breadth-first playback (no new treenumerator; O(width), one tier of families
> buffered). Plus `Invert(ITreenumerableBuffer) : ITreenumerable` — the buffer is the
> proof-of-purchase that random access is paid for, so the mirror is a full citizen again
> (built once, lazily, from the capture's replay; the zero-copy view stays the planned
> upgrade). **No overload accepts `IDepthFirstTreenumerable`** — a DFT-only source's only
> spelling is `.Memoize().Invert()`. Invariant: every path to the mirror's depth-first
> dimension passes through a visible `Memoize`; source fullness does not buy mirror fullness
> (only random access moves the arrival-order cliff). `PreorderTree`'s remaining Linq
> consumer is now `LeaffixScan` alone.

## The original Invert design note (superseded by the above)

Jason: *if* we go down this route, operators with hidden escalations get rethought rather
than mechanically re-typed. `Invert` (mirror) is the poster child — today it does **two**
hidden O(N) things: a full DFT materialization into pre-order arrays, then a *copy* into
mirrored arrays (and ironically consumes via DFT even though BFT could stream it).

Reworked:

- `Invert(this IBreadthFirstTreenumerable<T>) : IBreadthFirstTreenumerable<T>` — lazy,
  O(width): buffer one level, emit it reversed. No materialization.
- No DFT overload — mirror-over-DFT is the arrival-order cliff (see organizing
  principle). Callers write `.Memoize().Invert()`: the O(N) is in *their* code.
- **Buffer-aware fast path = zero-copy mirrored views.** Subtree sizes are invariant
  under mirroring (the current code's own comment), so mirroring a DFT capture is a lazy
  reverse-child replay over the *existing* arrays — the current step-2 stack walk made
  lazy, no copied arrays. The BFT capture's child spans walked backwards yield the
  mirrored level-order replay directly. Mirror twice → two views, zero copies.
- Mechanics: `Invert` and `MemoizeTreenumerable` are both in `Copse.Linq` — the view can
  use **internal** capture access; don't publish rich-access capability APIs until real
  demand (per the lattice doc). Sniff the buffer *type* at composition time (types don't
  change); read capture *state* at enumeration time (a memo's capture evolves between
  composing and running).

## WhatIf (related, separate feature)

A cost-introspection channel (idea predates this discussion — quick exchange ~late June
2026): each treenumerable exposes, per dimension, a coarse symbolic profile — time and
space from a small lattice like {O(1), O(d), O(w), O(n), O(n·d)} — composed
operator-by-operator, sniffed via an optional interface the way LINQ sniffs
`ICollection`. Yields:

1. **Introspection**: `chain.WhatIf(strategy)` → "time O(n·d) in source reads, space
   O(d)". With envelope shape stats, *concrete* estimates ("~250 GB of reads, ~40 KB
   resident"), not just symbols.
2. **Budget guard**: a declarative assertion at the head of a chain that throws at
   treenumerator acquisition if the composed profile exceeds a stated class. One guard
   protects the whole pipeline, including operators added later; casual users never see
   it. Paternalism as opt-in.

Honest limitation: doesn't solve *who checks* — but it converts "understand all the
nuances" into a one-call question, enables analyzers/docs, and the split absorbs the
worst cases statically anyway. Nothing in the serializer blocks on WhatIf; only the
envelope stats should be designed with it in mind.

## Pick-up plan

1. **Operator audit table** (first artifact; also the first draft of the WhatIf cost
   model): classify all 45+ operators by dimension consumed, dimension preserved,
   emission-vs-arrival gap per dimension, and hidden escalations. Expected interesting
   rows besides `Invert`: `GetLevels`, `LevelOrderTraversal`, the set operations
   (`Union`/`Intersection`/`Subtract`/`SymmetricDifference`). This settles which
   operators get narrow overloads.
2. Settle interface names and placement (`Copse.Core`) and the composite definition.
3. Implement the split + narrow overloads for the streaming set; single-dimension
   wrapper classes where needed.
4. `Invert` rework (typed + buffer-aware views) as the exemplar for other audited
   operators.
5. Serializer: envelope format decision (header vs JSON-in-separate-package), layout
   axis, shape stats, reader-factory overloads, `Serialize(strategy)`.
6. Re-scan opt-in (`AsBreadthFirst()` on rewindable dft sources), gated on header stats.

---

*Discussed and recorded 2026-07-04. Companion to
[TREE_CAPABILITY_INTERFACES.md](TREE_CAPABILITY_INTERFACES.md) (orthogonal axis) and
[MEMOIZE_DESIGN.md](MEMOIZE_DESIGN.md) (the upgrade op the split makes typed).*
