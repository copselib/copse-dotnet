# WithPosition (design)

> **Status: STAGE A SHIPPED; STAGES B-C REJECTED AT THE WITNESS GATE (2026-08-18).**
> The witness pairs (Compose family, seeded before any reroute) measured the spelling
> against the overloads' machines on MegaTriangle, local ShortRun:
>
> | Pair (DFT / BFT)      | Overload        | Spelled         | Delta        |
> |-----------------------|-----------------|-----------------|--------------|
> | PositionalPruneAfter  | 32.7 / 36.6 ms  | 59.4 / 55.7 ms  | +82% / +52%  |
> | PositionalWhere       | 57.6 / 55.4 ms  | 64.1 / 63.7 ms  | +11% / +15%  |
> | Allocations           | --              | --              | IDENTICAL    |
>
> **The erasure argument held exactly for allocation** (the pair never lands in any
> state) **and failed for time. The mechanism, CORRECTED by decomposition** (Jason's
> challenge -- "if composing Select.PruneAfter.Select added 82%, the composition story
> has been lying" -- prompted the follow-up run; local DFT rows: raw drain 46.2 ms, bare
> `Select(n => n)` 45.6 ms, overload 33.0 ms, spelled 59.2 ms, Defer-broken stacked
> spelling 66.0 ms):
>
> - **The composition story is VINDICATED**: composed (59.2) beats stacked (66.0) on
>   exactly this chain.
> - **A single per-visit delegate is FREE** (bare Select ≈ raw), falsifying the
>   first-draft per-visit-vs-per-schedule multiplicity claim as the dominant term.
> - The overload's 33 vs raw's 46 is just the pruned walk (depth-1200 sheds ~30%).
> - **The real gap is the LIGHT TIER'S FUNC-COMPOSED ARROWS**: the spelled chain runs
>   three closure hops with NodeContext/SelectWhereResult struct plumbing per visit
>   (~12 ns/visit over ~2.2M visits ≈ the 26 ms) -- the exact de-inlined-delegate-chain
>   disease the struct-composed arrow cured in the DRIVER tier at the reunification,
>   still alive in the light tier (SelectWhereComposition = closure arrows).
>
> **The rescue is therefore a KNOWN CURE, not speculation: struct-nest the light tier's
> in-tier arrows** the way ComposedResultSelector struct-nested the driver's. Precedent
> (the reunification's collapse-ratio recovery), independent value (every light chain
> speeds up), and a ready gate (the witness pairs converging). Stages B-C are not dead --
> they are gated behind the light tier's own reunification.
>
> **ATTEMPT #2, SAME DAY: the known cure FAILED its gate too.** The light struct nesting
> was built in full (the SPA machine and wrapper went 3-arity selector-struct-generic,
> chains nesting via ComposedResultSelector; the four Select-involving arrows deleted;
> 24,600 green) and measured WORSE: PruneAfter_Spelled 59.4 -> 71.0 ms DFT, and 55.7 ->
> 146.2 ms BFT -- reproducible (StdDev 0.12), and AggressiveInlining on every GetResult
> changed nothing. The deep nested-generic struct chain under PER-VISIT evaluation is a
> JIT/codegen pathology on this runtime that the driver tier never sees, because the
> driver evaluates ONCE PER SCHEDULE where any per-call residual is noise. Committing it
> would also have regressed ordinary Select-then-PruneAfter chains, so the whole attempt
> was REVERTED UNSHIPPED.
>
> **The corrected lesson, both attempts on record:** the cost is evaluation-count TIMES
> per-evaluation weight. The Func arrows lose on weight but the struct chain loses
> HARDER under per-visit multiplicity (against every prior on this codebase). The light
> tier's real moat is the bespoke drivers' EVALUATION-SITE DISCIPLINE -- one predicate at
> the scheduling decision, nothing per visit. Any future rescue must target the COUNT
> (a once-per-node evaluation cache in the passthrough -- the first verdict's sketch,
> prematurely dismissed), and must profile before building: two hardware rejections is
> the doctrine speaking. Until then the light tier's closure arrows stand as measured-
> best, and the witness pairs remain the standing gate.
>
> **What stands meanwhile:** WithPosition itself (Stage A, additive -- the
> explicit-capture spelling, the capture-scope semantics, the sugar-equivalence law, all
> pinned); the witness series as the standing gate; the design record below. The
> positional overloads KEEP their machines until the light arrows go struct and the
> pairs converge -- the cheapest-machinery doctrine, upheld by its own instrument. The
> deletion list (section 3) does NOT execute yet.
>
> Original status line, for the record: RATIFIED 2026-08-18; ratified in design dialogue
> the same day the public composition surface shipped.
> Jason's design, arrived at in dialogue the same day the public composition surface
> shipped: strip the positional ARITY AXIS from the operator machinery and make position a
> SPELLING — a projection into `NodeContext<TNode>` that the value algebra composes over —
> while keeping the positional overloads as one-line SUGAR over that spelling. The key to
> closing composition fully: the last axis standing becomes a composition.

## 1. The idea

Positionality today is an axis: every operator carries a `(TNode, NodePosition)` overload,
and the machinery carries the axis's weight — the join rule, the `Relabels` bit and its
guards at every positional call site, the positional selector structs, the context-shaped
doors. Under this design there is ONE positional operator:

```
tree.WithPosition()   // ITreenumerable<NodeContext<TNode>> — each node paired with its
                      // position in THIS tree, at THIS point in the chain
```

and every positional spelling is value composition over the pair:

```
tree.WithPosition().PruneAfter(nc => nc.Position.Depth == 5).Select(nc => nc.Node)
```

