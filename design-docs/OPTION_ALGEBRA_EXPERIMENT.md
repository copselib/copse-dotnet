# The Option<T> experiment

> **Status: EXPERIMENT, awaiting a ruling** (built 2026-08-19 on `feature/option-algebra`,
> four commits, unmerged, unpushed). The 2026-08-16 result-struct-vs-`Option` chat
> reaffirmed the bespoke miss-carriers and the admission rule wrote that down
> (OPERATOR_SURFACE_MAP.md §0). This branch builds the refused alternative and measures it
> on the three axes named when the experiment was commissioned: benchmarks, boilerplate,
> ergonomics. Nothing here overturns the standing rule; it is the evidence a ruling can be
> made on.
>
> Not to be confused with the `Option`-LABELLED SENTINEL COMPLETION of the carrier
> (CATEGORY_THEORY_SURVEY.md §11) -- a different question, canonized as semantics and
> refused as representation, and untouched by this branch.

## 1. What was built

| Commit | Stage | What |
|---|---|---|
| `868bb6c9` | **A** | `Option<TValue>` in Copse.Vocabulary; `ChildResult`, `ParentResult`, `AsyncTreeWalkerResult` and their three generated twins deleted; every probe, pull and step re-typed |
| `3f44ce60` | **B** | the algebra spent at the call sites: `Map` at the doors and the steps, `TryGetValue` in the climbs and axis scans, `AsyncOption` for awaited chains |
| `af21dafb` | **C** | the algebra taken back OUT of the three walker steps (the per-node call), everything else in B kept |

`Option<TValue>` is a `readonly struct` carrying a public `HasValue` flag beside a public
`Value` field -- deliberately the same shape the deleted family had, so stage A measures
the type swap and nothing else. It carries `Map`, `Bind`, `Match`, `Or`, `Where`,
`GetValueOrDefault`, `TryGetValue`, and capture-free `Map`/`Bind` overloads that take the
state as an argument so a delegate in a loop stays a cached static. It has NO equality
members: the library compares no values, and an option over a value must not be the type
that starts.

It lives in Copse.Vocabulary because that is what the Core contracts speak, and because it
is color-neutral: **one hand-written type serves both colors where the family needed three
sources and three generated twins.** The async color adds `AsyncOption` (map and bind over
an option in hand and over a step's pending option); the sync color needs no twin, so the
codegen manifest loses three entries and gains none.

## 2. Benchmarks

BufferProbes is the instrument -- it is the only family that reaches the probe/topology
layer, and its rows drive `TryGetChildAt`/`TryGetParent`/`TryGetRootAt` through walker
sweeps. PreorderTraversal rides the child-enumerator pull instead and never touches a
walker, so it serves as the control.

Job.Default, this dev container, two runs per stage (three for A, whose first row lost one run to a truncated capture; absolutes are a shared-runner draw and
mean nothing across machines; the within-stage repeats are the signal):

| Row (ms) | baseline | A: type swap | B: algebra in the step | C: algebra out of the step |
|---|---|---|---|---|
| Walk_over_MaterializedPreorder | 20.67, 7.93 | 7.78, 8.08 | **24.62, 24.84** | 8.10, 7.99 |
| Walk_over_MaterializedLevelOrder | 7.28, 7.05 | 7.88, 7.09, 7.19 | **8.43, 8.61** | 7.22, 7.26 |
| Walk_over_MemoizedPreorder | 112.9, 121.0 | 116.3, 122.7, 111.0 | **138.8, 139.7** | 111.5, 112.2 |
| Walk_over_MemoizedLevelOrder | 108.5, 114.1 | 110.0, 109.7, 112.4 | **136.9, 137.1** | 110.6, 114.0 |
| LeaffixScan_over_MaterializedPreorder | 36.8, 44.8 | 39.1, 37.1, 41.9 | 49.4, 37.5 | 36.7, 38.5 |
| PreorderTraversal Chain / Forest / Binary / Triangle | 10.68 / 4.18 / 119.0 / 35.14 | 10.77 / 4.19 / 117.1 / 34.95 | 10.99 / 4.14 / 118.0 / 35.22 | -- |

**The type swap is free.** Every stage-A row sits inside its baseline's own spread, and the
control family does not move at all. The layouts are identical (a flag beside a payload,
same order, same size), and nothing on the pull path gained an instruction.

**A delegate in a per-node step is not free: 19-23%, reproducibly.** Stage B put `Map` in
`MoveToParent`/`MoveToChild`/`MoveToRoot` -- the one place the library evaluates a partial
operation once per NODE rather than once per walk -- and every walker row rose together
across both runs, while the control family stayed flat. The delegate is capture-free and
allocates nothing; the cost is that the call stops the step's construction from inlining
into the sweep.

**Stage C proves the attribution.** Reverting exactly those three methods, and keeping every
other algebra call site, returns every row to baseline. So the rule is not "the algebra
costs" -- it is **"a delegate in a per-node step costs, and none of the other call sites
do."** The doors run once per walk, the axis scans once per child group, and neither shows
up in any row.

**One row is bimodal and must not be read as a result.** `Walk_over_MaterializedPreorder`
answered 20.67 ms and 7.93 ms on IDENTICAL baseline code. Stage A landed twice in the fast
mode, stage B twice in the slow one, stage C twice in the fast one. Whatever selects the
mode (code layout, most likely), it is not the option -- a 2.6x "win" was on the table for
stage A until the baseline re-run produced the same number. Any claim about this row needs
the CI testbed, not this container.

## 3. Boilerplate

Whole branch, `cc1bb101..HEAD`, 93 files:

| | insertions | deletions | net |
|---|---|---|---|
| new (`Option.cs` 106, `AsyncOption.cs` 53) | 159 | 0 | +159 |
| existing hand-written sources (59 files) | 346 | 516 | **-170** |
| generated twins (32 files) | 138 | 284 | **-146** |
| **total** | 643 | 800 | **-157** |

The 159 new lines replace 160 deleted ones (six type files, three of them generated), so
the type swap is a wash by construction and everything below the line is call-site
collapse. Three shapes carried nearly all of it:

- **the LIFT** -- a probe's hit becomes a walker. Nine sites (three steps, six walkable
  doors), each a four-line ternary over a freshly named result, each now one `Map` with the
  topology passed as state. (Stage C returns three of the nine to the ternary for the
  measured reason above; the six doors keep it.)
