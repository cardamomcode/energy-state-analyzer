# Releasing

This project uses [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt)
for release automation and [Conventional Commits](https://www.conventionalcommits.org/)
for versioning.

## Commit conventions

PR titles must follow the conventional commit format (enforced by CI):

| Prefix    | Version bump | Example                            |
| --------- | ------------ | ----------------------------------- |
| `feat:`   | minor        | `feat: add PHP support`             |
| `fix:`    | patch        | `fix: correct nesting depth count`  |
| `feat!:`  | major        | `feat!: rename config namespace`    |
| `chore:`  | patch        | `chore: update dependencies`        |
| `docs:`   | patch        | `docs: update README`               |
| `refactor:` | patch      | `refactor: simplify detector walk`  |

Other valid prefixes: `test`, `perf`, `ci`, `build`, `style`, `revert`.

## Creating a release

Releases are driven entirely by CI — there's nothing to run locally:

1. Merge PRs to `main` with conventional-commit titles.
2. On every push to `main`, ShipIt analyzes commits since the last release and
   opens/updates a `chore: release energy-state-analyzer@<version>` PR that bumps
   `package.json`'s version (via `npm version`) and updates `CHANGELOG.md`.
3. Merging that PR triggers the release job, which runs `vsce publish` and
   `npm publish` (so the headless CLI is installable via `npx energy-state-analyzer`
   without cloning the repo), then tags the commit and creates a GitHub release.

To preview what the next release PR would contain without pushing anything,
run `just shipit` locally (requires `dotnet tool restore` once, and a `GH_TOKEN`
env var with repo access).

## Prerequisites

- `VSCE_PAT` repository secret (a Marketplace "Manage" PAT — see the publisher
  setup steps in this repo's history/README).
- npm publishing uses [Trusted Publishing](https://docs.npmjs.com/trusted-publishers)
  (OIDC) instead of a long-lived token — no `NPM_TOKEN` secret needed. Since the
  package doesn't exist on npm yet, the very first publish has to be done manually
  once (`npm publish` from a machine logged into the target npm account), then
  configure a trusted publisher at npmjs.com → Packages → energy-state-analyzer →
  Settings → Trusted publishing: org/user `cardamomcode`, repo
  `energy-state-analyzer`, workflow filename `publish.yml`, allowed action
  `npm publish`. After that, CI publishes with no credentials at all — the
  `release` job's `id-token: write` permission handles the OIDC exchange.
- `GITHUB_TOKEN` (provided automatically in Actions) or `gh` CLI authenticated
  locally (for ShipIt to open/update the release PR).
