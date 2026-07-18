# Async→Sync Codegen: the single-source architecture

The async library is the source of truth; the synchronous library is **generated from it**.
Every `.g.cs` file under `src/Copse*/` is the mechanical transcription of a hand-written source
file, produced by `Copse.CodeGen` (Roslyn syntax transforms). The relationship is:

```
Copse.Core.Async / Copse.Async / Copse.Linq.Async / Copse.SimpleSerializer (async halves)
        │  hand-written, reviewed, tested
        ├──────────────────────────────────────────────────────┐
        │                                                      │  (SelectWhere lattice only)
        │                                       Copse.CodeGen phase 1: CompositeToNarrow
        │                                                      │
        │                                                      ▼
        │                          narrow single-dimension async twins   *.g.cs, committed
        │                                                      │
        ▼                                                      ▼
Copse.CodeGen phase 2  (GeneratorManifest.cs drives AsyncToSync.cs over both)
        │  dotnet run --project src/Copse.CodeGen
        ▼
Copse.Core / Copse / Copse.Linq / Copse.SimpleSerializer (sync halves)   *.g.cs, committed
```

Why this direction: the two colors must stay semantically identical forever, and only one can be
canonical. Async can express everything sync can (an `await` over a completed `ValueTask` is the
sync behavior plus a seam); the reverse is not true. So the async file is where the logic lives,
and sync users pay nothing — the generated code has no tasks, no state machines, no async
dependencies, and reads like the hand-written code it replaced (that is a hard authoring
requirement, see "Authoring rules").

The **dimension axis** gets the same treatment for the same reason (see
OPERATOR_COMPOSITION_DESIGN.md, "the narrow halves"): C# generics cannot abstract over which
successor type a composable wrapper constructs, so the narrow (depth-first-only /
breadth-first-only) twins of the SelectWhere lattice are transcribed from their composite-width
sources by `CompositeToNarrow.cs` — the source interface and return types narrow, the lattice
types rename by a closed dictionary, and the other dimension's acquisition method is dropped.
One composite-width async file fans out to five generated ones (narrow async ×2, sync ×3).

## The contracts

1. **`.g.cs` files are committed.** The build never depends on the generator (the Npgsql
   pattern). A consumer cloning the repo builds the sync libraries without ever running codegen.
2. **`GeneratedTwinDriftTests` is the safety net.** It re-runs both transforms in-memory and
   asserts every committed `.g.cs` byte-equals the fresh transcription of its source. If you
   edit a source and forget to regenerate — or hand-edit a `.g.cs` — the suite fails. The
   committed narrow twins are also the sync entries' inputs, so the two tests together prove the
   whole chain is fresh.
3. **One manifest entry per twin.** `GeneratorManifest.cs` maps each source file to its twin:
   the sync entries carry source path, output path, class rename (`AsyncFoo` → `Foo`), and
   target namespace; the narrow entries carry source path, output path, and the kept dimension.
   Nothing is discovered by convention; the manifest is the explicit, reviewable list of what is
   generated.

## What the transform does (`AsyncToSync.cs`)

Mechanical, syntax-level, no semantic analysis:

- strips `async`, `await`, `.ConfigureAwait(...)`; `await foreach` → `foreach`;
  `await using` → `using`; async local functions lose their color;
- `ValueTask<X>` → `X`, `ValueTask` → `void`, `Func<ValueTask>` → `Action`,
  `new ValueTask<X>(expr)` → `expr`;
- **the fast-path probe idiom** (see the note in `AsyncToSync.cs`): a
  `if (!x.IsCompletedSuccessfully) return AwaitThen…Async(x);` guard statement vanishes and
  `x.Result` collapses to `x` — `.IsCompletedSuccessfully`/`.Result` are RESERVED spellings in
  manifest sources (they always mean this idiom); the continuation methods live in async-only
  marker regions;
