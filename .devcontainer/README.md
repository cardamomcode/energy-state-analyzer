# Dev container

[![Open in GitHub Codespaces](https://img.shields.io/badge/Open%20in-GitHub%20Codespaces-0078D4?style=for-the-badge)](https://codespaces.new/cardamomcode/energy-state-analyzer)

[![Open in local Docker](https://img.shields.io/badge/Open%20in-Local%20Docker-0078D4?style=for-the-badge)](https://vscode.dev/redirect?url=vscode://ms-vscode-remote.remote-containers/cloneInVolume?url=https://github.com/cardamomcode/energy-state-analyzer)

**GitHub Codespaces** opens the cloud environment creation page; no local Docker is required.

**Local Docker** opens desktop VS Code and clones the repository into a Docker volume.
Install VS Code and start Docker with Linux container support first; the link can
install the Dev Containers extension if needed.

For an existing local checkout, open the repository in VS Code and run
**Dev Containers: Reopen in Container** from the Command Palette.

The container provides .NET 10, Node.js 22 (matching CI), `just`,
`ripgrep`, GitHub CLI (`gh`), and the Ionide F# extension.

Wait for container setup to finish; it runs `just setup` and `npm ci`
automatically. Then use the container terminal to complete the development steps:

```bash
just build
just test
```

`just lint`, `just format`, and `just analyze` also run in that terminal. Press `F5`
in desktop VS Code while connected to the container to launch the Extension Development Host.
After pulling dependency changes, rerun `just setup` and `npm ci`. Use `just install`
when intentionally updating npm dependencies and their lockfile. After changing
the container configuration, run **Dev Containers: Rebuild Container**.

Terminals, tasks, and direct container commands default to the non-root `vscode`
user. An init process handles orphaned child processes from watchers and agent commands.
Linux `node_modules` and npm/NuGet caches live in Docker volumes scoped to this
workspace; they survive rebuilds and are not shared with any Windows dependencies. Build outputs stay in the workspace so the existing cleanup commands continue to work.

For coding agents, run build and validation commands in the container terminal.
From the host, run these commands from the repository root:

```bash
npx --yes @devcontainers/cli@0.89.0 up --workspace-folder .
npx --yes @devcontainers/cli@0.89.0 exec --workspace-folder . just test
```

Use a separate checkout or worktree per concurrent agent to avoid competing writes
to source and generated output. Follow [AGENTS.md](../AGENTS.md) for branch and validation rules.
Authenticate `gh` only when needed for your workflow; credentials are not baked into
the image. The container has writable access to this checkout and `vscode` has
sudo access, so it is a development environment rather than a sandbox for untrusted code.

The feature lockfile records the resolved installer versions; .NET 10 and Node 22
still receive patch updates on fresh builds.
