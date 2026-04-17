# Base Architecture

## Top-level components

### 1) Operator Client
A desktop app for planners and controllers.

Responsibilities:
- create scenarios
- define supply nodes, depots, warehouses, FOBs, bases
- allocate cargo, vehicles, aircraft, rail stock
- view alerts, incidents, ETA drift, stock levels
- issue commands to simulation

Recommended tech:
- Avalonia UI
- MVVM
- gRPC client to backend services

### 2) Simulation Engine
The authoritative source of truth.

Responsibilities:
- time-step simulation
- route progression
- fuel/ammo/medical/rations accounting
- vehicle readiness and maintenance state
- security threat resolution
- weather and infrastructure effects
- mission success/failure criteria

Recommended tech:
- .NET 8
- modular class libraries
- deterministic tick-based loop
- plugin-style rulesets

### 3) Persistence Layer
Stores scenarios and state.

Responsibilities:
- scenario definitions
- unit inventories
- route graphs
- movement histories
- incident logs
- replay snapshots
- camera metadata

Recommended tech:
- PostgreSQL + PostGIS
- Redis for fast live state cache

### 4) Renderer Bridge
Translates sim state into 3D scene updates.

Responsibilities:
- publish position/heading/speed updates
- spawn/despawn units
- update base status, alert posture, warehouse occupancy
- push camera targets and event markers

Recommended tech:
- gRPC streaming + NATS topics

### 5) Unreal Visualization Client
The 3D picture.

Responsibilities:
- terrain/world visualization
- convoy/train/air movements
- base/FOB visualization
- tactical incidents
- drone/helmet camera feeds when available
- time controls and playback

### 6) Replay / After Action Review
Responsibilities:
- event timeline
- scrub through incidents
- switch cameras
- compare planned vs actual
- export summary reports

## Suggested repo shape

- `/docs`
- `/schemas`
- `/src/Sim.Domain`
- `/src/Sim.Engine`
- `/src/Sim.Application`
- `/src/Sim.Infrastructure`
- `/src/Sim.Api`
- `/src/Operator.Client`
- `/src/Renderer.Bridge`
- `/unreal/MilLogViz`
- `/tests`

## Simulation model
Use an **authoritative backend** model:
- Unreal is not the source of truth
- Unreal renders state received from simulation services
- any tactical camera or live video metadata is attached to entities/events in backend state

This matters because:
- save/load is cleaner
- replay is easier
- analytics are easier
- headless runs become possible
