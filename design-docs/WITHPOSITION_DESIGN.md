# WithPosition (design)

> **Status: RATIFIED 2026-08-18; Stage A in progress on `feature/with-position`.**
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
