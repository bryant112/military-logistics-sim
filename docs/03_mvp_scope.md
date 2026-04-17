# MVP Scope

## MVP goal
Create a usable first simulator where an operator can:
- place nodes on a map
- define routes between them
- assign cargo to convoy, rail, or air assets
- run the simulation
- watch movement in a 3D scene
- receive alerts for delays, shortages, ambush/security incidents, and damaged assets
- switch to tactical camera view for supported incidents
- review a timeline after the run

## MVP entities
- Depot
- Warehouse
- FOB
- Main Base
- Airfield
- Rail Yard
- Checkpoint
- Convoy
- Train
- Aircraft
- Drone feed
- Helmet camera feed
- Cargo package
- Fuel/ammo/medical/rations stock
- Threat event
- Security team / QRF
- Mission order

## MVP features
### Planning
- create scenario
- define nodes and capacities
- define asset pools
- define routes and schedules
- assign cargo and escorts
- set threat level per region/route

### Simulation
- tick-based movement
- fuel consumption
- capacity handling
- travel delays
- breakdown chance
- threat-triggered incidents
- base stock depletion / replenishment
- simple security response logic

### Visualization
- map + terrain
- moving 3D icons/models
- route overlays
- warehouse/base status indicators
- alert markers
- tactical feed pop-out or full-screen view

### Reporting
- completed deliveries
- failed missions
- stockout moments
- damaged or lost assets
- incident timeline
- actual vs planned arrival

## Explicitly out of scope for MVP
- photorealistic combat simulation
- ballistic simulation
- full RTS command and control
- multiplayer
- classified integration
- real drone ingest
- full GIS editing suite
- detailed personnel biometrics

Keep tactical feeds as **event-driven visualization**, not a full separate shooter/game.
