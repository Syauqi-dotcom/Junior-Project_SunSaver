using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Serialization;
using SunSaver.App.Core.Model;

namespace SunSaver.App.Core.Lokasi;

/// <summary>
/// Geocoding alamat ↔ koordinat lewat OpenStreetMap Nominatim.
/// Mematuhi kebijakan penggunaan: User-Agent jelas, maksimal 1 permintaan/detik, hasil di-cache,
/// dan pencarian hanya saat pengguna menekan Enter/tombol (bukan autocomplete).
/// </summary>
public class NominatimGeocoder
{
    private const string Basis = "https://nominatim.openstreetmap.org/";
    private static readonly SemaphoreSlim Kunci = new(1, 1);
    private static DateTime _terakhir = DateTime.MinValue;

    private readonly ConcurrentDictionary<string, IReadOnlyList<HasilLokasi>> _cacheCari = new();
    private readonly ConcurrentDictionary<string, HasilLokasi?> _cacheBalik = new();

    public async Task<IReadOnlyList<HasilLokasi>> CariAsync(string kueri, CancellationToken ct = default)
    {
        kueri = kueri.Trim();
        if (kueri.Length < 3) return [];

        // Masukan berupa koordinat langsung, mis. "-7.7956, 110.3695".
        if (CobaKoordinat(kueri) is { } titik)
            return [new HasilLokasi($"Koordinat {titik.Lat:0.#####}, {titik.Lon:0.#####}", titik.Lat, titik.Lon, "koordinat")];

        var kunci = kueri.ToLowerInvariant();
        if (_cacheCari.TryGetValue(kunci, out var tersimpan)) return tersimpan;

        var url = Basis + "search?format=jsonv2&limit=6&countrycodes=id&accept-language=id&q=" + Uri.EscapeDataString(kueri);
        var data = await AmbilAsync<List<ItemNominatim>>(url, ct) ?? [];
        IReadOnlyList<HasilLokasi> hasil = data
            .Select(d => new HasilLokasi(d.NamaTampil, Parse(d.Lat), Parse(d.Lon), d.Tipe ?? d.Kategori ?? ""))
            .ToList();
        return _cacheCari[kunci] = hasil;
    }

    public async Task<HasilLokasi?> BalikAsync(double lat, double lon, CancellationToken ct = default)
    {
        var kunci = string.Create(CultureInfo.InvariantCulture, $"{lat:F5}:{lon:F5}");
        if (_cacheBalik.TryGetValue(kunci, out var tersimpan)) return tersimpan;

        var url = Basis + string.Create(CultureInfo.InvariantCulture,
            $"reverse?format=jsonv2&zoom=18&accept-language=id&lat={lat:0.######}&lon={lon:0.######}");
        var d = await AmbilAsync<ItemNominatim>(url, ct);
        var hasil = d is null || string.IsNullOrEmpty(d.NamaTampil)
            ? null
            : new HasilLokasi(d.NamaTampil, Parse(d.Lat), Parse(d.Lon), d.Tipe ?? "");
        return _cacheBalik[kunci] = hasil;
    }

    public static TitikGeo? CobaKoordinat(string teks)
    {
        var bagian = teks.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (bagian.Length != 2) return null;
        if (double.TryParse(bagian[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(bagian[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
            && lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
            return new TitikGeo(lat, lon);
        return null;
    }

    private static async Task<T?> AmbilAsync<T>(string url, CancellationToken ct) where T : class
    {
        await Kunci.WaitAsync(ct);
        try
        {
            var jeda = _terakhir.AddMilliseconds(1100) - DateTime.UtcNow;
            if (jeda > TimeSpan.Zero) await Task.Delay(jeda, ct);
            _terakhir = DateTime.UtcNow;
            return await Http.AmbilJsonAsync<T>(url, "layanan pencarian alamat (Nominatim)", ct);
        }
        finally
        {
            Kunci.Release();
        }
    }

    private static double Parse(string? s)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private sealed class ItemNominatim
    {
        [JsonPropertyName("display_name")] public string NamaTampil { get; set; } = "";
        [JsonPropertyName("lat")] public string? Lat { get; set; }
        [JsonPropertyName("lon")] public string? Lon { get; set; }
        [JsonPropertyName("type")] public string? Tipe { get; set; }
        [JsonPropertyName("category")] public string? Kategori { get; set; }
    }
}
