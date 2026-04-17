# Build Backlog for Codex

## Phase 0 — Foundation
- create monorepo structure
- set up .NET solution
- add shared contracts project
- add Docker Compose for postgres, redis, nats, seq
- define basic lint/test pipeline
- define JSON/YAML scenario schemas

## Phase 1 — Domain + Sim Core
- implement core entities and value objects
- implement tick engine
- implement route progress model
- implement fuel and capacity rules
- implement stock consumption and replenishment
- implement event generation model
- add deterministic random seed support

## Phase 2 — API + Persistence
- build PostgreSQL schema
- add EF Core migrations
- expose scenario CRUD
- expose simulation control endpoints
- expose event timeline endpoints
- expose snapshot endpoints for renderer

## Phase 3 — Operator Client
- scenario editor
- route graph editor
- asset assignment view
- stock dashboard
- incident panel
- playback controls

## Phase 4 — Unreal Bridge
- create gRPC/NATS bridge service
- define scene update contracts
- implement state snapshot push
- implement delta updates
- create Unreal receivers for:
  - node spawn/update
  - movement spawn/update
  - incident markers
  - camera switch requests

## Phase 5 — Unreal Visualization
- base terrain scene
- generic asset placeholders
- convoy movement visualization
- rail movement visualization
- air route visualization
- warehouse/base visual states
- event markers
- operator camera + tactical camera transitions

## Phase 6 — Tactical Feed Layer
- fake/mock stream service
- drone feed panels
- helmet cam panels
- incident-linked camera switching
- replay of feed availability windows

## Phase 7 — After Action Review
- timeline scrubber
- compare planned vs actual
- export CSV/PDF summaries later
- filter incidents by severity/type
- replay synced with 3D scene

## Phase 8 — Advanced
- weather system inputs
- adversary threat network model
- maintenance/repair workflows
- staff shift/fatigue model
- fog-of-war/security uncertainty
- AI-assisted route recommendations
