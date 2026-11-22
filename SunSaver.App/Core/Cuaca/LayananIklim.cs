using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using SunSaver.App.Core.Kalkulasi;

namespace SunSaver.App.Core.Cuaca;

/// <summary>
/// Penyedia data iklim: Open-Meteo + cache dua lapis.
/// Ringkasan historis tahunan disimpan di disk (data/cache) karena data masa lalu tidak berubah;
/// prakiraan di-cache di memori selama 20 menit.
/// </summary>
public class LayananIklim(OpenMeteoClient openMeteo)
{
    private readonly ConcurrentDictionary<string, RingkasanIklim> _iklim = new();
    private readonly ConcurrentDictionary<string, (DateTime Waktu, PrakiraanMentah Data)> _prakiraan = new();

    /// <summary>Koordinat dibulatkan 2 desimal (~1 km), resolusi yang memadai untuk data cuaca grid.</summary>
    public static double Bulatkan(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Ringkasan iklim tahun kalender penuh terakhir (mis. 2025 bila sekarang 2026).</summary>
    public async Task<RingkasanIklim> RingkasanTahunanAsync(double lat, double lon, CancellationToken ct = default)
    {
        var latB = Bulatkan(lat);
        var lonB = Bulatkan(lon);
        var tahun = DateTime.Now.Year - 1;
        var kunci = string.Create(CultureInfo.InvariantCulture, $"{latB:0.00}_{lonB:0.00}_{tahun}");

        if (_iklim.TryGetValue(kunci, out var diMemori)) return diMemori;

        var berkas = Path.Combine(FolderData.Cache, $"iklim_{kunci}.json");
        if (File.Exists(berkas))
        {
            try
            {
                var dariDisk = JsonSerializer.Deserialize<RingkasanIklim>(await File.ReadAllTextAsync(berkas, ct), Json.Opsi);
                if (dariDisk is { Bulanan.Count: 12 })
                    return _iklim[kunci] = dariDisk;
            }
            catch (JsonException)
            {
                // cache rusak → ambil ulang
            }
        }

        var arsip = await openMeteo.AmbilArsipAsync(latB, lonB, new DateOnly(tahun, 1, 1), new DateOnly(tahun, 12, 31), ct);
        var ringkasan = Agregasi(arsip, tahun);
        try
        {
            FolderData.TulisAtomik(berkas, JsonSerializer.Serialize(ringkasan, Json.Opsi));
        }
        catch (IOException)
        {
            // cache hanya optimasi
        }
        return _iklim[kunci] = ringkasan;
    }

    /// <summary>Prakiraan 7 hari + kondisi saat ini.</summary>
    public async Task<PrakiraanMentah> PrakiraanAsync(double lat, double lon, bool paksaSegar = false, CancellationToken ct = default)
    {
        var kunci = string.Create(CultureInfo.InvariantCulture, $"{Bulatkan(lat):0.00}_{Bulatkan(lon):0.00}");
        if (!paksaSegar && _prakiraan.TryGetValue(kunci, out var c) && DateTime.UtcNow - c.Waktu < TimeSpan.FromMinutes(20))
            return c.Data;

        var data = await openMeteo.AmbilPrakiraanAsync(Bulatkan(lat), Bulatkan(lon), ct);
        _prakiraan[kunci] = (DateTime.UtcNow, data);
        return data;
    }

    /// <summary>Agregasi data harian setahun menjadi 12 ringkasan bulanan.</summary>
    public static RingkasanIklim Agregasi(ArsipHarian arsip, int tahun)
    {
        var h = arsip.Harian;
        var bulanan = new List<IklimBulanan>(12);

        for (var bulan = 1; bulan <= 12; bulan++)
        {
            double psh = 0, sinar = 0, suhu = 0, awan = 0;
            int nPsh = 0, nSinar = 0, nSuhu = 0, nAwan = 0;

            for (var i = 0; i < h.Tanggal.Count; i++)
            {
                if (!DateOnly.TryParseExact(h.Tanggal[i], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tgl)
                    || tgl.Month != bulan) continue;

                if (Ambil(h.RadiasiMj, i) is { } r) { psh += KalkulatorEnergi.PshDariMj(r); nPsh++; }
                if (Ambil(h.SinarDetik, i) is { } s) { sinar += s / 3600; nSinar++; }
                if (Ambil(h.SuhuRata, i) is { } t) { suhu += t; nSuhu++; }
                if (Ambil(h.Awan, i) is { } a) { awan += a; nAwan++; }
            }

            var hari = DateTime.DaysInMonth(tahun, bulan);
            bulanan.Add(new IklimBulanan(
                bulan,
                hari,
                PshRata: nPsh == 0 ? 0 : Math.Round(psh / nPsh, 3),
                // diskalakan ke jumlah hari penuh bila ada hari yang datanya kosong
                JamSinarMatahari: nSinar == 0 ? 0 : Math.Round(sinar / nSinar * hari, 1),
                SuhuRata: nSuhu == 0 ? 27 : Math.Round(suhu / nSuhu, 2),
                TutupanAwanRata: nAwan == 0 ? 0 : Math.Round(awan / nAwan, 1)));
        }

        if (bulanan.All(b => b.PshRata == 0))
            throw new LayananLuarException("Data radiasi matahari untuk lokasi ini tidak tersedia.");

        return new RingkasanIklim(arsip.Latitude, arsip.Longitude, arsip.Elevasi, arsip.ZonaWaktu, tahun, bulanan);
    }

    private static double? Ambil(List<double?> data, int i) => i < data.Count ? data[i] : null;
}
