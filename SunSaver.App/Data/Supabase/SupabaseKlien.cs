using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunSaver.App.Core;

namespace SunSaver.App.Data.Supabase;

/// <summary>Sesi login Supabase Auth (GoTrue). Access token berupa JWT berumur pendek (default 1 jam).</summary>
public record Sesi(string AccessToken, string RefreshToken, DateTime KedaluwarsaUtc, Guid PenggunaId, string Email, string Nama);

/// <summary>Galat dari Supabase dengan pesan yang sudah diterjemahkan untuk pengguna.</summary>
public class SupabaseException(string pesan, HttpStatusCode status, string? kode = null) : Exception(pesan)
{
    public HttpStatusCode Status { get; } = status;
    public string? Kode { get; } = kode;
}

/// <summary>Hasil pendaftaran: langsung masuk, atau perlu konfirmasi email terlebih dahulu.</summary>
public record HasilDaftar(Sesi? Sesi, bool PerluKonfirmasiEmail);

/// <summary>
/// Klien REST minimal untuk Supabase:
/// <list type="bullet">
/// <item>Auth  → <c>/auth/v1/*</c> (signup, token password, refresh token, logout)</item>
/// <item>Data  → <c>/rest/v1/{tabel}</c> (PostgREST; akses dibatasi Row Level Security)</item>
/// </list>
/// Kunci yang dipakai adalah publishable/anon key, yang memang dirancang untuk aplikasi klien.
/// </summary>
public sealed class SupabaseKlien(PengaturanSupabase pengaturan)
{
    private readonly SemaphoreSlim _kunciSegar = new(1, 1);

    public PengaturanSupabase Pengaturan { get; } = pengaturan.Normalisasi();
    public Sesi? Sesi { get; private set; }

    /// <summary>Dipicu saat sesi berganti (login, refresh — refresh token Supabase dirotasi, logout).</summary>
    public event Action<Sesi?>? SesiBerubah;

    // ------------------------------------ Auth ------------------------------------

    public async Task<HasilDaftar> DaftarAsync(string email, string kataSandi, string nama, CancellationToken ct = default)
    {
        var isi = new { email, password = kataSandi, data = new { nama } };
        using var resp = await KirimAsync(HttpMethod.Post, "/auth/v1/signup", isi, pakaiToken: false, ct: ct);
        var json = await BacaJsonAsync(resp, ct);

        if (json.TryGetProperty("access_token", out _))
        {
            var sesi = KeSesi(json);
            AturSesi(sesi);
            return new HasilDaftar(sesi, false);
        }
        return new HasilDaftar(null, true);
    }

    public async Task<Sesi> MasukAsync(string email, string kataSandi, CancellationToken ct = default)
    {
        using var resp = await KirimAsync(HttpMethod.Post, "/auth/v1/token?grant_type=password",
            new { email, password = kataSandi }, pakaiToken: false, ct: ct);
        var sesi = KeSesi(await BacaJsonAsync(resp, ct));
        AturSesi(sesi);
        return sesi;
    }

    public async Task<Sesi> SegarkanAsync(string refreshToken, CancellationToken ct = default)
    {
        using var resp = await KirimAsync(HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token",
            new { refresh_token = refreshToken }, pakaiToken: false, ct: ct);
        var sesi = KeSesi(await BacaJsonAsync(resp, ct));
        AturSesi(sesi);
        return sesi;
    }

    public async Task KeluarAsync(CancellationToken ct = default)
    {
        if (Sesi is not null)
        {
            try
            {
                using var _ = await KirimAsync(HttpMethod.Post, "/auth/v1/logout", null, pakaiToken: true, ct: ct);
            }
            catch (Exception e) when (e is SupabaseException or LayananLuarException)
            {
                // Tetap keluar secara lokal walau server tak terjangkau / token sudah kedaluwarsa.
            }
        }
        AturSesi(null);
    }

    /// <summary>Lupakan sesi tanpa memanggil server (mis. saat refresh token ditolak).</summary>
    public void LupakanSesi() => AturSesi(null);

    // ------------------------------------ Data (PostgREST) ------------------------------------

