FROM mcr.microsoft.com/devcontainers/base:3-ubuntu24.04

RUN apt-get update \
    && apt-get install -y --no-install-recommends just ripgrep gh \
    && rm -rf /var/lib/apt/lists/*
