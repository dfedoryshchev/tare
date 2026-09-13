# Changelog

All notable changes to this project are documented in this file.

## 0.2.0 - 2026-09-13

### Added
- `--json` structured output (`JsonReport`)
- Config and thresholds file (`TareOptions`, `tare.json`)
- Labeled corpus and `tare bench` for precision/recall/false-positive measurement (`Bench`, `BenchCase`)
- Citation existence checks: URL HEAD resolve and DOI/Crossref lookup (`IClaimSource`, `HttpClaimSource`, `CrossrefClaimSource`)
- `IClaimVerifier` port and a bring-your-own-key claim-vs-source check wired into `tare analyze --verify`, needs `ANTHROPIC_API_KEY` (`ClaudeClaimVerifier`)
- Pre-publish gate: exit codes and `--fail-on <band>` for CI (`PublishGate`)
- `README.md` and an MIT `LICENSE`

### Fixed
- Grounding check recognizes prose attribution ("according to...", "measured by...") and exempts timestamp-only claims from the grounding rule
- Citation policy is applied on every redirect hop in both HTTP adapters, closing a gap where a followed redirect skipped the check
- Citation policy blocks CGNAT, IPv4 multicast, reserved, IPv6 site-local and IPv6 multicast address ranges, not just the original private-network set

### Known limitations
- Citation checks see the URL a document names, not the address a hostname resolves to; a public hostname resolving to a private address is not caught
- `--verify` needs `ANTHROPIC_API_KEY` set; without it, citation support is reported as unknown rather than checked
- One labeled corpus case (pure filler, no specific claims) scores below the Slop cutoff by design of the density weighting; tracked as a known gap in `tare bench`

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