    public async Task<List<T>> PilihAsync<T>(string tabel, string kueri, CancellationToken ct = default)
    {
        using var resp = await KirimDataAsync(HttpMethod.Get, $"/rest/v1/{tabel}?{kueri}", null, null, ct);
        return await resp.Content.ReadFromJsonAsync<List<T>>(Json.Opsi, ct) ?? [];
    }

    public async Task<T> SisipAsync<T>(string tabel, object baris, CancellationToken ct = default)
    {
        using var resp = await KirimDataAsync(HttpMethod.Post, $"/rest/v1/{tabel}", baris, "return=representation", ct);
        var hasil = await resp.Content.ReadFromJsonAsync<List<T>>(Json.Opsi, ct);
        return hasil is { Count: > 0 } ? hasil[0] : throw new SupabaseException("Server tidak mengembalikan data tersimpan.", resp.StatusCode);
    }

    public async Task<T?> UbahAsync<T>(string tabel, string filter, object perubahan, CancellationToken ct = default)
    {
        using var resp = await KirimDataAsync(HttpMethod.Patch, $"/rest/v1/{tabel}?{filter}", perubahan, "return=representation", ct);
        var hasil = await resp.Content.ReadFromJsonAsync<List<T>>(Json.Opsi, ct);
        return hasil is { Count: > 0 } ? hasil[0] : default;
    }

    public async Task<int> HapusAsync(string tabel, string filter, CancellationToken ct = default)
    {
        using var resp = await KirimDataAsync(HttpMethod.Delete, $"/rest/v1/{tabel}?{filter}", null, "return=representation", ct);
        var hasil = await resp.Content.ReadFromJsonAsync<List<JsonElement>>(Json.Opsi, ct);
        return hasil?.Count ?? 0;
    }

    /// <summary>Uji koneksi + kunci dengan membaca tabel referensi publik.</summary>
    public async Task<int> UjiKoneksiAsync(CancellationToken ct = default)
    {
        using var resp = await KirimAsync(HttpMethod.Get, "/rest/v1/tarif_listrik?select=kode", null, pakaiToken: false, ct: ct);
        if (!resp.IsSuccessStatusCode) throw await GalatDariAsync(resp, ct);
        var data = await resp.Content.ReadFromJsonAsync<List<JsonElement>>(Json.Opsi, ct);
        return data?.Count ?? 0;
    }

    // ------------------------------------ Internal ------------------------------------

    private async Task<HttpResponseMessage> KirimDataAsync(HttpMethod metode, string path, object? isi, string? prefer, CancellationToken ct)
    {
        if (Sesi is null) throw new SupabaseException("Silakan masuk ke akun SunSaver terlebih dahulu.", HttpStatusCode.Unauthorized);
        await PastikanTokenSegarAsync(ct);

        var resp = await KirimAsync(metode, path, isi, pakaiToken: true, prefer, ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized && Sesi is not null)
        {
            // Token mungkin dicabut/kedaluwarsa lebih cepat: segarkan sekali lalu ulangi.
            resp.Dispose();
            await SegarkanAsync(Sesi.RefreshToken, ct);
            resp = await KirimAsync(metode, path, isi, pakaiToken: true, prefer, ct);
        }
        if (!resp.IsSuccessStatusCode)
        {
            var galat = await GalatDariAsync(resp, ct);
            resp.Dispose();
            throw galat;
        }
        return resp;
    }

    private async Task PastikanTokenSegarAsync(CancellationToken ct)
    {
        if (Sesi is null || Sesi.KedaluwarsaUtc - DateTime.UtcNow > TimeSpan.FromSeconds(90)) return;
        await _kunciSegar.WaitAsync(ct);
        try
        {
            if (Sesi is not null && Sesi.KedaluwarsaUtc - DateTime.UtcNow <= TimeSpan.FromSeconds(90))
                await SegarkanAsync(Sesi.RefreshToken, ct);
        }
        finally
        {
            _kunciSegar.Release();
        }
    }

