# Military Logistics Sim (First Look)

This repository contains a first runnable slice of a backend-authoritative military logistics simulator with:

- deterministic tick-based simulation engine
- REST API for scenario validation and session control
- mock real-world and population enrichment snapshots
- thin read-only Avalonia operator client

## Solution layout

- `docs/` starter design docs and manifest
- `schemas/` starter scenario + JSON schemas
- `src/Sim.Domain` domain model
- `src/Sim.Contracts` shared API and renderer contracts
- `src/Sim.Engine` deterministic tick simulation
- `src/Sim.Application` session orchestration and validation
- `src/Sim.Infrastructure` file scenario source + mock enrichment provider
- `src/Sim.Api` REST API
- `src/Operator.Client` thin read-only Avalonia app
- `tests/Sim.Tests` simulation tests

## Run API

```bash
export DOTNET_CLI_HOME="${HOME}/.dotnet"
dotnet run --project src/Sim.Api/Sim.Api.csproj
```

API binds to `http://localhost:5080`.

## Run Operator Client

```bash
export DOTNET_CLI_HOME="${HOME}/.dotnet"
dotnet run --project src/Operator.Client/Operator.Client.csproj
```

Use the UI buttons in order:

1. `Create Session`
2. `Start`
3. `Refresh`

## Test

```bash
export DOTNET_CLI_HOME="${HOME}/.dotnet"
dotnet test tests/Sim.Tests/Sim.Tests.csproj
```

Windows fallback uses the same `dotnet` commands with `DOTNET_CLI_HOME` pointed at your Windows SDK cache location.

## Dev Toolbar

- Run common workflows from terminal:
  - `./scripts/dev-toolbar.sh start`
  - `./scripts/dev-toolbar.sh build`
  - `./scripts/dev-toolbar.sh test`
  - `./scripts/dev-toolbar.sh lint`
  - `./scripts/dev-toolbar.sh deploy` (safe stub)
  - `./scripts/dev-toolbar.sh git-pull`
  - `./scripts/dev-toolbar.sh git-push`
- VS Code task labels use the same command set: `dev:*` and `git:*`.
