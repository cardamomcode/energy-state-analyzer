# Justfile for energy-state-analyzer

default:
    @just --list

# Install npm dependencies
install:
    npm install

# Build the extension bundle (dev mode)
build:
    npm run compile

# Rebuild on file changes
watch:
    npm run watch

# Lint src/**/*.ts
lint:
    npm run lint

# Format src/**/*.ts in place with Prettier
format:
    npm run format

# Check formatting without writing changes (used by CI)
format-check:
    npm run format-check

# Run the energy-state-analyzer CLI against local file(s)/dir(s), e.g. `just analyze src/extension.ts`
analyze *paths="src":
    npm run analyze -- {{paths}}

# Compile tests, compile, lint, then run the VS Code extension test host
test:
    npm run pretest
    npm test

# Production build + package into a .vsix via vsce
pack:
    npm run package
    npx @vscode/vsce package

# Install .NET tools (ShipIt)
setup:
    dotnet tool restore

# Phase 0 spike: fable (JS target) -> node ESM shim -> run the Scriptorium suite under Node.
# Green means Fable emits JS, Node consumes it with a correct exit code on BOTH the pass and
# fail paths (see docs/fable-rewrite-plan.md). `--noCache` sidesteps the documented Fable
# `[<Emit>]` cache-instability gotcha; the {"type":"module"} shim makes Node treat the ESM
# output as a module (avoids ERR_REQUIRE_ESM).
spike:
    dotnet fable tests/EnergyState.Tests.fsproj --lang javascript --outDir spike-js --noCache
    echo '{"type":"module"}' > spike-js/package.json
    node spike-js/Main.js

# Preview the release PR ShipIt would open/update for commits since the last release
shipit *args:
    dotnet shipit --allow-branch main --skip-invalid-commit {{args}}

# Remove build artifacts
clean:
    rm -rf dist out *.vsix
