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

# Run the F# analyzer. With no arguments it scans src; otherwise arguments are forwarded unchanged.
# Examples: `just analyze tests` and `just analyze --base-ref main --report human`.
analyze *args:
    npm run compile
    if [ -n "{{args}}" ]; then npm run analyze -- {{args}}; else npm run analyze -- src; fi

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
