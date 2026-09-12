#!/usr/bin/env bash
set -euo pipefail

# Prepare Docker-created volume roots for installs under the development user.
# decision: change only mount-root ownership; avoid walking caches on every rebuild.
sudo chown "$(id -u):$(id -g)" node_modules /home/vscode/.npm \
    /home/vscode/.nuget /home/vscode/.nuget/packages

just setup
npm ci
