using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sim.Application;
using Sim.Contracts;

namespace Sim.Infrastructure;

public sealed class NoaaWeatherService : IRealWorldWeatherService
{
    private static readonly Regex WindNumberRegex = new(@"\d+", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public NoaaWeatherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("MilitaryLogisticsSim/0.1"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(https://github.com/bryant112/military-logistics-sim)"));
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/geo+json"));
    }

    public async Task<RealWorldWeatherSnapshotDto> GetCurrentWeatherAsync(double lat, double lon, CancellationToken cancellationToken = default)
    {
        var pointUrl = $"https://api.weather.gov/points/{lat:F4},{lon:F4}";
        var point = await GetJsonAsync<NoaaPointResponse>(pointUrl, cancellationToken);
        var forecastUrl = point.Properties.ForecastHourly ?? point.Properties.Forecast;
        if (string.IsNullOrWhiteSpace(forecastUrl))
        {
            throw new InvalidOperationException("NWS point lookup did not return a forecast URL.");
        }

        var forecast = await GetJsonAsync<NoaaForecastResponse>(forecastUrl, cancellationToken);
        var period = forecast.Properties.Periods.FirstOrDefault()
            ?? throw new InvalidOperationException("NWS forecast did not return any forecast periods.");

        var windMph = ParseWindSpeed(period.WindSpeed);
        int? precipChance = period.ProbabilityOfPrecipitation?.Value is null
            ? null
            : (int)Math.Round(period.ProbabilityOfPrecipitation.Value.Value);
        var severity = ComputeSeverity(period.ShortForecast, period.DetailedForecast, period.Temperature, windMph, precipChance);

        return new RealWorldWeatherSnapshotDto
        {
            QueryLat = lat,
            QueryLon = lon,
            Summary = period.ShortForecast ?? "Forecast unavailable",
            DetailedForecast = period.DetailedForecast ?? string.Empty,
            TemperatureF = period.Temperature,
            TemperatureTrend = period.IsDaytime ? "Day" : "Night",
            WindSpeedMph = windMph,
            WindDirection = period.WindDirection ?? string.Empty,
            PrecipitationChancePercent = precipChance,
            Severity = Math.Round(severity, 2),
            SeverityBand = ToWeatherBand(severity),
            ObservedAt = period.StartTime == default ? DateTimeOffset.UtcNow : period.StartTime,
            Source = "NOAA NWS API",
            GridId = point.Properties.GridId ?? string.Empty,
            ForecastOfficeUrl = point.Properties.ForecastOffice ?? string.Empty
        };
    }

    private async Task<T> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<T>(content, JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException($"Failed to deserialize NOAA response for {url}.");
    }

    private static int? ParseWindSpeed(string? rawWind)
    {
        if (string.IsNullOrWhiteSpace(rawWind))
        {
            return null;
        }

        var values = WindNumberRegex.Matches(rawWind)
            .Select(match => int.TryParse(match.Value, out var mph) ? mph : 0)
            .Where(mph => mph > 0)
            .ToList();

        return values.Count == 0 ? null : values.Max();
    }

    private static double ComputeSeverity(string? shortForecast, string? detailedForecast, int temperatureF, int? windMph, int? precipChancePercent)
    {
        var merged = $"{shortForecast} {detailedForecast}".ToUpperInvariant();
        var severity = 0.05;

        severity += Math.Clamp((precipChancePercent ?? 0) / 100.0, 0, 1) * 0.28;
        severity += Math.Clamp((windMph ?? 0) / 45.0, 0, 1) * 0.32;

        if (temperatureF >= 95 || temperatureF <= 20)
        {
            severity += 0.12;
        }

        if (temperatureF >= 105 || temperatureF <= 10)
        {
            severity += 0.08;
        }

        if (merged.Contains("TORNADO") || merged.Contains("BLIZZARD") || merged.Contains("ICE STORM"))
        {
            severity += 0.42;
        }
        else if (merged.Contains("SEVERE THUNDERSTORM") || merged.Contains("THUNDERSTORM") || merged.Contains("FREEZING RAIN"))
        {
            severity += 0.26;
        }
        else if (merged.Contains("SNOW") || merged.Contains("SLEET") || merged.Contains("HEAVY RAIN") || merged.Contains("GUSTY"))
        {
            severity += 0.18;
        }
        else if (merged.Contains("FOG") || merged.Contains("SHOWERS") || merged.Contains("SMOKE"))
        {
            severity += 0.1;
        }

        return Math.Clamp(severity, 0.02, 1.0);
    }

    private static string ToWeatherBand(double severity)
    {
        return severity switch
        {
            <= 0.25 => "Nominal",
            <= 0.5 => "Watch",
            <= 0.75 => "Rough",
            _ => "Severe"
        };
    }

    private sealed class NoaaPointResponse
    {
        public NoaaPointProperties Properties { get; set; } = new();
    }

    private sealed class NoaaPointProperties
    {
        public string? Forecast { get; set; }
        public string? ForecastHourly { get; set; }
        public string? GridId { get; set; }
        public string? ForecastOffice { get; set; }
    }

    private sealed class NoaaForecastResponse
    {
        public NoaaForecastProperties Properties { get; set; } = new();
    }

    private sealed class NoaaForecastProperties
    {
        public List<NoaaForecastPeriod> Periods { get; set; } = new();
    }

    private sealed class NoaaForecastPeriod
    {
        public DateTimeOffset StartTime { get; set; }
        public bool IsDaytime { get; set; }
        public int Temperature { get; set; }
        public string? WindSpeed { get; set; }
        public string? WindDirection { get; set; }
        public string? ShortForecast { get; set; }
        public string? DetailedForecast { get; set; }
        public ProbabilityValue? ProbabilityOfPrecipitation { get; set; }
    }

    private sealed class ProbabilityValue
    {
        public double? Value { get; set; }
    }
}
