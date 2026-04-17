# Domain Model

## Core aggregates

### Scenario
- ScenarioId
- Name
- StartTime
- TickRate
- Duration
- TerrainReference
- RulesetId

### Node
Represents supply endpoints and operational locations.

Fields:
- NodeId
- NodeType: Depot | Warehouse | FOB | Base | Airfield | RailYard | Checkpoint
- Name
- GeoPosition
- Capacity
- SecurityPosture
- StorageByCommodity
- OperatingStatus

### Route
Fields:
- RouteId
- Mode: Ground | Rail | Air
- StartNodeId
- EndNodeId
- Waypoints
- RiskProfile
- EstimatedTravelTime
- SurfaceCondition

### Asset
Base type for movable platforms.

Common fields:
- AssetId
- AssetType
- OwningUnit
- Status
- Position
- FuelState
- Readiness
- PayloadCapacity
- SpeedProfile
- SignatureLevel

Subtypes:
- Truck
- ArmoredEscort
- TrainLocomotive
- RailCar
- CargoAircraft
- Helicopter
- Drone
- SecurityVehicle

### Shipment
Fields:
- ShipmentId
- CommodityType
- Quantity
- Weight
- Volume
- Priority
- OriginNodeId
- DestinationNodeId
- RequiredByTime
- AssignedMovementId

### Movement
Represents a mission or transport run.

Fields:
- MovementId
- Mode
- AssignedAssets
- AssignedShipments
- PlannedRouteId
- CurrentState
- CurrentETA
- DepartureTime
- ArrivalTime
- EscortPlan
- MediaAvailability

### Incident
Fields:
- IncidentId
- IncidentType
- Severity
- RelatedMovementId
- RelatedNodeId
- DetectionTime
- ResolutionTime
- Outcome
- CameraRefs

Examples:
- Ambush
- IED / route denial
- Gate breach
- Warehouse fire
- Drone sighting
- Fuel contamination
- Rail obstruction
- Airstrip closure

### MediaFeed
Fields:
- FeedId
- FeedType: Drone | HelmetCam | TowerCam | VehicleCam
- SourceEntityId
- AvailabilityState
- StreamUriOrMockRef
- StartTime
- EndTime
- ClassificationTag

## Key design rule
Store camera feeds as metadata and stream references attached to incidents/assets.
Do not make tactical video the primary system.
