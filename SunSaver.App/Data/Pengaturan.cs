using System.Text.Json;
using System.Text.Json.Serialization;
using SunSaver.App.Core;

namespace SunSaver.App.Data;

/// <summary>Konfigurasi koneksi ke project Supabase.</summary>
public record PengaturanSupabase(string Url, string AnonKey)
{
    public static readonly PengaturanSupabase Kosong = new("", "");

    [JsonIgnore]
    public bool Lengkap => Uri.TryCreate(Url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
                           && !string.IsNullOrWhiteSpace(AnonKey);

    public PengaturanSupabase Normalisasi() => new(Url.Trim().TrimEnd('/'), AnonKey.Trim());
}

/// <summary>
/// Membaca/menyimpan pengaturan. Prioritas (tinggi → rendah):
/// env SUNSAVER_SUPABASE_URL/SUNSAVER_SUPABASE_KEY → data/pengaturan.json (isian dialog Pengaturan)
/// → appsettings.json di samping .exe (bawaan saat distribusi).
/// </summary>
public static class PenyimpanPengaturan
{
    private static string BerkasPengguna => FolderData.Berkas("pengaturan.json");

    public static PengaturanSupabase Muat()
    {
        var p = DariAppSettings() ?? PengaturanSupabase.Kosong;

        if (File.Exists(BerkasPengguna))
        {
            try
            {
                var u = JsonSerializer.Deserialize<PengaturanSupabase>(File.ReadAllText(BerkasPengguna), Json.Berkas);
                if (u is not null && (!string.IsNullOrWhiteSpace(u.Url) || !string.IsNullOrWhiteSpace(u.AnonKey)))
                    p = u;
            }
            catch (JsonException)
            {
                // abaikan berkas rusak
            }
        }

        var envUrl = Environment.GetEnvironmentVariable("SUNSAVER_SUPABASE_URL");
        var envKey = Environment.GetEnvironmentVariable("SUNSAVER_SUPABASE_KEY");
        if (!string.IsNullOrWhiteSpace(envUrl) && !string.IsNullOrWhiteSpace(envKey))
            p = new PengaturanSupabase(envUrl, envKey);

        return p.Normalisasi();
    }

    public static void Simpan(PengaturanSupabase p)
        => FolderData.TulisAtomik(BerkasPengguna, JsonSerializer.Serialize(p.Normalisasi(), Json.Berkas));

    private static PengaturanSupabase? DariAppSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Supabase", out var s)) return null;
            var url = s.TryGetProperty("Url", out var u) ? u.GetString() ?? "" : "";
            var key = s.TryGetProperty("AnonKey", out var k) ? k.GetString() ?? "" : "";
            return new PengaturanSupabase(url, key);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Connection string PostgreSQL untuk koneksi langsung (Worksheet 8). Prioritas: env
/// SUNSAVER_PG_CONNECTION → appsettings.json ("Postgres:ConnectionString"). Kosong = fitur nonaktif.
/// Contoh: <c>Host=localhost;Port=5432;Database=sunsaver;Username=postgres;Password=...</c>
/// </summary>
public static class PengaturanPostgres
{
    public static string Muat()
    {
        var env = Environment.GetEnvironmentVariable("SUNSAVER_PG_CONNECTION");
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim();

        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return "";
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("Postgres", out var s) && s.TryGetProperty("ConnectionString", out var c)
                ? (c.GetString() ?? "").Trim()
                : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }
}