The positional overloads SURVIVE as sugar (ergonomics unchanged):

```
public static ITreenumerable<T> PruneAfter<T>(this ITreenumerable<T> source,
    Func<T, NodePosition, bool> predicate)
  => source.WithPosition()
      .PruneAfter(nc => predicate(nc.Node, nc.Position))
      .Select(nc => nc.Node);
```

(The sugar's return type is whatever the chain's last operator returns — per-capability
closure makes that visible; today's positional overloads already return the plain
contract.)

## 2. The laws that fall out

**The join rule becomes data flow.** The sugar calls `WithPosition()` on the operator's
INPUT, so a positional predicate reads the input tree's emitted labels — the exact
guarantee the join rule enforces today with guard machinery, now true by construction and
visible in the spelling. Capture scope is explicit: after a relabeling operator, the old
capture is simply a tree of stale pairs and the spelling says so; call `WithPosition`
again. (This also makes expressible what today is not: reading PRE-relabel coordinates
downstream of a relabeling operator, by capturing early — deliberately, legibly.)

**The gating retreats into the member (the door-optimality law).** `WithPosition` is a
position-reading leg, so it must not splice into a relabeling chain (the composed selector
would read source coordinates). The light tier splices it always — structurally
label-preserving. The general driver decides splice-or-stack from a PRIVATE relabels
flag — its own representation choice, its own knowledge. `Relabels` leaves the interface;
every extension-side guard dies; what survives is one private field steering one door.

**The erasure argument (why the pair is ~free).** In a collapsed chain the
`NodeContext` pair is a mid-chain intermediate inside one composed `GetResult` call —
minted, tested, stripped, on the stack (the emission mint; the output-reachability
erasure rule). This holds through the general driver: `ComposedResultSelector` nests in
the type and path state stores only the chain's FINAL output, which is post-strip. The
composed machine for the sugar is character-for-character what today's positional
overloads build — the overloads are revealed as sugar over the algebra that already
existed. The pair genuinely travels only where the chain cannot collapse: across a
barrier (Do / Hide / the memo seam), through a buffer, or when the pair itself is the
materialized product.

## 3. The deletion list (the payoff)

- The positional overloads' dedicated code paths (~15 operators × composite/narrow
  widths) — each becomes a one-line sugar.
- The join rule as machinery: every `Relabels` guard at every positional call site.
- `Relabels` from `ISelectWhereTreenumerable` (retreats to a private driver field).
- `PositionalWhereResultSelector`, `PositionalPruneBeforeResultSelector`.
- The context-shaped doors on `ISelectWhereTreenumerable` (the value doors + the
  `WithPosition` leg cover their work).
- The public-door positional question, permanently: the citizenship never needs
  positional seats — positional spellings are sugar over `WithPosition` + the VALUE
  doors already shipped. (The IPruneAfter context-door proposal from the same dialogue
  is WITHDRAWN in favor of this.)

## 4. Known costs and divergences

1. **Machinery divergence, benchmark-gated:** positional `PruneAfter` over a PLAIN source
   today builds the bespoke predicate-only prune driver; the sugar builds the
   `SelectPruneAfter` passthrough with a composed selector. Same tier, marginally
   different machine. The witness pair arbitrates before the bespoke path is rerouted.
2. **Barrier-crossing chains** pay the pair width (the scan pair-product price class,
   +13-20% on Chain rows when pairs were the OUTPUT). Accepted the same way the scan
   pairing was: the pair is load-bearing where it survives.
3. **Positional absorption reach is forfeited** (open ruling): a citizen cannot see a
   depth predicate through the `WithPosition` wrapper, so e.g. a decoder absorbing
   `Depth == k` to skip subtree parsing is not expressible through the citizenship.
   Either a deliberate won't-do, or a future `WithPosition` absorption door.

## 5. Stages (land-then-distill; every stage gated by the battery + RepresentationPinTests)

- **Stage A — the operator, additive.** `WithPosition` as sugar over the positional
  Select pair (`Select((n, p) => new NodeContext<TNode>(n, p))`): rides ALL existing
  machinery — context door, join rule, stacking — so it is correct by the existing
  battery from birth. Pins: representation (plain → light Select wrapper; light chain →
  in-tier merge; relabeling driver → stack) + behavior (the pair carries emitted labels,
  including after Where — the join-rule-as-data pin). Surface-map row. NO existing path
  moves.
- **Stage B — the overloads become sugar** (witness-gated, consult on the rows first):
  each positional overload rewrites to the `WithPosition` spelling; the battery proves
  extensional equality; the witness pair prices the plain-source prune divergence (§4.1)
  and the collapsed-chain parity claim (§2 erasure).
- **Stage C — the axis demolition:** positional selector structs, context doors,
  `Relabels`-from-the-interface (private retreat), the extension guards. Stage 0-style
  pins re-anchored to the new machines where representation deliberately changes.
- **Stage D — the record:** docs, surface map, supersession notes; the composition
  workstream's actual closure.

## 6. Open rulings

1. §4.3 absorption reach: won't-do, or future door?
2. The witness benchmark rows (consult-first, per the standing rule): proposed —
   a `WithPosition` family or Compose-family rows pairing the sugar spelling against
   today's positional overloads, DFT/BFT, collapsed and barrier-broken, plus the
   plain-source prune pair for §4.1.
3. `WithPosition` return type: plain `ITreenumerable<NodeContext<TNode>>` vs the Select
   citizenship (it IS a projection — the citizenship comes free via the light wrapper).
   Lean: whatever the light wrapper already is; no new surface.
