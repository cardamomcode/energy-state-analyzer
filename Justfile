# Justfile for energy-state-analyzer

default:
    @just --list

# Install npm dependencies
install:
    npm install

# Transpile the F# extension and CLI entries with Fable's JavaScript target
fable:
    npm run fable

# Build the extension and CLI bundles (dev mode)
build:
    npm run compile

# Rebuild the webpack bundles after an F# transpilation
watch:
    npm run watch

# Check F# formatting
lint:
    npm run lint

# Format F# source and tests in place
format:
    npm run format

# Check F# formatting without writing changes (used by CI)
format-check:
    npm run format-check

# Check markdown formatting (used locally and by CI)
md-lint:
    npm run md-lint

# Run the F# analyzer against src/ (or supplied paths). Rebuilds the webpack bundles only when dist/
# is missing; otherwise it reuses the existing bundle so per-file checks stay near-instant. With no
# arguments it scans src; otherwise the remaining args are forwarded unchanged to the CLI.
# Examples: `just analyze`, `just analyze tests`, `just analyze --base-ref main --report human`.
analyze *args:
    @if [ ! -d dist ]; then echo "just analyze: rebuilding before analysis…"; npm run compile; fi
    @paths="{{args}}"; [ -z "$paths" ] && paths=src; npm run analyze -- $paths

# Run the F# analyzers (fsharp-analyzers + G-Research rules) over one or more .fsproj. Emits a
# SARIF report per project under fsharp-analyzer-reports/ for CI code-scanning. With no args, it
# checks src/. Examples: `just fsharp-analyze`, `just fsharp-analyze src/EnergyState.fsproj cli/
# EnergyState.Cli.fsproj`.
fsharp-analyze *args:
    @paths="{{args}}"; [ -z "$paths" ] && paths=src/EnergyState.fsproj; \
    for p in $paths; do \
      echo ">> restoring $p"; dotnet restore "$p"; \
      mkdir -p fsharp-analyzer-reports; \
      echo ">> analyzing $p"; dotnet msbuild /t:AnalyzeFSharpProject "$p" || true; \
    done

# Trigger the CI workflow against a branch (main by default) to scan the default branch and upload
# SARIF to GitHub Code Scanning. PR uploads only populate the PR-level view, so this is how you push
# a scan of main itself. Use after the ci.yml schedule/dispatch change merges. Examples:
# `just fsharp-scan-main`, `just fsharp-scan-main refs/heads/main`.
fsharp-scan-main *args:
    @ref="{{args}}"; [ -z "$ref" ] && ref=main; \
    echo ">> triggering CI on $ref"; \
    gh workflow trigger ci.yml --ref "$ref" || { echo "gh workflow trigger failed (is gh authenticated with write access?)" >&2; exit 1; };

# Transpile and run the F# Scriptorium suite
test:
    npm test

# Production build + package into a .vsix via vsce
pack:
    npm run package
    npx @vscode/vsce package

# Install .NET tools (ShipIt, Fable, Fantomas)
setup:
    dotnet tool restore

# Preview the release PR ShipIt would open/update for commits since the last release
shipit *args:
    dotnet shipit --allow-branch main --skip-invalid-commit {{args}}

# Remove generated bundles, Fable output, and package artifacts
clean:
    rm -rf dist fable-out fable-tests *.vsix
