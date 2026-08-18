# The Public Composition Surface (design)

> **Status: RATIFIED 2026-08-18; build not started.** Successor to the citizenship shape of
> SELECT_INTO_CAPTURES_DESIGN.md §2 (that document stays CLOSED; its rulings carry
> forward unchanged: filtering internal-forever, the memo compose seam, the value-only
> seat rule, compose-left PARKED). Ratified in design dialogue, 2026-08-18; all rulings
> below are Jason's unless marked as recommendation-accepted.

## 1. Origin

Three observations, in the order they arrived:

1. **The parallel-axis confusion.** `ISelectComposableTreenumerable` (public citizenship)
   and `ISelectPruneAfterTreenumerable` (internal light-tier marker) overlap in capability
   but sit on unrelated axes — one public/internal, one machinery-tier. The fourth cell
   (SCAN_TIER_DESIGN.md) was solved with no interface at all, which made the map harder
   to read, not easier.
2. **The strategy-expressible boundary.** The filtering-citizenship rejection ("too
   difficult to do right") is really a mechanical law: Where's rewrite (promotion, sibling
   renumbering, manufactured/suppressed visits) requires invariants only the driver owns;
   PruneAfter's entire rewrite is `SkipDescendants` — a consumer-protocol primitive every
   conformant treenumerator already honors, with no relabeling (the kept node keeps its
   position; no sibling of anyone renumbers). PruneBefore removes the matched node, so
   subsequent siblings renumber — internal. The light tier is exactly the fragment whose
   rewrite the traversal protocol itself enforces. **That fragment can be public.**
3. **The GetAncestors principle, correctly aimed.** "The general can do everything the
   specific can" argues for subsumption-by-inheritance — but only for **doors** (compose
   requests: pull-shaped, free until knocked), never for **seats** (per-visit payloads:
   push-shaped, paid whether read or not — which is why lineage seats failed the hard
   gate and the fourth cell stayed a type). The compose surface is all doors, so the
   hierarchy can and should run general-extends-specific.

## 2. The law of the boundary

**Public composition = what the consumer protocol can express.** Projection (value-only,
no invariant surface) and prune-after (`SkipDescendants`, forwarded, no relabeling).
**Internal composition = what requires driver invariants.** Where, PruneBefore, the
selector-struct algebra, Relabels, the splice/donation surface.

This restates — and strengthens — the filtering ruling: "too difficult to do right"
becomes "requires invariants only the driver owns."

## 3. The hierarchy (reversed, split)

```
public   ISelectTreenumerable<TNode>          — ComposeSelect<TResult>(Func<TNode,TResult>)
public   IPruneAfterTreenumerable<TNode>      — ComposePruneAfter(Func<TNode,bool>)
internal ISelectWhereTreenumerable<TNode> : ISelectTreenumerable<TNode>,
                                            IPruneAfterTreenumerable<TNode>
         — the struct algebra: struct/Func Compose, Relabels, the splice surface
```

Rulings:

- **Split over chain** (the diamond): capabilities are claimed independently. Evidence:
  the buffer citizenship can honor Select (counted re-capture, presize rule) but not
  prune (post-prune size is data-dependent); the TakeSubtreesWhere citizens are
  Select-composable only, per the filtering ruling made manifest.
- **Names**: `ISelectTreenumerable`, `IPruneAfterTreenumerable` — consistent with
  `ISelectWhereTreenumerable`; no "Composable" in the names.
- **`ISelectPruneAfterTreenumerable` is DELETED** (async + sync twin + codegen entry).
  No conjunction marker: the light tier stops being an interface and becomes what it
  always was — concrete types with light representations. The in-tier arrows become
  internal statics called from those types' door implementations.
- **Why this orientation is forced, not preferred**: an internal interface may extend a
  public one, never the reverse — so the public fragment must be the base. And finality
  lands correctly: the public bases are tiny and stable; the churny struct algebra stays
  internal and freely evolvable above them.
- **Nothing from the tear-out is lost**: the tear-out's semantic content ("light members
  offer the general splice surface") lives in the classes, which keep implementing
  `ISelectWhereTreenumerable`. The interface arrow flips; no capability moves.

## 4. Door signatures (FINAL)

