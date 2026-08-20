# `Option<TValue>` — the typed miss

> **Status: DESIGN RECORD** (ruled 2026-08-20). `Option<TValue>` (Copse.Vocabulary) is the
> library's one spelling of "this operation may produce nothing": the criterion for what
> converts, the closed inventory it was applied to, and the measurements behind both live
> here. This supersedes the 2026-08-16 bespoke-carrier rule (one named result struct per
> miss — `ChildResult`, `ParentResult`, `TreeWalkerResult`; the supersession is recorded at
> the rule, OPERATOR_SURFACE_MAP.md §0). The monadic algebra built alongside the type was
> refused at the witness gate; §5 records why, so it is re-tried by evidence, not re-argued.
> Built and measured on `feature/option-algebra`; the stage-by-stage build history is the
> branch's commit log (`868bb6c9..7c649601`).
>
> Not to be confused with the `Option`-LABELLED SENTINEL COMPLETION of the carrier
> (CATEGORY_THEORY_SURVEY.md §11) — a different question, canonized as semantics and
> refused as representation, and untouched by this design.

## 1. The type

`Option<TValue>` is a `readonly struct`: a public `HasValue` flag beside a public `Value`
field, returned BY VALUE, storing nothing and using no `out` param — the shape that stays
legal in both colors (an `out` param is illegal in an async method; a stored result bloats
the enumerator frame). An option is what an operation returns when the miss is an expected
answer rather than a violation — the parent of a root, the child past the last one, the
step that had nowhere to stand; the exception channel stays reserved for malformed
questions. `default` is the miss. It has NO equality members: the library compares no
values, and an option over a value must not be the type that starts.

Beyond the flag and the field it carries exactly two members, each with a measured reason
and live callers:

- **`TryGetValue(out TValue)`** — the adapter between the typed miss and the language's
  statement grammar, for the one place C# offers nothing else: a loop condition demands a
  `bool` and gives no way to name the value it guards. Its callers are the sync walker-axis
  loops. The BCL-familiar name STAYS (ruled 2026-08-19, reaffirmed 2026-08-20): every
  objection was about the name (`TryGetPreorderStore().TryGetValue(…)` says try twice),
  every argument for it about the shape, and the C# 7.3 floor on the multi-target sync legs
  rules out the pattern-matching alternatives. It assigns `default` on the miss too — the
  try-pattern's own contract — see §5.
- **`Map<TState, TResult>(state, selector)`** — relabel the value in place, leaving the
  miss a miss, with the state passed as an argument rather than closed over so the delegate
  caches as a static. For the once-per-acquisition doors ONLY (its callers are the walker
  mints); per-node code reads the flag directly (§3, the delegate finding).

Everything else is a guard: explicit `HasValue`/`Value` reads are the per-node idiom, and
call sites are written for readability over terseness — a guard whose hit-path is a block
stays a guard.

It lives in Copse.Vocabulary because that is what the Core contracts speak, and because it
is color-neutral: **one 70-line hand-written type where the bespoke family needed three
sources and three generated twins.** The codegen manifest lost four entries (`ChildResult`,
`AsyncTreeWalkerResult`, `ParentResult`, `LevelOrderRead`) and gained none.

## 2. The admission criterion and the inventory

Two clauses decide whether a carrier is option-shaped:

1. **The flag must govern the WHOLE carrier.** An option answers "did this operation
   produce a value?" — `false` means the caller gets nothing. Where the flag governs a
   COMPONENT of a compound answer, and the components are correlated, the thing is a
   variant, not an option, and two independent options cannot express it.
2. **The payload must already be one named thing**, or the option form mints a type for
   it — one type traded for another, plus a hop at every read.

| carrier | clause 1 | clause 2 | outcome |
|---|---|---|---|
| `ChildResult`, `ParentResult`, `TreeWalkerResult` | ✓ | ✓ (`NodeAndSiblingIndex`, a bare handle, a walker) | converted |
| `LevelOrderRead` | ✓ | ✓ (a bare value) | deleted outright — it WAS the option |
| the buffer's two tuple doors (`TryGetPreorderStore`, `TryGetNodeCount`) | ✓ | ✓ | converted (discharging the tuple debt the 2026-08-16 rule recorded) |
| `PreorderRead` | ✓ | ✗ as it stood | converted by DEMOTING it to the payload: the streams read `Option<PreorderRead<TValue>>` |
| `MergeNode` | **✗** | ✓ | **left alone** |
| `ScanEvent` | ✓ for `Ok`, ✗ for the inner flag | ✗ | left alone (and internal) |

The inventory was swept 2026-08-20 with both clauses in hand — try-pattern methods, flag
tuples, `bool Has*`/`Ok`/`Is*` members, and null-as-absence returns — and is CLOSED; new
carriers answer to the clauses, not to this list. What the sweep ruled:

- **Converted**: the six carriers in the table above.
- **Refused, with the reason recorded at the type**: `MergeNode` (a variant — clause 1),
  `ScanEvent` (internal, and its inner flag governs a component — both clauses),
  `ValueTokenStringScanner.TryScanEvent` (two `out`s and a span payload; not option-shaped),
  the serializer's private `TryCommit*` loop helpers (their `bool` is a keep-scanning
  signal, not a miss). Compound frames whose flag governs a component — the `LeaffixScan`
  frame tuple's `Folded`, the Where paths' `out bool` companions — are clause-1 variants.
- **Not a candidate**: `Option.TryGetValue` itself (the type's own statement-grammar
  adapter, §1); the state predicates (`IsBuilt`, `IsComplete`, `IsForestRoot`, and kin) —
  they answer a question about an object and carry no payload whose presence is in doubt;
  the private reference-typed sentinel fields — for a reference type `null` already IS the
  absent case, so an option there would add a flag to say what the reference says on its
  own.

