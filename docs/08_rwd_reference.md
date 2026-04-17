# RWD Reference

`RWD` = `Real World Data`

This is the shorthand we should use later when talking about imported real-world inputs such as roads, counties, transport methods, political/allegiance data, population context, and highlighted regional infrastructure.

## Primary RWD Files

- `docs/data/upper-cumberland-realworld.json`
  - Main source-of-truth snapshot for the current Upper Cumberland regional import.
  - Holds county list, county political/allegiance profiles, transit services, highway corridors, transport realism profiles, and feature highlights.

- `src/Sim.Infrastructure/MockAoiPlanningService.cs`
  - Loads the Upper Cumberland RWD snapshot.
  - Maps imported regional data into AO planning results.
  - Builds support zones, objectives, county allegiance output, transport summaries, and imported feature results.

- `src/Sim.Contracts/PlanningContracts.cs`
  - Defines the API/data contracts used to carry RWD into the app.
  - Includes `TransportationAreaSummaryDto`, `CountyAllegianceDto`, `ImportedFeatureDto`, and `TransportRealismProfileDto`.

## Related Runtime / Simulation Files

- `src/Sim.Infrastructure/MockEnrichmentProvider.cs`
  - Not the raw RWD file, but it transforms route context into runtime enrichment used by the sim.
  - Handles ground-route segment generation, route severity, surface attrition, concealment, and weather-sensitive route conditions.

- `src/Sim.Application/SimulationSessionManager.cs`
  - Holds session-level enrichment snapshots after they are built.
  - Serves those snapshots back through API endpoints used by the dashboard.

- `src/Sim.Contracts/EnrichmentContracts.cs`
  - Defines the sim-facing enrichment structures such as `EnrichmentSnapshot`, `GroundCorridorMetrics`, and route segment metadata.

## API / App Surface

- `src/Sim.Api/Program.cs`
  - Wires up the AO planning service and exposes `/planning/ao`.
  - This is the main backend entry point that returns imported RWD into the UI.

- `src/Operator.Client/ViewModels/MainWindowViewModel.cs`
  - Requests AO planning data from the API.
  - Stores county allegiances, transport profiles, transit services, feature highlights, and planning summaries for display.

- `src/Operator.Client/Views/MainWindow.axaml`
  - Displays the imported RWD in the operator UI.
  - Includes county political climate, counties, corridors, transit services, feature highlights, and transport realism cards.

## Test Coverage

- `tests/Sim.Tests/SimulationTests.cs`
  - Verifies the Upper Cumberland snapshot is loaded.
  - Verifies county allegiance output, transport realism profiles, and imported feature data are surfaced correctly.

## Current Mental Model

- `Static RWD`
  - Roads
  - County/support posture
  - Transport profiles
  - Population/structures
  - Regional infrastructure
  - Imported at setup time and used to build a planning/runtime snapshot

- `Dynamic live data`
  - Weather only for now
  - Meant to refresh during runtime separately from the static RWD snapshot

## Good Follow-Up Placeholders

When we add more RWD later, extend this document first so we always know:

- where the raw imported data lives
- where it gets transformed
- where it becomes sim enrichment
- where it shows up in the UI
