namespace Sim.Domain;

public enum NodeType
{
    Depot,
    Warehouse,
    FOB,
    Base,
    Airfield,
    RailYard,
    Checkpoint
}

public enum TransportMode
{
    Ground,
    Rail,
    Air
}

public enum AssetType
{
    Truck,
    ArmoredEscort,
    TrainLocomotive,
    RailCar,
    CargoAircraft,
    Helicopter,
    Drone,
    SecurityVehicle
}

public enum CommodityType
{
    Fuel,
    Ammo,
    Medical,
    Rations,
    General
}

public enum IncidentType
{
    Ambush,
    RailObstruction,
    AirstripClosure,
    GateBreach,
    WarehouseFire,
    DroneSighting,
    RouteDenial,
    Other
}

public enum Severity
{
    Low,
    Medium,
    High
}

public enum FeedType
{
    Drone,
    HelmetCam,
    TowerCam,
    VehicleCam
}