- **the CLIMB** -- step until the miss. `while (true)` with a mid-body guard and a temporary
  becomes a `while` whose condition IS the step, `TryGetValue` putting the successor stance
  straight into the loop variable. The walker axes lost 49 lines to this shape alone.
- **the SCAN** -- probe an indexed axis until the miss. The root and child loops in
  `GetHandles`, `GetHandlesWithValues`, `Invert`, `LeaffixScan` and the axes shed their
  break-guard and their temporary the same way.

Explicit `.HasValue` reads in hand-written sources: **117 → 97**.

Where the algebra does NOT help, and the code was left alone:

- a two-armed branch on a step (`LeaffixScan`'s frame push/close) -- `Bind` short-circuits
  a miss, it does not express "and on a miss, do this other thing" any more cheaply than an
  `if`;
- an awaited projection inside the projection (the topology child enumerator reads the
  value with a second await);
- any guard whose hit-path is a block rather than an expression. **C# has no do-notation;
  bind stops at the first statement.** This is the ceiling on the whole boilerplate story.

## 4. Ergonomics

**What got better.** Awaited chains compose: `await walker.MoveToChildAsync(0)
.BindAsync(child => child.MoveToChildAsync(1))` is a two-step climb, miss included, with no
temporary and no guard -- unwritable with the bespoke family. The try-pattern face
(`TryGetValue`) turns four-line guards into loop conditions. Providers implement ONE shape
instead of learning three, and a consumer who knows `Option` knows all of them.

**What got worse, and it is exactly what the admission rule predicted.** `HasChild` false
said "past the last child"; `HasParent` false said "this handle is a root"; `HasWalker`
false said "the step had nowhere to stand". `HasValue` false says nothing at all -- the
sentence a reader has to reconstruct from the method name. The types themselves stop
naming their subject:

```
ChildResult<HandleAndValue<THandle, TValue>>
Option<NodeAndSiblingIndex<HandleAndValue<THandle, TValue>>>
```

That second spelling is what a consumer now meets in IntelliSense, in compiler errors, and
in the XML docs.

**Three concrete costs the build turned up, none of them predicted:**

1. **`Value` is the most collided-with member name in the library.** The walker tier's
   `LeaffixScan` frame is a tuple with its own `Value` element; the moment `Walker` stopped
   being the member name, `frame.Value` meant two things and the compiler caught it. A
   domain-named member cannot collide this way.
2. **Nesting broke the codegen.** The async->sync transform unwrapped `ValueTask<X>` with a
   regex good for two levels of nested generics; the option adds a third and the transform
   emitted `void<...>`. Fixed here (balanced-bracket matching, any depth), but the lesson
   stands: a generic wrapper deepens every signature it touches, and tooling that hand-rolls
   type parsing pays for it.
3. **`TryGetValue` assigns on the miss too.** A climb that reuses its loop variable as the
   out target ends holding a `default` stance. Every site here lets the variable die with
   the loop, so the branch is correct -- but the shape invites a reader to use it after,
   and the bespoke family had no such edge.

Minor, but real: the lambda parameter in a `Map(topology, (topology, root) => ...)` shadows
the enclosing local, which the net48/netstandard legs reject (CS0136) even though the
modern async leg accepts it. Capture-free algebra means naming the state twice.

## 5. What this does not answer

- **The CI testbed has not seen any of it.** Every number above is one container's draw,
  and one row is bimodal on identical code.
- **The pull path was measured only through PreorderTraversal.** `IChildEnumerator.MoveNext`
  now returns `Option<NodeAndSiblingIndex<T>>`; the control rows say the swap is free there,
  but no benchmark drives a foreign provider's enumerator.
- **`ScanEvent` and `SelectWhereResult` were left alone**, correctly: neither is an option.
  `SelectWhereResult` carries (value, strategies) and encodes rejection IN the strategies;
  `ScanEvent` is `Ok` + `HasValue` + `Terminator`, which is at best
  `Option<(bool, char)>` -- the inner pair still needs a name. Evidence that the option
  composes WITH named payloads rather than replacing them.
- **The public surface breaks** (pre-beta): every walkable/topology signature that spoke a
  result struct now speaks an option.

## 6. The rulings this asks for

1. Does the boilerplate collapse (-170 hand-written lines, -146 generated, 20 fewer guards)
   buy back the naming loss the admission rule was written to protect?
2. If yes in part: is there a middle -- keep `Option<T>` for the WALKER tier (where the
   climbs and chains live) and leave the child pull's miss named, or the reverse?
3. If no: the admission rule stands as written, and this branch is the evidence for it --
   §0 gains a line pointing here so the question is closed by measurement rather than
   re-argued.

Whichever way it goes, two artifacts here are worth keeping regardless: the codegen's
depth-independent `ValueTask<X>` unwrap, and the per-node-delegate finding -- which applies
to any algebra the library adds, not just this one.
