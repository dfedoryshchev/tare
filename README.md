# tare

Output-quality checks for long-form prose. `tare` reads a markdown document and reports
where it makes specific claims without sourcing them, where it restates itself, and where it
leans on stock filler. It scores the document, puts it in a band, and can fail a build on
that band.

It never says "this is AI". It measures substance, not style. Every finding carries a source
span, a stable rule id and a reason a reader can check against the text, so a verdict is
always arguable rather than oracular.

## Who this is for

The same report serves two readers.

- **A person editing a draft.** Run it before publishing and get a `file:line` list of the
  places a reader would ask "says who?".
- **An eval loop grading model output.** `--json` emits a stable report and the process exit
  code is the verdict, so a generated document can be scored without a human in the loop and
  without a second model deciding what "good" means.

Nothing in the analyzer knows or cares which of the two wrote the document. That is
deliberate: the checks are authorship-blind, and the corpus keeps fairness cases in it
(clipped operational writing, English written by someone who did not grow up speaking it) so
the false-positive rate has to account for prose that is terse or non-native rather than bad.

## What it checks

| Rule | Severity | Fires when |
|---|---|---|
| `GROUND001` | warning | A specific claim (a number, a causal assertion, an appeal to authority) has no source signal in its sentence or an adjacent one. |
| `DENSITY001` | info | A prose block restates its heading or the previous paragraph and adds little novel content. |
| `FILLER001` | info | A prose block hits two or more phrases from the filler lexicon. |
| `CITE001` | info | A cited source was read and does not back the claim it is cited for. Optional, off by default. |