- identifier renames from the manifest plus the standing dictionary
  (`IAsyncTreenumerable` → `ITreenumerable`, `GetAsyncTreenumerator` → `GetTreenumerator`,
  `AsyncDisposable` → `Disposable`, …) and `Async` prefix/suffix strips;
- using-directive surgery: async-only namespaces dropped (`System.Threading.Tasks`,
  `Copse.Async`, `System.Threading`), async namespaces mapped to their sync twins
  (`Copse.Linq.Async.Treenumerators` → `Copse.Linq.Treenumerators`, …);
- **cancellation elision** — see below;
- output normalized to CRLF (an `.editorconfig` cannot govern programmatic writes).

### Marker regions

For the places where the two colors genuinely differ, the async source carries comment markers:

```csharp
// codegen: begin async-only     -- real code, deleted from the sync twin
// codegen: end async-only
// codegen: begin sync-only      -- commented-out code, uncommented in the sync twin
// codegen: end sync-only
```

An adjacent async-only/sync-only pair is the substitution idiom (e.g. `AsyncTree`'s dispose
thunk becomes `resource.Dispose` in the twin). Typical sync-only members: `IEnumerator.Reset`,
non-generic `Current`, `GetEnumerator()`.

### Cancellation elision

Cancellation is edges-only (see the design note in `AsyncTree.cs`): tokens appear on serializer
I/O, `ValueTask` terminals, `[EnumeratorCancellation]` iterators, and tokenizer
`GetAsyncEnumerator` — never on `MoveNextAsync`/`DisposeAsync`. The transform elides all of it
from the sync twin, keyed on **exact spellings**; deviate and the twin drifts:

- parameter exactly `CancellationToken cancellationToken` (attributes removed with it);
- field exactly `_CancellationToken` plus its ctor assignment;
- standalone `…ThrowIfCancellationRequested();` statements;
- call-site arguments that are the bare identifiers `cancellationToken` / `_CancellationToken`
  (the named form `cancellationToken: cancellationToken` also elides).

Corollaries: place a guard directly above the statement it protects with **no blank line
after it** (the blank survives elision); fully-qualify
`[System.Runtime.CompilerServices.EnumeratorCancellation]` where the file has no other need for
that using (so no orphaned using leaks into the twin).

## Authoring rules for async sources

- The generated file must read like the hand-written file it replaces: keep the original names,
  type spellings, comment placement, and member order. No one-character locals.
- Single-line method signatures where the sync original had one (the rewrapper does not reflow).
- `DisposeAsync` forwarding must be expression-bodied
  (`public ValueTask DisposeAsync() => _Inner.DisposeAsync();`) — a block-bodied
  `return …DisposeAsync();` transcribes to an illegal `return` of `void`.
- Accepted drift is essentially zero; the one known tolerated delta is brace normalization of
  drain-loop bodies.

## Adding a new twin

1. Write the async source in the appropriate `*.Async` project (or the serializer), following
   the authoring rules; use marker regions only where the colors must differ.
2. Add a `GeneratorManifest` entry (async source, twin path, class rename, sync namespace).
3. `dotnet run --project src/Copse.CodeGen`
4. `git status` — review the new/changed `.g.cs` like any other diff.
5. Run the test suites; `GeneratedTwinDriftTests` confirms the committed twin matches.

## What is not generated

- **Hand-written sync that remains:** `TreenumeratorBase`/`Wrapper`, `EmptyTreenumerator`,
  `IChildEnumerator`, the `Copse.Linq.Traversal` shared structs, and `Copse.Primitives`
  (including the `Copse.Disposables` Rx lift — its async counterpart was written independently).
  (Since the 2026-07-14 de-share, the store SPIs, read structs, completed array stores, and
  `ChildResult` are all generated — their async sources are the single source of truth.)
- **Async-only surface (no sync twin by design):** `ToListAsync`, the serializer's
  `TreeSerializer.StreamAsync` deserialize overloads (the sync deserializers predate them and
  have a different shape), `IAsyncCancelable`.
- **Copse.Linq.Experimental** has no async half.
