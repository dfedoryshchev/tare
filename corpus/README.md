# Labeled corpus

Fixtures with known verdicts, used to measure the analyzer instead of guessing at it. Each
case is a small markdown document; `manifest.json` records what the analyzer is expected to
say about it.

```json
{
  "file": "slop-ungrounded-stats.md",
  "band": "Slop",
  "rules": ["GROUND001"],
  "note": "numbers everywhere, not one source"
}
```

- `band` - the expected overall verdict.
- `rules` - every rule id expected to fire somewhere in the document, as a set. Order and
  count do not matter; a rule listed here that stays silent is a miss, and a rule that fires
  without being listed is a false positive.
- `note` - why the case is in the corpus, for whoever reads a failure later.

Cases are written by hand and labeled by reading them, never by recording whatever the
analyzer happened to output. A label that disagrees with the code is the point: it is either
a bug to fix or a threshold to move, and the corpus is where that argument gets settled.

The set is deliberately small and skewed toward the boundaries - the facts override, claims
inside code fences, terse prose with no claims at all. Those are where a scoring change does
damage, and the middle of the range takes care of itself.

Prose here is synthetic. It has to be safe to publish and stable across runs, so nothing in
it is quoted from a real draft.
