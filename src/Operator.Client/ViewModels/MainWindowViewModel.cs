using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.Input;
using Sim.Contracts;

namespace Operator.Client.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://localhost:5080")
    };

    private Guid? _sessionId;
    private string _statusMessage = "API not connected yet.";
    private string _sessionDisplay = "No session";
    private string _simulationStatus = "Paused";
    private string _logisticsOverview = "Realism profile not loaded yet.";
    private string _sitrepOverview = "SITREP not loaded yet.";
    private double _aoiCenterLat = 36.1627;
    private double _aoiCenterLon = -85.5016;
    private double _aoiRadiusMiles = 30;
    private double _infrastructurePriority = 0.7;
    private double _civilFriction = 0.35;
    private double _governmentFriendliness = 0.65;
    private double _threatLevel = 0.4;
    private double _weatherStress = 0.2;
    private double _propagandaFactor = 0.35;
    private string _planningStatus = "AO planner idle.";
    private string _transportationSummary = "No imported transport data loaded yet.";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string SessionDisplay
    {
        get => _sessionDisplay;
        private set => SetProperty(ref _sessionDisplay, value);
    }

    public string SimulationStatus
    {
        get => _simulationStatus;
        private set => SetProperty(ref _simulationStatus, value);
    }

    public string LogisticsOverview
    {
        get => _logisticsOverview;
        private set => SetProperty(ref _logisticsOverview, value);
    }

    public string SitrepOverview
    {
        get => _sitrepOverview;
        private set => SetProperty(ref _sitrepOverview, value);
    }

    public double AoiCenterLat
    {
        get => _aoiCenterLat;
        set => SetProperty(ref _aoiCenterLat, value);
    }

    public double AoiCenterLon
    {
        get => _aoiCenterLon;
        set => SetProperty(ref _aoiCenterLon, value);
    }

    public double AoiRadiusMiles
    {
        get => _aoiRadiusMiles;
        set => SetProperty(ref _aoiRadiusMiles, value);
    }

    public double InfrastructurePriority
    {
        get => _infrastructurePriority;
        set => SetProperty(ref _infrastructurePriority, value);
    }

    public double CivilFriction
    {
        get => _civilFriction;
        set => SetProperty(ref _civilFriction, value);
    }

    public double GovernmentFriendliness
    {
        get => _governmentFriendliness;
        set => SetProperty(ref _governmentFriendliness, value);
    }

    public double ThreatLevel
    {
        get => _threatLevel;
        set => SetProperty(ref _threatLevel, value);
    }

    public double WeatherStress
    {
        get => _weatherStress;
        set => SetProperty(ref _weatherStress, value);
    }

    public double PropagandaFactor
    {
        get => _propagandaFactor;
        set => SetProperty(ref _propagandaFactor, value);
    }

    public string PlanningStatus
    {
        get => _planningStatus;
        private set => SetProperty(ref _planningStatus, value);
    }

    public string TransportationSummary
    {
        get => _transportationSummary;
        private set => SetProperty(ref _transportationSummary, value);
    }

    public ObservableCollection<MovementStateDto> Movements { get; } = new();
    public ObservableCollection<IncidentDto> Incidents { get; } = new();
    public ObservableCollection<AssetStateDto> Assets { get; } = new();
    public ObservableCollection<TimelineEventDto> TimelineEvents { get; } = new();
    public ObservableCollection<EnrichmentSnapshot> EnrichmentSnapshots { get; } = new();
    public ObservableCollection<ObjectiveDto> Objectives { get; } = new();
    public ObservableCollection<SupportZoneDto> SupportZones { get; } = new();
    public ObservableCollection<MovementPinDto> SitrepPins { get; } = new();
    public ObservableCollection<string> RegionalCounties { get; } = new();
    public ObservableCollection<CountyAllegianceDto> CountyAllegiances { get; } = new();
    public ObservableCollection<string> HighwayCorridors { get; } = new();
    public ObservableCollection<string> TransitServices { get; } = new();
    public ObservableCollection<ImportedFeatureDto> ImportedFeatures { get; } = new();
    public ObservableCollection<TransportRealismProfileDto> TransportProfiles { get; } = new();

    public IAsyncRelayCommand PlanAoCommand { get; }
    public IAsyncRelayCommand CreateSessionCommand { get; }
    public IAsyncRelayCommand StartCommand { get; }
    public IAsyncRelayCommand PauseCommand { get; }
    public IAsyncRelayCommand ResetCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }

    public MainWindowViewModel()
    {
        PlanAoCommand = new AsyncRelayCommand(PlanAoAsync);
        CreateSessionCommand = new AsyncRelayCommand(CreateSessionAsync);
        StartCommand = new AsyncRelayCommand(StartAsync);
        PauseCommand = new AsyncRelayCommand(PauseAsync);
        ResetCommand = new AsyncRelayCommand(ResetAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    private async Task PlanAoAsync()
    {
        try
        {
            var request = new AoiPlanningRequest
            {
                CenterLat = AoiCenterLat,
                CenterLon = AoiCenterLon,
                RadiusMiles = AoiRadiusMiles,
                Seed = 42,
                Criteria = new SituationCriteriaDto
                {
                    InfrastructurePriority = InfrastructurePriority,
                    CivilFriction = CivilFriction,
                    GovernmentFriendliness = GovernmentFriendliness,
                    ThreatLevel = ThreatLevel,
                    WeatherStress = WeatherStress,
                    PropagandaFactor = PropagandaFactor
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/planning/ao", request);
            var plan = await response.Content.ReadFromJsonAsync<AoiPlanningResponse>();

            if (!response.IsSuccessStatusCode || plan is null)
            {
                PlanningStatus = plan?.ValidationMessage ?? $"AO planning failed with HTTP {(int)response.StatusCode}.";
                return;
            }

            ReplaceCollection(Objectives, plan.Objectives);
            ReplaceCollection(SupportZones, plan.SupportZones);
            ReplaceCollection(RegionalCounties, plan.Transportation.Counties);
            ReplaceCollection(CountyAllegiances, plan.Transportation.CountyAllegiances);
            ReplaceCollection(HighwayCorridors, plan.Transportation.HighwayCorridors);
            ReplaceCollection(TransitServices, plan.Transportation.TransitServices);
            ReplaceCollection(ImportedFeatures, plan.Transportation.FeatureHighlights);
            ReplaceCollection(TransportProfiles, plan.Transportation.TransportProfiles);
            var hostileZones = plan.SupportZones.Count(zone => zone.GovernmentStance is "Restricted" or "Hostile");
            PlanningStatus = $"{plan.ValidationMessage} Roads {plan.Transportation.MajorRoadSegments}, fuel sites {plan.Transportation.FuelSites}, Army Corps campgrounds {plan.Transportation.ArmyCorpsCampgrounds}, hostile/restricted zones {hostileZones}.";
            TransportationSummary = $"{plan.Transportation.DataSource} | {plan.Transportation.DatasetCoverage} | speed baseline {plan.Transportation.AverageSpeedLimitKph:F1} kph | traffic {plan.Transportation.TrafficVehiclesPerHour:F0} vph | profiles {plan.Transportation.TransportProfiles.Count} | propaganda {PropagandaFactor:P0}";
            StatusMessage = "AO planning complete.";
        }
        catch (Exception ex)
        {
            PlanningStatus = $"AO planning failed: {ex.Message}";
        }
    }

    private async Task CreateSessionAsync()
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/sessions", new CreateSessionRequest { Seed = 42 });
            response.EnsureSuccessStatusCode();

            var created = await response.Content.ReadFromJsonAsync<CreateSessionResponse>();
            if (created is null)
            {
                StatusMessage = "Failed to parse create-session response.";
                return;
            }

            _sessionId = created.SessionId;
            SessionDisplay = created.SessionId.ToString();
            StatusMessage = $"Session created with seed {created.Seed}.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Create session failed: {ex.Message}";
        }
    }

    private async Task StartAsync()
    {
        if (!EnsureSession())
        {
            return;
        }

        try
        {
            var response = await _httpClient.PostAsync($"/sessions/{_sessionId}/start", null);
            response.EnsureSuccessStatusCode();
            StatusMessage = "Simulation started.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Start failed: {ex.Message}";
        }
    }

    private async Task PauseAsync()
    {
        if (!EnsureSession())
        {
            return;
        }

        try
        {
            var response = await _httpClient.PostAsync($"/sessions/{_sessionId}/pause", null);
            response.EnsureSuccessStatusCode();
            StatusMessage = "Simulation paused.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Pause failed: {ex.Message}";
        }
    }

    private async Task ResetAsync()
    {
        if (!EnsureSession())
        {
            return;
        }

        try
        {
            var response = await _httpClient.PostAsync($"/sessions/{_sessionId}/reset", null);
            response.EnsureSuccessStatusCode();
            StatusMessage = "Simulation reset.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reset failed: {ex.Message}";
        }
    }

    private async Task RefreshAsync()
    {
        if (!EnsureSession())
        {
            return;
        }

        try
        {
            var state = await _httpClient.GetFromJsonAsync<WorldStateResponse>($"/sessions/{_sessionId}/state");
            var timeline = await _httpClient.GetFromJsonAsync<TimelineResponse>($"/sessions/{_sessionId}/timeline");
            var enrichment = await _httpClient.GetFromJsonAsync<EnrichmentResponse>($"/sessions/{_sessionId}/enrichment");
            var sitrep = await _httpClient.GetFromJsonAsync<SitrepResponse>($"/sessions/{_sessionId}/sitrep");

            if (state is null || timeline is null || enrichment is null || sitrep is null)
            {
                StatusMessage = "Refresh returned incomplete payloads.";
                return;
            }

            ReplaceCollection(Movements, state.Movements);
            ReplaceCollection(Incidents, state.Incidents);
            ReplaceCollection(Assets, state.Assets);
            ReplaceCollection(TimelineEvents, timeline.Events.OrderByDescending(e => e.Tick).Take(20));
            ReplaceCollection(EnrichmentSnapshots, enrichment.Routes);
            ReplaceCollection(SitrepPins, sitrep.MovementPins);

            SimulationStatus = $"{state.Status} @ tick {state.Tick} ({state.SimulatedTime:O})";
            LogisticsOverview = $"Reporting {state.Overview.ReportingQuality:P0} | Rhythm {state.Overview.SustainmentRhythmAdherence:P0} | Loads {state.Overview.ConfiguredLoadQuality:P0} | Avg fatigue {state.Overview.AverageCrewFatigueIndex:P0} | Avg morale {state.Overview.AverageMorale:P0} | Avg RSI {state.Overview.AverageRouteSeverityIndex:P0} | Avg cargo risk {state.Overview.AverageCargoDamageRisk:P0} | Avg maint backlog {state.Overview.AverageMaintenanceBacklog:F1}";
            SitrepOverview = $"{sitrep.OverallStatus} | Delayed {sitrep.DelayedMovements} | Incidents {sitrep.ActiveIncidents} | Critical assets {sitrep.CriticalAssets}";
            StatusMessage = "Refresh complete.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
    }

    private bool EnsureSession()
    {
        if (_sessionId.HasValue)
        {
            return true;
        }

        StatusMessage = "Create a session first.";
        return false;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> destination, IEnumerable<T> source)
    {
        destination.Clear();
        foreach (var item in source)
        {
            destination.Add(item);
        }
    }
}
