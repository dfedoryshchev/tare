# Changelog

All notable changes to this project are documented in this file.

## 0.1.0 - 2026-07-11

### Added
- Markdown block parser with source spans (`MarkdownBlocker`, `Block`, `BlockKind`)
- Sentence splitter and specific-claim extraction (`SentenceSplitter`, `ClaimExtractor`, `Claim`)
- Grounding check and grounding-gap metric (`GroundingSignal`, `GroundingResult`)
- Density and restatement signal (`DensitySignal`, `DensityResult`)
- Facts-cannot-be-filler override (`FactDetector`, `FillerLexicon`)
- Scoring bands and span-level findings (`Analyzer`, `Band`, `Finding`, `Severity`)
- `tare analyze` command wiring the pipeline end-to-end with a span-level report (`Reporter`)

### Known limitations
- Deterministic signals only; the optional bounded LLM pass is not wired yet
- No configuration or thresholds file
- No structured (JSON) output