A source signal is a link, a footnote marker, an inline citation, a named authority, or plain
prose attribution to a particular record ("according to the dashboard", "per the handover
document"). A block carrying a concrete fact is never flagged as filler, however stock its
phrasing. Claims whose only specific content is a wall-clock time are left out of the
grounding metric entirely: an incident log has no source to cite for having been there.

Code fences are skipped, so sample output inside a document is not read as a claim about the
world.

## Running it

Needs the .NET 10 SDK. Nothing is published as a package yet; run it from source.

```
git clone https://github.com/dfedoryshchev/tare.git
cd tare
dotnet run --project src/Tare.Cli -c Release -- analyze corpus/cases/slop-ungrounded-stats.md
```

```
slop-ungrounded-stats.md  -  Slop (score 0.60)

  block 1
    slop-ungrounded-stats.md:3  warning  GROUND001  specific claim: no source signal near the claim
  block 2
    slop-ungrounded-stats.md:5  warning  GROUND001  specific claim: no source signal near the claim
  block 3
    slop-ungrounded-stats.md:7  warning  GROUND001  specific claim: no source signal near the claim
  block 4
    slop-ungrounded-stats.md:9  warning  GROUND001  specific claim: no source signal near the claim

4 finding(s), band Slop
```

That document is four paragraphs of percentages and not one citation, which is the case the
grounding rule exists for.

`--json` swaps the console report for the machine-readable one:

```
dotnet run --project src/Tare.Cli -c Release -- analyze corpus/cases/watch-light-filler.md --json
```

```json
{
  "score": 0.2,
  "band": "Watch",
  "findings": [
    {
      "ruleId": "FILLER001",
      "severity": "Info",
      "blockIndex": 1,
      "startLine": 3,
      "endLine": 5,
      "startChar": 11,
      "endChar": 256,
      "message": "filler phrasing: at the end of the day, when it comes to"
    }
  ]
}
```

Rule ids are part of the contract: an existing id never changes meaning.

## How the score works

```
score = 0.6 * grounding gap + 0.4 * density rate
```

- **grounding gap** - the share of specific claims that cite nothing.
- **density rate** - the share of prose blocks flagged as restatement or filler.

The bands are cutoffs on that score:

| Band | Score |
|---|---|
| `Clean` | below 0.2 |
| `Watch` | 0.2 up to 0.5 |
| `Slop` | 0.5 and above |

## Configuration

Every weight, cutoff and threshold is data. `tare.json` in the working directory is picked up
automatically, or point at one with `--config`:

```json
{
  "weights": { "grounding": 0.6, "density": 0.4 },
  "bands": { "watch": 0.2, "slop": 0.5 },
  "density": { "highOverlap": 0.5, "lowNovelty": 0.35, "minFillerHits": 2 },
  "grounding": { "minClaims": 3 },
  "filler": []
}
```

Any field left out keeps its default, so a partial file is enough to nudge one threshold. The
`filler` array extends the built-in lexicon; it never replaces it. A malformed config is a
clean error rather than an analysis run with half-applied settings.

A longer list can live in its own file. Set `"fillerFile": "filler.txt"` and tare reads it
relative to the config: one phrase per line, blank lines and lines starting with `#` skipped.
Its phrases extend the lexicon the same way the array does, and a missing file is an error.

## The pre-publish gate

`--fail-on <band>` turns a report into a decision. The bar is the lowest band that fails, so
`--fail-on slop` is the ordinary CI setting and `--fail-on watch` is the strict one.

```
dotnet run --project src/Tare.Cli -c Release -- analyze corpus/cases/slop-ungrounded-stats.md --fail-on slop
```

```
slop-ungrounded-stats.md  -  Slop (score 0.60)

  block 1
    slop-ungrounded-stats.md:3  warning  GROUND001  specific claim: no source signal near the claim
...
4 finding(s), band Slop
gate: slop-ungrounded-stats.md is slop, at or above the --fail-on bar of slop
```

That run exits 2. The report still prints - a refusal owes the reader the findings that
explain it - and the gate line goes to stderr, so a `--json` run stays a single parseable
document on stdout.

| Exit | Meaning |
|---|---|
| 0 | The draft met the bar, or no bar was set. |
| 1 | tare never got as far as an opinion: a missing file, an unreadable config, an argument it could not parse. |
| 2 | The draft was read and refused. |

Keeping 1 and 2 apart is the point. A build that cannot tell "your prose is thin" from "your
config is malformed" points the wrong person at the wrong fix.

`--fail-on clean` is accepted and refuses every document, because every document is at least
clean. That is a real setting for proving the step is wired up.

Those three exit codes are all a pipeline needs; no adapter is required. A flow runner can
call `tare analyze <file> --fail-on slop` as a command step, stop on a non-zero exit and keep
the stderr line as the reason. [agentweft](https://github.com/dfedoryshchev/agentweft) does
exactly that, and its
[tare-gate example](https://github.com/dfedoryshchev/agentweft/tree/master/examples/tare-gate)
is a runnable version: a reviewer step told to source every number, and tare as the check on
whether it did.

## Citation verification (optional, off by default)

The deterministic pass counts a claim as grounded when it carries a link. It does not read
what is on the other end of it, which leaves link-dressed prose scoring clean. `--verify`
closes that gap: for each specific claim that cites a URL inside its own sentence, the page is
fetched and a single bounded model call is asked what that text does with the claim -
supported, contradicted, overstated, or unknown. Only the middle two produce a finding.

It is bring-your-own-key and needs `ANTHROPIC_API_KEY`. Without one, nothing is fetched,
nobody is asked, and the run says so:

```
dotnet run --project src/Tare.Cli -c Release -- analyze corpus/cases/clean-grounded-report.md --verify
```

```
note: --verify needs ANTHROPIC_API_KEY to be set; nothing was verified
clean-grounded-report.md  -  Clean (score 0.00)

  no findings

0 finding(s), band Clean
```

Three constraints hold whether or not a key is present:

- **It cannot move the score.** The deterministic report is produced first and carried over;
  verification can only append a finding, at `info`, below the ungrounded-claim warning. The
  deterministic tier was calibrated against a labeled corpus and this tier has none.
- **Every failure path answers "unknown", never a verdict.** A refused key, a busy service, a
  reply cut short, a reply in the wrong shape, a verdict word nobody recognises, a source that
  will not load: each is a fact about the run, not about the writing, and none of them may
  cost an author a mark.
- **The fetched page is untrusted.** A draft under analysis names the URL, so whoever wrote
  that page can write to the model too. The text is fenced and marked as quoted material,
  the verdict can only be one of four words, and the reason is collapsed to one capped line.
  Two HTTP clients are used and only one of them carries the key, so the credential can never
  follow a URL chosen by the document.

One claim costs one call, and a run is capped at 25 of them. Past the ceiling the answer is
"unknown" with a reason that says the budget is spent, so a truncated run says so instead of
looking cheap. The model, the endpoint, the ceilings and the cap are library options; the CLI
runs them at their defaults and does not expose flags for them.

Cited URLs are filtered before anything is fetched. The allowed set is stated positively - http
or https, no credentials, a public hostname - and loopback, private, carrier-grade NAT,
link-local, multicast and reserved addresses, private network suffixes and single-label hosts
are declined rather than probed. A document naming `http://169.254.169.254/` should not turn
the tool into a request forwarder.

## Measuring the analyzer

The claim this repository is willing to make about its own accuracy is the one `bench` prints.

```
dotnet run --project src/Tare.Cli -c Release -- bench
```

```
corpus: 15 cases, 3 rules

  precision  100.0%   (6 hit, 0 false)
  recall     100.0%   (0 missed)
  fp rate      0.0%   (of 39 that should stay quiet)
  f1         100.0%
  bands      14/15

known gaps (not counted against the run)
  slop-filler-padding.md  labeled Slop, scored Watch (0.40)

no regressions
```

Every rule is a separate yes/no prediction per document, which is what makes precision and
recall mean anything here: a document is not "slop or not", it is a set of rules that should
or should not have fired. Cases are written by hand and labeled by reading them, never by
recording whatever the analyzer happened to output - a label that disagrees with the code is
either a bug to fix or a threshold to move, and the corpus is where that gets settled.

`bench` exits 1 on a regression against the labels. A declared known gap is reported and does
not fail the run, but it is still counted in the metrics, so closing one means deleting the
declaration rather than muting a rule.

`--corpus` points at a different labeled set (it expects a `manifest.json` and a `cases/`
directory, and defaults to `corpus`), and `--config` applies the same thresholds `analyze`
would use, which is how a threshold change gets argued with numbers rather than opinions.

## Limits

The tool exists to catch claims that outrun their evidence, so here are its own.

- **The numbers above come from 15 hand-written synthetic cases.** The corpus is small and
  deliberately skewed toward the boundaries, so 100% precision on it is a statement about
  those 15 documents, not a general accuracy claim.
- **One known gap is open and `bench` prints it.** A document made entirely of stock
  transitions tops out at 0.40 and lands in `Watch`. Nothing in it is a claim, so the
  grounding half of the score is zero and density is weighted 0.4 - such a document cannot
  reach the 0.5 `Slop` cutoff no matter how bad it gets. The band is what a reader would say;
  the weighting is what has to move.
- **Grounding is citation hygiene, not truth.** Without `--verify` a claim that carries any
  link scores grounded, however unrelated that link is.
- **`CITE001` has no labeled corpus behind it.** `bench` measures the three deterministic
  rules only. How often the model call is right is unmeasured here, which is why the finding
  is advisory and cannot move a score.
- **The URL filter is a pure check on the URL.** It cannot see a DNS resolution, so a public
  hostname that resolves to a private address passes it.
- **Both fetchers walk the redirect chain themselves rather than leaving it to the transport.**
  The filter is re-applied at every hop, so a public URL that redirects to a private address is
  refused at the hop that names it.
- **The CLI wires the support question and nothing else.** The adapters that answer whether a
  cited URL or DOI exists at all are in the library and are not reachable from `analyze`.
- **Markdown in, console or JSON out.** There is no editor integration, no SARIF, no published
  package, and the filler lexicon can only be extended through config, not replaced.

## Layout

```
src/Tare.Core     the engine: pure, no file, network or model access
src/Tare.Cli      the analyze and bench verbs
src/Tare.Http     the only project that reaches the network
tests/            xUnit; the adapters are covered through an injected transport,
                  so no test touches the network
corpus/           the labeled cases bench scores against
```

The core takes no IO of its own. It names the citation questions and never learns who answers
them, which is what keeps the whole analyzer testable offline, deterministic and free to run.

## License

MIT. See [LICENSE](LICENSE).
