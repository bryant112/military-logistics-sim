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

    public ObservableCollection<MovementStateDto> Movements { get; } = new();
    public ObservableCollection<IncidentDto> Incidents { get; } = new();
    public ObservableCollection<TimelineEventDto> TimelineEvents { get; } = new();
    public ObservableCollection<EnrichmentSnapshot> EnrichmentSnapshots { get; } = new();

    public IAsyncRelayCommand CreateSessionCommand { get; }
    public IAsyncRelayCommand StartCommand { get; }
    public IAsyncRelayCommand PauseCommand { get; }
    public IAsyncRelayCommand ResetCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }

    public MainWindowViewModel()
    {
        CreateSessionCommand = new AsyncRelayCommand(CreateSessionAsync);
        StartCommand = new AsyncRelayCommand(StartAsync);
        PauseCommand = new AsyncRelayCommand(PauseAsync);
        ResetCommand = new AsyncRelayCommand(ResetAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
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

            if (state is null || timeline is null || enrichment is null)
            {
                StatusMessage = "Refresh returned incomplete payloads.";
                return;
            }

            ReplaceCollection(Movements, state.Movements);
            ReplaceCollection(Incidents, state.Incidents);
            ReplaceCollection(TimelineEvents, timeline.Events.OrderByDescending(e => e.Tick).Take(20));
            ReplaceCollection(EnrichmentSnapshots, enrichment.Routes);

            SimulationStatus = $"{state.Status} @ tick {state.Tick} ({state.SimulatedTime:O})";
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