The public interfaces ship **complete and final**: net48/netstandard2.0 have no default
interface members, so any member added later breaks every third-party citizen. Growth
later = major-version break or a second interface. (Noted against "maybe we'll add
members later" — the names are right regardless; the member lists freeze at ship.)

- Value-only doors (the seat rule; positional flavors take the wrapper — unchanged).
- **Per-capability closure** (recommendation, accepted): `ComposeSelect` returns
  `ISelectTreenumerable<TResult>`; `ComposePruneAfter` returns
  `IPruneAfterTreenumerable<TNode>`. Cross-capability chaining (a Select after a prune)
  resolves by runtime sniff — how every extension works anyway. The closure signature
  weakens from "citizen" to "citizen-in-this-capability"; stated, accepted. The
  alternative (a combined public return interface) would resurrect the conjunction
  publicly and force both capabilities on implementers of the return.

## 5. Sniffing under the new shape

Per-operator, most-derived-first — **safe only because of the door-optimality law (§6)**:

- **Rejecting operators** (Where, PruneBefore): sniff `ISelectWhereTreenumerable` →
  splice/join; else the driver stacks over the source. One rung.
- **Non-rejecting operators** (Select, PruneAfter): sniff `ISelectWhereTreenumerable`
  (struct door — the 6bc14fd lesson: internal call sites keep the struct algebra) →
  public base (Func door — foreign citizens, buffer faces) → wrapper fallback. Two rungs.

The rungs discriminate **representation** (struct vs Func leaf) and **rejection** (the
internal-only algebra) — never capability. Capability ordering dissolved into virtual
dispatch. Historical note: most-derived-first for Select would have recreated the
tier-seal regression (2026-08-04, `Where.Triangle_Mixed` +20–25%) under the old door
semantics ("convert to the general representation"); it is correct now only because
doors self-optimize. The 53ce350 probe-order fix (citizenship above general) becomes
moot: there is one `ComposeSelect` slot per member and the scan citizen's slot IS the
re-plant. Probe-order pins retire; door-optimality pins replace them (§6).

## 6. The door-optimality law

**Every door implementation is that member's best machinery.** The light wrapper's door
composes delegates and stays light; the driver's door nests the leg; the scan citizen's
door re-plants into fold and emission; the buffer's door does the counted re-capture.
The old design enforced optimality at the call site (probe picks the rung); this design
trusts the member (door picks the machinery). The trade: probe-order pins (few, globally
fragile) retire; door-optimality pins (one per member-door, trivially local) land —
SelectComposableLawTests style, asserting the returned machine's type per member.

## 7. Absorption-claim semantics (no floors)

Implementing a public door is an **absorption claim**, per capability: "I can fold this
into my recipe." There is no floor obligation and no floor machinery (the Hide idiom and
the helper statics from design dialogue are retired unbuilt). The three-way
implementation story, in preference order:

1. **Output-generic type** (a projector seat with a free output parameter): return your
   rebuilt self with the composed delegate. Zero layers, true absorption.
2. **Fixed-output type holding a treenumerable source**: return
   `new SelectTreenumerable(yourSource, composedDelegate)` — you swap yourself for the
   public wrapper at the layer count you occupied. True absorption (the delegate
   composed; nothing stacked). For prune-after the analog is the public
   `PruneAfterTreenumerable` over your source; prune absorption is strategy forwarding
   rather than delegate composition, so "same layer count" holds only when your own walk
   was already a pass-through — one honest caveat, disclosed here.
3. **No seat at all**: `new SelectTreenumerable(this, f)` is the safe cheap floor —
   recursion-free by construction, closure-correct, and subsequent Selects compose into
   that wrapper's delegate, so the whole chain still costs exactly one wrapper — the
   same result the extension's fallback builds for non-citizens. ("Don't claim if you
   can't absorb" is thereby guidance, not law.)

**The recursion trap, documented at the member**: the `Select`/`PruneAfter` extensions
defer to the doors. A door implemented as "call the extension on myself" is mutual
recursion — stack overflow at first use, no compile-time signal. Each door's XML doc
carries the warning phrased positively: *"rebuild yourself, or return
`new SelectTreenumerable(source-or-this, composedDelegate)` — never the extension on
yourself."*

**The drop-in guarantees**, stated as the design's purpose:
- Dropping a Select in always collapses — into the citizen's recipe (zero layers) or
  into one light wrapper (one layer), however long the chain.
- Dropping a tree in works with or without the claim; the citizenship's exact value is
  removing the last wrapper by letting the projection reach the member's internals.

## 8. The public wrapper classes

