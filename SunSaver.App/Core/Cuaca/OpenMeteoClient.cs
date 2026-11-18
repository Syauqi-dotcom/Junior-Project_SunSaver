using System.Globalization;
using System.Text.Json.Serialization;

namespace SunSaver.App.Core.Cuaca;

/// <summary>
/// Klien REST Open-Meteo (gratis, tanpa API key). Menarik radiasi gelombang pendek
/// (shortwave radiation), durasi sinar matahari, suhu, dan tutupan awan berdasarkan koordinat.
/// </summary>
public class OpenMeteoClient
{
    private const string UrlArsip = "https://archive-api.open-meteo.com/v1/archive";
    private const string UrlPrakiraan = "https://api.open-meteo.com/v1/forecast";

    /// <summary>Data harian historis (ERA5) untuk rentang tanggal.</summary>
    public Task<ArsipHarian> AmbilArsipAsync(double lat, double lon, DateOnly mulai, DateOnly selesai, CancellationToken ct = default)
    {
        var url = $"{UrlArsip}?latitude={F(lat)}&longitude={F(lon)}" +
                  $"&start_date={mulai:yyyy-MM-dd}&end_date={selesai:yyyy-MM-dd}" +
                  "&daily=shortwave_radiation_sum,sunshine_duration,temperature_2m_mean,cloud_cover_mean" +
                  "&timezone=auto";
        return Http.AmbilJsonAsync<ArsipHarian>(url, "Open-Meteo Archive", ct);
    }

    /// <summary>Prakiraan 7 hari (harian + per jam) dan kondisi saat ini (real-time, interval 15 menit).</summary>
    public Task<PrakiraanMentah> AmbilPrakiraanAsync(double lat, double lon, CancellationToken ct = default)
    {
        var url = $"{UrlPrakiraan}?latitude={F(lat)}&longitude={F(lon)}" +
                  "&current=temperature_2m,cloud_cover,shortwave_radiation,is_day" +
                  "&hourly=shortwave_radiation,cloud_cover,temperature_2m" +
                  "&daily=shortwave_radiation_sum,sunshine_duration,temperature_2m_max,temperature_2m_min,cloud_cover_mean" +
                  "&forecast_days=7&timezone=auto";
        return Http.AmbilJsonAsync<PrakiraanMentah>(url, "Open-Meteo Forecast", ct);
    }

    private static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}

// ---------------------------- DTO respons Open-Meteo ----------------------------

public class ArsipHarian
{
    [JsonPropertyName("latitude")] public double Latitude { get; set; }
    [JsonPropertyName("longitude")] public double Longitude { get; set; }
    [JsonPropertyName("elevation")] public double Elevasi { get; set; }
    [JsonPropertyName("timezone")] public string ZonaWaktu { get; set; } = "";
    [JsonPropertyName("daily")] public DataHarianArsip Harian { get; set; } = new();
}

public class DataHarianArsip
{
    [JsonPropertyName("time")] public List<string> Tanggal { get; set; } = [];
    [JsonPropertyName("shortwave_radiation_sum")] public List<double?> RadiasiMj { get; set; } = [];
    [JsonPropertyName("sunshine_duration")] public List<double?> SinarDetik { get; set; } = [];
    [JsonPropertyName("temperature_2m_mean")] public List<double?> SuhuRata { get; set; } = [];
    [JsonPropertyName("cloud_cover_mean")] public List<double?> Awan { get; set; } = [];
}

public class PrakiraanMentah
{
    [JsonPropertyName("timezone")] public string ZonaWaktu { get; set; } = "";
    [JsonPropertyName("current")] public KondisiSaatIni? SaatIni { get; set; }
    [JsonPropertyName("hourly")] public DataPerJam PerJam { get; set; } = new();
    [JsonPropertyName("daily")] public DataHarianPrakiraan Harian { get; set; } = new();
}

public class KondisiSaatIni
{
    [JsonPropertyName("time")] public string Waktu { get; set; } = "";
    [JsonPropertyName("temperature_2m")] public double? Suhu { get; set; }
    [JsonPropertyName("cloud_cover")] public double? Awan { get; set; }
    [JsonPropertyName("shortwave_radiation")] public double? Radiasi { get; set; }
    [JsonPropertyName("is_day")] public int? Siang { get; set; }
}

public class DataPerJam
{
    [JsonPropertyName("time")] public List<string> Waktu { get; set; } = [];
    [JsonPropertyName("shortwave_radiation")] public List<double?> Radiasi { get; set; } = [];
    [JsonPropertyName("cloud_cover")] public List<double?> Awan { get; set; } = [];
    [JsonPropertyName("temperature_2m")] public List<double?> Suhu { get; set; } = [];
}

public class DataHarianPrakiraan
{
    [JsonPropertyName("time")] public List<string> Tanggal { get; set; } = [];
    [JsonPropertyName("shortwave_radiation_sum")] public List<double?> RadiasiMj { get; set; } = [];
    [JsonPropertyName("sunshine_duration")] public List<double?> SinarDetik { get; set; } = [];
    [JsonPropertyName("temperature_2m_max")] public List<double?> SuhuMaks { get; set; } = [];
    [JsonPropertyName("temperature_2m_min")] public List<double?> SuhuMin { get; set; } = [];
    [JsonPropertyName("cloud_cover_mean")] public List<double?> Awan { get; set; } = [];
}