    private async Task<HttpResponseMessage> KirimAsync(HttpMethod metode, string path, object? isi, bool pakaiToken,
        string? prefer = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(metode, Pengaturan.Url + path);
        req.Headers.TryAddWithoutValidation("apikey", Pengaturan.AnonKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (pakaiToken && Sesi is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Sesi.AccessToken);
        else if (Pengaturan.AnonKey.StartsWith("eyJ", StringComparison.Ordinal))
            // Anon key lama (JWT) juga dikirim sebagai Bearer; publishable key baru (sb_publishable_…) cukup di header apikey.
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Pengaturan.AnonKey);

        if (prefer is not null) req.Headers.TryAddWithoutValidation("Prefer", prefer);
        if (isi is not null) req.Content = JsonContent.Create(isi, isi.GetType(), options: Json.Opsi);

        try
        {
            return await Http.Klien.SendAsync(req, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException { CancellationToken.IsCancellationRequested: false })
        {
            throw new LayananLuarException("Tidak dapat terhubung ke Supabase. Periksa koneksi internet dan URL project.", e);
        }
    }

    private static async Task<JsonElement> BacaJsonAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (!resp.IsSuccessStatusCode) throw await GalatDariAsync(resp, ct);
        return await resp.Content.ReadFromJsonAsync<JsonElement>(Json.Opsi, ct);
    }

    private void AturSesi(Sesi? sesi)
    {
        Sesi = sesi;
        SesiBerubah?.Invoke(sesi);
    }

    private static Sesi KeSesi(JsonElement j)
    {
        var akses = j.GetProperty("access_token").GetString() ?? "";
        var segar = j.GetProperty("refresh_token").GetString() ?? "";
        var detik = j.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var d) ? d : 3600;
        var user = j.GetProperty("user");
        var id = Guid.TryParse(user.GetProperty("id").GetString(), out var g) ? g : Guid.Empty;
        var email = user.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
        var nama = user.TryGetProperty("user_metadata", out var meta) && meta.ValueKind == JsonValueKind.Object
                   && meta.TryGetProperty("nama", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()!
            : email.Split('@')[0];
        return new Sesi(akses, segar, DateTime.UtcNow.AddSeconds(detik), id, email, nama);
    }

    private static async Task<SupabaseException> GalatDariAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        string? kode = null, pesan = null;
        try
        {
            var j = await resp.Content.ReadFromJsonAsync<JsonElement>(Json.Opsi, ct);
            if (j.ValueKind == JsonValueKind.Object)
            {
                kode = Teks(j, "error_code") ?? Teks(j, "code") ?? Teks(j, "error");
                pesan = Teks(j, "msg") ?? Teks(j, "message") ?? Teks(j, "error_description");
            }
        }
        catch (JsonException)
        {
            // badan respons bukan JSON
        }

        var ramah = kode switch
        {
            "invalid_credentials" or "invalid_grant" when pesan?.Contains("credentials", StringComparison.OrdinalIgnoreCase) != false
                => "Email atau kata sandi salah.",
            "email_not_confirmed" => "Email belum dikonfirmasi. Buka tautan konfirmasi di kotak masuk email Anda.",
            "user_already_exists" or "email_exists" => "Email sudah terdaftar. Silakan masuk.",
            "weak_password" => "Kata sandi terlalu lemah. Gunakan minimal 8 karakter campuran huruf dan angka.",
            "over_email_send_rate_limit" or "over_request_rate_limit" => "Terlalu banyak percobaan. Tunggu beberapa menit lalu coba lagi.",
            "signup_disabled" => "Pendaftaran akun baru sedang dinonaktifkan di project Supabase.",
            "refresh_token_not_found" or "refresh_token_already_used" or "session_not_found"
                => "Sesi sudah berakhir. Silakan masuk kembali.",
            "42501" => "Akses ditolak oleh kebijakan keamanan database (RLS).",
            "42P01" or "PGRST205" => "Tabel belum ada di Supabase. Jalankan skrip migrasi di folder supabase/migrations.",
            "PGRST301" or "PGRST303" => "Sesi sudah berakhir. Silakan masuk kembali.",
            _ => null,
        };

        if (ramah is null && resp.StatusCode == HttpStatusCode.Unauthorized && pesan?.Contains("API key", StringComparison.OrdinalIgnoreCase) == true)
            ramah = "API key Supabase tidak valid. Periksa menu Pengaturan.";

        return new SupabaseException(ramah ?? pesan ?? $"Supabase mengembalikan status {(int)resp.StatusCode}.", resp.StatusCode, kode);
    }

    private static string? Teks(JsonElement j, string nama)
        => j.TryGetProperty(nama, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        } : null;
}
