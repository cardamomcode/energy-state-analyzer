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

# Compile tests, compile, lint, then run the VS Code extension test host
test:
    npm run pretest
    npm test

# Production build + package into a .vsix via vsce
package:
    npm run package
    npx @vscode/vsce package

# Publish the extension to the VS Code Marketplace
publish:
    npm run package
    npx @vscode/vsce publish

# Remove build artifacts
clean:
    rm -rf dist out *.vsix
