using System.Net.Http.Headers;

namespace SunSaver.App.Core;

/// <summary>Galat saat memanggil layanan pihak ketiga (Open-Meteo, Nominatim, Supabase).</summary>
public class LayananLuarException(string pesan, Exception? inner = null) : Exception(pesan, inner);

/// <summary>Satu HttpClient bersama untuk seluruh aplikasi (menghindari socket exhaustion).</summary>
public static class Http
{
    public const string AgenPengguna =
        "SunSaver-Desktop/1.0 (+https://github.com/Syauqi-dotcom/Junior-Project_SunSaver)";

    public static readonly HttpClient Klien = Buat();

    private static HttpClient Buat()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 8,
        };
        var c = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd(AgenPengguna);
        return c;
    }

    /// <summary>GET JSON dengan pemetaan galat jaringan/status ke <see cref="LayananLuarException"/>.</summary>
    public static async Task<T> AmbilJsonAsync<T>(string url, string namaLayanan, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        try
        {
            using var resp = await Klien.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new LayananLuarException($"{namaLayanan} mengembalikan status {(int)resp.StatusCode}.");
            var hasil = await System.Net.Http.Json.HttpContentJsonExtensions
                .ReadFromJsonAsync<T>(resp.Content, Json.Opsi, ct).ConfigureAwait(false);
            return hasil ?? throw new LayananLuarException($"Respons {namaLayanan} kosong.");
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException
                                      or TaskCanceledException { CancellationToken.IsCancellationRequested: false })
        {
            throw new LayananLuarException($"Gagal menghubungi {namaLayanan}. Periksa koneksi internet.", e);
        }
    }
}

public static class Json
{
    /// <summary>Opsi umum: properti case-insensitive (untuk respons pihak ketiga).</summary>
    public static readonly System.Text.Json.JsonSerializerOptions Opsi = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>snake_case untuk kolom PostgreSQL/Supabase (KapasitasKwp ↔ kapasitas_kwp).</summary>
    public static readonly System.Text.Json.JsonSerializerOptions SnakeCase = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    /// <summary>Format berkas lokal: terbaca manusia.</summary>
    public static readonly System.Text.Json.JsonSerializerOptions Berkas = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}
