# Change Log

All notable changes to the "energy-state-analyzer" extension will be documented in this file.

Check [Keep a Changelog](http://keepachangelog.com/) for recommendations on how to structure this file.

## [Unreleased]

## [0.0.7]

- Added a primitive obsession detector (Python only): flags consecutive same-typed primitive parameters (e.g. `lat: float, lon: float`, a swap risk) and variables compared against 3+ distinct string literals (a de facto enum encoded as strings).

## [0.0.6]

- Added a file coherence check that flags files with too many large functions (configurable via `energyStateAnalyzer.coherence.largeFunctionLines` and `.maxLargeFunctions`), independent of total function count, so languages like F# with many small functions per module aren't penalized.
- Initial release