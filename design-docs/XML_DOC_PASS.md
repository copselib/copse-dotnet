# The XML doc pass (parked)

> **Status: PARKED 2026-08-19**, to run near the alpha→beta exit. Scope, standard, and the
> open questions are worked out below; the work itself has not started. Parked by Jason:
> "leave this here for now... note this work somewhere for when we eventually do a full pass
> over all xml docs. That will likely be closer to exiting the alpha version."
>
> This is a working note, not a ratified design record. Clean it up when the pass runs.

## What the pass is

Bring every public `///` doc to the audience standard in CLAUDE.md ("Comments: current truth
only", and its second test): **a `///` doc is for the CALLER** — what it is, what it
guarantees, how to use it — written for someone with none of the design in their head. Not
for the author, and not a changelog.

## Why it times to the beta exit

`GenerateDocumentationFile` is now ON for every project (src/Directory.Build.props) with CS1591 an ERROR on packable projects (src/Directory.Build.targets) -- these docs SHIP as IntelliSense.
The moment that property goes on — a one-line change any pre-1.0 library eventually makes —
every public doc comment becomes hover text for package consumers. **Turning it on is the
forcing function**: run the pass first, or in the same push.

## The worked example

`Copse.Core.Async/AsyncTreeWalker.cs` (`ee78839`, corrected by `77c3e1f`) — the most extreme
case in the repo, done as the calibration sample. Read that diff to recover the standard.

Its three faults were instructive, because only the first is "history":

1. A paragraph of **architecture** (which project holds contracts, which holds operators) that
   CLAUDE.md's Project Structure section already carried — a second copy with no link to the
   first.
2. **Public API docs** on a packable project reading "the placement-pass ruling", "Stage C's
   internalization closed the ecosystem", "the hiding games" — none of which help a caller
   decide whether they can call `MoveToParentAsync`.
3. A **register** that asserts rather than explains: named events as proper nouns, decisions
   justified where behavior should be described, three claims per sentence with no redundancy
   — the shape of notes for a reader who already holds the whole design.

`THE INVARIANT` paragraph in that same file was kept nearly verbatim. It was always a
contract, written for a caller, and it is the contrast case: one file, one author, one day,
containing both kinds of writing.

## Scope, as measured 2026-08-19

After the composition (`fe85f42`) and scan/dispatch (`6dfee32`) passes landed: **200 flagged
lines across 106 authored files, of which roughly half are genuinely the pattern** — the rest
are false positives or legitimately historical. Priority slice: public `///` docs on packable
projects (roughly a dozen files).

Finder — a review aid, never a gate:

```bash
grep -rEn --include='*.cs' '^\s*(//|///)' src \
  | grep -Ei '20[0-9]{2}-[0-9]{2}-[0-9]{2}|used to |no longer|was deleted|superseded|formerly|previously|the old |went dead'
```

Half of any such list is generated `.g.cs`; fix the async source and codegen carries it. The
detector also under-reports: the register problem (coined proper nouns, capitals as emphasis,
sentences arguing a case) trips no grep. "0 tells" is necessary, not sufficient.

## Open questions — NOT yet ruled

1. **Regression tests.** `WhereBreadthFirstAllocationTests` names "the old WhereAll bug" three
   times. Recommendation: **EXEMPT**. A regression test's subject IS the bug it pins; strip the
   name and the ratio assertion looks arbitrary. Needs a ruling, then the exemption goes in
   CLAUDE.md.
2. **Breaking-change notices.** `TakeSubtreesWhere` carries `BREAKING (pre-beta): returned a
   buffer through 2026-08-17`. The date is arguably load-bearing until real release notes
   exist. Where does it live?
3. **Benchmark dates.** `Copse.Benchmarks` has ~11 flagged lines where dates pin row renames
   and taxonomy changes. Those matter for Bencher series continuity, so the date may earn its
   keep. Probably exempt.

## Known trap

**Docs drift in both directions.** `WALKER_FACTORY_DESIGN.md` §2 claimed the
comonad-invariant-subject law was "stated in the XML doc" when it was stated nowhere in code
(fixed in `77c3e1f`). Only one direction of drift has a grep. When the pass runs, spot-check
design-doc claims of the form "stated in the XML doc" / "the doc says" against the code.