`SelectTreenumerable<TSource,TResult>` and `PruneAfterTreenumerable<TNode>` (async +
generated sync twins) go **public, sealed** — the canonical vehicles for §7 and the
`IList`/`List`-style pairing with the interfaces. Class over factory, ruled: the ctor is
the factory; classes grow non-breakingly (only ctor shape and arity freeze); a choosing
static factory can be added beside the ctor later if ever needed. Known cost, accepted:
a constructor cannot sniff-and-collapse manually stacked `new`-over-`new` — the doors do
all collapsing that matters.

**The visibility audit** (per class):

1. Every internal-algebra member becomes an **explicit interface implementation**; the
   public surface is exactly: ctor + the public-interface doors.
2. The compiler enforces this wherever a signature mentions an internal type (the Func
   `Compose` door via `SelectWhereResult`, the struct door via `IResultSelector`,
   `CaptureThrough` via the projection-consumer shapes) — those cannot compile as
   implicit public.
3. **Hand-check the compiler-blind cases**: internal-concept members with public-typed
   signatures — `Relabels` (a bare bool) is the known one. Audit question: "does any
   implicit public member remain that isn't the ctor or a public door?"
4. `CaptureThrough` stays an explicit implementation of the internal
   `IAsyncProjectionSource` — invisible publicly, reachable by our capture operators'
   sniff. **This preserves the PARKED status of compose-left** (third parties get
   compose-right only); publicizing the class must not unpark it. The partial-file
   arrangement (CompositeToNarrow file-granularity; narrow twins must not claim the
   composite-width door) is unaffected — only one partial part carries the modifiers.
5. The public ctors speak `Func<TSource,TResult>` / `Func<TNode,bool>`; internal context
   shapes stay internal.
6. The combined Select∘PruneAfter wrapper stays internal (reachable through doors).
   Narrow variants stay internal (narrow parity deferred-until-demand, unchanged).
7. XML docs on all public surface, including the §7 recursion warning.

## 9. The buffer flavor

The buffer citizenship keeps its shape — a separate interface extending
`IAsyncTreenumerableBuffer` with its own `ComposeSelect` returning the buffer flavor
(counted projected re-capture; the presize rule) — renamed to the new grammar
(`ISelectTreenumerableBuffer`, name consult at build). **No prune door**: post-prune
size is data-dependent, which breaks the buffer's promise; prune over a buffer goes
through the streaming face. This is the split paying for itself in the types.

## 10. What does not move

- **The fourth cell**: `ScanWhereTreenumerable` remains a type over
  `ISelectWhereTreenumerable`; the fold stays representation (TAccumulate existential);
  doors forward inward (the ComposeSelect slot is the fold re-plant).
- **The scan/rootfix/TakeSubtreesWhere citizens**: unchanged membership, doors renamed
  onto the new bases.
- **Hide**: pinned as law — `Hide()`'s result implements none of the public doors. The
  isolation barrier must remain the one guaranteed capability-free view (barrier
  taxonomy: Do = count, Hide = isolation, Memoize = compose seam).
- The hard gate: plain Where treenumerators, byte-identical.

## 11. Breaking changes (pre-beta, release-notes flags)

- `ISelectComposableTreenumerable` (public, shipped v0.3.0-alpha.18) is replaced by
  `ISelectTreenumerable`; the buffer flavor renamed to match.
- `ISelectPruneAfterTreenumerable` deleted — internal, not a public break; codegen entry
  and sync twin removed with it.
- No behavioral changes to any existing spelling (gated by Stage 0).

## 12. Build plan (land-then-distill stages)

- **Stage 0 — the behavior-neutrality harness.** Before any interface moves: pins
  asserting, for the canonical composition spellings (light chains, joins, scan
  citizens, TakeSubtreesWhere, buffers), the exact runtime machine type each produces
  today; plus the twin byte-identity checks. These pins must pass unchanged after
  Stages 1–2 — the reorientation is provably a representation no-op.
- **Stage 1 — the reorientation.** New public bases; `ISelectWhereTreenumerable`
  re-based onto them; conjunction marker deleted; every member's doors implemented by
  inward forwarding; sniffs collapsed to §5's ladders; door-optimality pins landed.
- **Stage 2 — the classes go public.** The §8 audit, both classes, twins regenerated.
- **Stage 3 — the record.** XML docs, release-notes flags, OPERATOR_SURFACE_MAP rows,
  supersession note in SELECT_INTO_CAPTURES §2 pointing here.

Benchmarks: **no new rows** (consult-first rule); probing is acquisition-time only.
Watch rows = the Compose family + the Mixed pair, as always.

## 13. Consult residue

The buffer-flavor name at Stage 2; nothing else — interface and class names are ruled
(§3, §8).
