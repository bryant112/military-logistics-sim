# Military Logistics Simulator — Codex Starter Pack

This pack is designed to be dropped into a Codex project as the base planning set for a **Windows-based military logistics simulator** with:

- strategic and operational logistics planning
- 3D visualization of convoys, trains, aircraft, warehouses, FOBs, and base-security incidents
- optional tactical feeds for drones and special-operations helmet cameras
- a clean separation between simulation logic and visualization

## Recommended stack

### Primary recommendation
- **Desktop shell / simulation host:** .NET 8
- **3D visualization client:** Unreal Engine 5.x (Windows packaged build)
- **Primary languages:** C# for sim/domain/backend tools, C++/Blueprints for UE integration
- **Data store:** PostgreSQL + PostGIS
- **Message/event transport:** gRPC for command/query; NATS for event streaming
- **Map / geospatial layer:** Cesium for Unreal for terrain + geospatial scene context
- **Local deployment/orchestration:** Docker Compose for non-UE services
- **Auth/roles later if needed:** Keycloak or Microsoft Entra for enterprise mode

## Why this stack

A logistics simulator like this is really **two products fused together**:

1. **A simulation and planning platform**
   - route planning
   - cargo/unit/resource state
   - warehouse and node capacity
   - mission timelines
   - disruptions, ambushes, maintenance, weather, security posture

2. **A 3D operational picture**
   - moving convoy icons and full 3D vehicles
   - camera handoff to drones / helmet views
   - base/FOB visual state
   - tactical incident playback and live view

Trying to force all of that into a single game-engine-only codebase usually makes the planning/UI/reporting layer painful.
Trying to force all of it into a pure enterprise desktop app makes the 3D side weak.

The hybrid approach keeps:
- **simulation deterministic and testable** in .NET
- **3D rich and cinematic** in Unreal
- **data model portable** for later web or headless simulation runs

## What Codex should build first

Build in this order:

1. Core sim engine
2. Domain model + persistence
3. Simple operator UI
4. Unreal visualization bridge
5. Tactical camera feeds
6. Security incidents and replay
7. Multi-scenario sandbox tools

See the other files for the exact breakdown.