That last point bounds the type's territory: the option earns its place on STRUCT payloads
and on generic `TValue`s that may be structs. Where the payload is a class, C# already has
the encoding.

`MergeNode` is the instructive refusal. Its node always exists; `HasLeft` is PROVENANCE —
which side of the merge this node came from — so the type is a three-state variant
(left-only, right-only, both). A pair of options admits `(None, None)`, a node from neither
tree, which the merge can never produce, and loses the conjunction the domain names
(`HasLeftAndRight`). C# has no discriminated union to encode it properly, so flags with an
invariant are the honest encoding.

## 3. The evidence

BufferProbes was the instrument — the only benchmark family that reaches the
probe/topology layer, driving `TryGetChildAt`/`TryGetParent`/`TryGetRootAt` through walker
sweeps — with PreorderTraversal (which rides the child-enumerator pull and never touches a
walker) as the control. Job.Default, one dev container, repeated runs per stage; absolutes
are a shared-runner draw, the within-stage repeats are the signal.

**The type swap is free.** With the option deliberately laid out field-for-field like the
deleted family (a flag beside a payload, same order, same size), every row sat inside its
baseline's own spread and the control family did not move. The same held for the store
family's per-node reads when `LevelOrderRead` was deleted and `PreorderRead` demoted:
the flat decoders, the replay streams, and the serializer all read inside the
repeat-on-identical-code spread (up to ~5% on these rows, the bar any claim must clear),
in both directions.

**A delegate in a per-node step is not free: 19–23%, reproducibly.** Putting `Map` in
`MoveToParent`/`MoveToChild`/`MoveToRoot` — the one place the library evaluates a partial
operation once per NODE rather than once per walk — raised every walker row together while
the control stayed flat. The delegate was capture-free and allocated nothing; the cost is
that the call stops the step's construction from inlining into the sweep. Reverting
exactly those three methods, keeping every other algebra call site, returned every row to
baseline — so the law is not "the algebra costs" but **"a delegate in a per-node step
costs, and no other call-site tier shows up."** The doors run once per walk, the axis
scans once per child group. This finding applies to ANY algebra the library adds, not just
this one.

Two caveats stand: `Walk_over_MaterializedPreorder` is bimodal on identical code (7.9 ms
and 20.7 ms from the same baseline; any claim about it needs the CI testbed), and the CI
testbed has not seen any of these numbers — every figure is one container's draw. The pull
path was measured only through the control family; no benchmark drives a foreign
provider's child enumerator.

## 4. What the conversion is made of

Whole branch at the ruling, code only (docs excluded): existing hand-written sources
+443/−575 across 71 files (**−132**), the new type +70, generated twins +193/−313 across
40 files (**−120**) — **net −182**. Three shapes carried the collapse:

- **the LIFT** — a probe's hit becomes a walker. A four-line ternary over a freshly named
  result becomes one `Map` with the topology passed as state; the walker-mint doors keep
  it (the steps do not — the delegate finding).
- **the CLIMB** — step until the miss. `while (true)` with a mid-body guard and a
  temporary becomes a `while` whose condition IS the step, `TryGetValue` putting the
  successor stance straight into the loop variable.
- **the SCAN** — probe an indexed axis until the miss, the same way: the root and child
  loops shed their break-guard and their temporary.

## 5. The refusals — why there is no algebra

The full algebra was built, spent at every call site, and then removed member by member as
the sites were rewritten for readability over terseness: `Bind`, `Match`, `Or`, `Where`,
`GetValueOrDefault`, and an `AsyncOption` companion for awaited chains all went — a member
with no callers is not surface, and every candidate site read better as a guard. The
structural reasons, so the question is closed by evidence:

- **C# has no do-notation; bind stops at the first statement.** Any guard whose hit-path
  is a block rather than an expression keeps its `if`. This is the ceiling on the whole
  monadic-composition story in this language.
- **The per-node tier is delegate-hostile** (§3): the one tier where an algebra would run
  hottest is the one tier it is banned from.
- **Bind does not express recovery.** A two-armed branch on a step ("and on a miss, do
  this other thing") is not cheaper through `Bind` than through an `if`.

Costs the build turned up, all standing:

1. **`Value` is the most collided-with member name in the library** — a tuple element or
   local named `Value` beside an option read means two things, and a domain-named member
   cannot collide this way. The compiler catches it; the read pays for it.
2. **A generic wrapper deepens every signature it touches.** The async→sync transform's
   `ValueTask<X>` unwrap broke at three levels of nested generics and was rewritten with
   balanced-bracket matching (kept regardless of this design — it is correct at any
   depth). Tooling that hand-rolls type parsing pays for every wrapper the surface adds.
3. **`TryGetValue` assigns on the miss too** (`default`, the try-pattern's own contract):
   a loop that reuses its stance as the `out` target ends holding a default. Fine where
   the variable dies with the loop — every current site — a bug the moment it is read
   after.
4. **Capture-free algebra names the state twice**, and the lambda parameter shadowing the
   enclosing local is CS0136 on the net48/netstandard legs even where the modern leg
   accepts it.

## 6. What remains open

- **The CI testbed has not confirmed any number here**; the first post-merge CI runs of
  BufferProbes and the store families are the check.
- **The public surface breaks** (pre-beta): every walkable/topology signature that spoke a
  result struct now speaks an option — release-notes flag.
- **The naming loss is real and accepted**: `HasChild` false said "past the last child";
  `HasValue` false says only what the method name implies. The trade — one shape every
  provider implements and every consumer learns once, against three self-naming misses —
  is the ruling this record exists to keep.
