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

## Road Surface Severity TODO
- add dynamic road surface severity for ground logistics as a documentation-first backlog item before implementation
- evaluate ground routes segment by segment instead of corridor average
- introduce `Route Severity Index (RSI)` as the operator-facing route condition score
- introduce `Surface Attrition Factor (SAF)` as the internal wear, damage, and maintenance driver
- classify imported or inferred segments into:
  - paved interstate/highway
  - secondary paved
  - improved dirt/pave hybrid
  - gravel
  - dirt
  - degraded mud/soft ground
- if a segment is not a highway and not connected to a highway, default it to `improved dirt/pave hybrid` unless better source data exists
- model both distance-based and time-based penalties:
  - rough surfaces increase wear per mile
  - delay, mud, stop-start movement, and poor traction add time-based stress
- use separate coefficients by vehicle family:
  - civilian cars/SUVs/pickups
  - civilian bus/freight
  - tactical wheeled military vehicles
  - tracked military vehicles
- make route severity affect:
  - speed
  - fuel burn
  - maintenance accumulation
  - breakdown frequency
  - crew fatigue
  - morale
  - cargo damage risk
  - concealment/signature tradeoff
- allow tracked vehicles to outperform wheeled vehicles off-road while paying higher paved-route fuel and maintenance penalties
- scale effects realistically by load weight, with light liaison movement receiving smaller penalties than tankers, freight trucks, and heavy tactical loads
- use hostile local areas as paved-route chokepoint amplifiers in the first pass:
  - paved and highway routes are easier to block, inspect, and officially restrict
  - rough back-road routes are slower and harsher but less governed
- include dust as a concealment breaker on dry rough routes so dirt-road movement is not automatically stealthier
- treat weather as a future dynamic input that can transform segment condition:
  - dirt to mud/soft ground
  - gravel to degraded gravel
  - paved to reduced-traction paved

## Cargo Damage TODO
- add cargo-damage and load-protection modeling tied to route severity
- represent cargo quality control with two separate ratings:
  - `UMO planning quality`
  - `Loading Team Chief execution quality`
- let cargo damage risk be influenced by:
  - surface severity
  - weather
  - vehicle family
  - load weight
  - UMO planning quality
  - Loading Team Chief quality
  - cargo securement systems
- model cargo-securement mitigation before advanced vehicle kits:
  - tiedowns
  - blocking/bracing
  - pallet restraint
  - cargo isolation / shock reduction

## Future Questions
- should morale become a first-class simulation stat or remain a derived modifier feeding fatigue, reporting, and performance
- how many surface subclasses can the model support before it becomes too granular to tune
- how should bridges, steep grades, and washout-prone segments affect RSI and denial risk
- should different commodity classes have different shock and damage tolerances
- how should convoy spacing interact with dust and visibility on dirt roads
- do we want road-improvement actions later, such as engineers or local contractors reducing RSI on key segments
- how should railheads, airfields, and transfer nodes inherit nearby road severity for last-mile distribution
- which cargo-securement systems should be explicit equipment versus folded into quality ratings
- add a later-version reminder to build a simulated weather system that can drive route condition changes dynamically
