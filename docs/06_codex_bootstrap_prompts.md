# Codex Bootstrap Prompts

## Prompt 1 — initialize repository
Create a monorepo for a Windows-based military logistics simulator with these folders:

- docs
- schemas
- src/Sim.Domain
- src/Sim.Engine
- src/Sim.Application
- src/Sim.Infrastructure
- src/Sim.Api
- src/Operator.Client
- src/Renderer.Bridge
- tests

Use .NET 8.
Use clean architecture boundaries.
Add solution files, project references, test projects, Docker Compose, README, and build instructions.

## Prompt 2 — domain model
Implement the initial domain model for:
- Scenario
- Node
- Route
- Asset and derived asset types
- Shipment
- Movement
- Incident
- MediaFeed

Use strong typing for IDs and enums where practical.
Keep the domain model deterministic and engine-agnostic.

## Prompt 3 — simulation engine
Build a tick-based simulation engine that:
- advances time
- moves assets along routes
- consumes fuel
- updates node inventory
- generates incidents based on risk profile
- tracks ETA drift
- emits domain events

Make the engine testable and deterministic from a seed.

## Prompt 4 — API
Create an ASP.NET Core API that supports:
- scenario CRUD
- start/pause/stop/reset simulation
- fetch current world state
- stream event updates
- fetch replay timeline

Use PostgreSQL via EF Core.

## Prompt 5 — operator client
Create a Windows desktop operator client in Avalonia UI.
Views needed:
- scenario list
- scenario editor
- route planner
- asset allocation dashboard
- live incident panel
- timeline and playback controls

Bind the client to the API through gRPC or REST for now.

## Prompt 6 — renderer bridge
Create a Renderer.Bridge service that subscribes to sim events and exposes:
- full scene snapshot messages
- delta update messages
- camera-switch messages
- incident marker messages

Define contracts so an Unreal client can consume them.

## Prompt 7 — Unreal integration plan
Do not generate the entire Unreal project at once.
Instead:
1. define the scene data contracts
2. define the Unreal-side receiver architecture
3. define actor types for nodes, routes, and moving assets
4. define a camera manager that can switch between operator, convoy, drone, and helmet-cam views
5. generate implementation in manageable slices

## Prompt 8 — scenario data format
Create JSON schema files and sample data for:
- nodes
- routes
- assets
- shipments
- incidents
- scenarios

Include one starter scenario involving:
- 1 depot
- 1 warehouse
- 1 FOB
- 1 main base
- 1 convoy route
- 1 rail route
- 1 air route
- 2 possible security incidents

## Prompt 9 — testing
Create tests for:
- route movement calculations
- fuel depletion behavior
- inventory transfer
- incident generation
- replay consistency
