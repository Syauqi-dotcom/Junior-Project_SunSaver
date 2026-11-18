using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunSaver.App.Core;
using SunSaver.App.Core.Model;
using SunSaver.App.Data.Supabase;

namespace SunSaver.App.Data;

/// <summary>Penyimpanan data "Instalasi Saya" beserta riwayat estimasinya.</summary>
public interface IRepositoriInstalasi
{
    string Nama { get; }
    Task<List<Instalasi>> DaftarAsync(CancellationToken ct = default);
    Task<Instalasi> SimpanAsync(Instalasi instalasi, CancellationToken ct = default);
    Task HapusAsync(long id, CancellationToken ct = default);
    Task<List<RiwayatEstimasi>> RiwayatAsync(long instalasiId, CancellationToken ct = default);
    Task<RiwayatEstimasi> TambahRiwayatAsync(RiwayatEstimasi riwayat, CancellationToken ct = default);
}

// =====================================================================================
// Mode cloud: tabel public.instalasi & public.riwayat_estimasi di Supabase (PostgreSQL)
// =====================================================================================

public sealed class RepositoriSupabase(SupabaseKlien klien) : IRepositoriInstalasi
{
    private const string TabelInstalasi = "instalasi";
    private const string TabelRiwayat = "riwayat_estimasi";

    public string Nama => "Supabase";

    public async Task<List<Instalasi>> DaftarAsync(CancellationToken ct = default)
    {
        var baris = await klien.PilihAsync<BarisInstalasi>(TabelInstalasi, "select=*&order=diperbarui_pada.desc", ct);
        return baris.Select(b => b.KeModel()).ToList();
    }

    public async Task<Instalasi> SimpanAsync(Instalasi i, CancellationToken ct = default)
    {
        var tulis = TulisInstalasi.Dari(i);
        if (i.Id is { } id)
        {
            var hasil = await klien.UbahAsync<BarisInstalasi>(TabelInstalasi, $"id=eq.{id}", tulis, ct)
                        ?? throw new SupabaseException("Instalasi tidak ditemukan atau bukan milik Anda.", System.Net.HttpStatusCode.NotFound);
            return hasil.KeModel();
        }
        return (await klien.SisipAsync<BarisInstalasi>(TabelInstalasi, tulis, ct)).KeModel();
    }

    public async Task HapusAsync(long id, CancellationToken ct = default)
    {
        if (await klien.HapusAsync(TabelInstalasi, $"id=eq.{id}", ct) == 0)
            throw new SupabaseException("Instalasi tidak ditemukan atau bukan milik Anda.", System.Net.HttpStatusCode.NotFound);
    }

    public async Task<List<RiwayatEstimasi>> RiwayatAsync(long instalasiId, CancellationToken ct = default)
    {
        var baris = await klien.PilihAsync<BarisRiwayat>(TabelRiwayat,
            $"select=*&instalasi_id=eq.{instalasiId}&order=dibuat_pada.desc&limit=60", ct);
        return baris.Select(b => b.KeModel()).ToList();
    }

    public async Task<RiwayatEstimasi> TambahRiwayatAsync(RiwayatEstimasi r, CancellationToken ct = default)
        => (await klien.SisipAsync<BarisRiwayat>(TabelRiwayat, TulisRiwayat.Dari(r), ct)).KeModel();

    // ---------------- DTO baris tabel (nama kolom eksplisit = skema SQL) ----------------

    private sealed class BarisInstalasi
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("nama")] public string Nama { get; set; } = "";
        [JsonPropertyName("alamat")] public string? Alamat { get; set; }
        [JsonPropertyName("latitude")] public double Latitude { get; set; }
        [JsonPropertyName("longitude")] public double Longitude { get; set; }
        [JsonPropertyName("kapasitas_kwp")] public double KapasitasKwp { get; set; }
        [JsonPropertyName("jumlah_panel")] public int JumlahPanel { get; set; }
        [JsonPropertyName("daya_panel_wp")] public double DayaPanelWp { get; set; }
        [JsonPropertyName("efisiensi_persen")] public double EfisiensiPersen { get; set; }
        [JsonPropertyName("luas_atap_m2")] public double? LuasAtapM2 { get; set; }
        [JsonPropertyName("poligon_atap")] public List<TitikGeo>? PoligonAtap { get; set; }
        [JsonPropertyName("kode_tarif")] public string? KodeTarif { get; set; }
        [JsonPropertyName("tarif_per_kwh")] public double TarifPerKwh { get; set; }
        [JsonPropertyName("tagihan_bulanan_rp")] public double TagihanBulananRp { get; set; }
        [JsonPropertyName("dibuat_pada")] public DateTimeOffset DibuatPada { get; set; }
        [JsonPropertyName("diperbarui_pada")] public DateTimeOffset DiperbaruiPada { get; set; }

        public Instalasi KeModel() => new()
        {
            Id = Id, Nama = Nama, Alamat = Alamat ?? "", Latitude = Latitude, Longitude = Longitude,
            KapasitasKwp = KapasitasKwp, JumlahPanel = JumlahPanel, DayaPanelWp = DayaPanelWp,
            EfisiensiPersen = EfisiensiPersen, LuasAtapM2 = LuasAtapM2, PoligonAtap = PoligonAtap,
            KodeTarif = KodeTarif, TarifPerKwh = TarifPerKwh, TagihanBulananRp = TagihanBulananRp,
            DibuatPada = DibuatPada.LocalDateTime, DiperbaruiPada = DiperbaruiPada.LocalDateTime,
        };
    }

    /// <summary>Kolom yang boleh ditulis klien. id, pengguna_id (= auth.uid()), dan timestamp diisi database.</summary>
    private sealed record TulisInstalasi(
        [property: JsonPropertyName("nama")] string Nama,
        [property: JsonPropertyName("alamat")] string Alamat,
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude,
        [property: JsonPropertyName("kapasitas_kwp")] double KapasitasKwp,
        [property: JsonPropertyName("jumlah_panel")] int JumlahPanel,
        [property: JsonPropertyName("daya_panel_wp")] double DayaPanelWp,
        [property: JsonPropertyName("efisiensi_persen")] double EfisiensiPersen,
        [property: JsonPropertyName("luas_atap_m2")] double? LuasAtapM2,
        [property: JsonPropertyName("poligon_atap")] List<TitikGeo>? PoligonAtap,
        [property: JsonPropertyName("kode_tarif")] string? KodeTarif,
        [property: JsonPropertyName("tarif_per_kwh")] double TarifPerKwh,
        [property: JsonPropertyName("tagihan_bulanan_rp")] double TagihanBulananRp)
    {
        public static TulisInstalasi Dari(Instalasi i) => new(
            i.Nama.Trim(), i.Alamat.Trim(), i.Latitude, i.Longitude, i.KapasitasKwp, i.JumlahPanel,
            i.DayaPanelWp, i.EfisiensiPersen, i.LuasAtapM2, i.PoligonAtap is { Count: >= 3 } p ? p : null,
            string.IsNullOrWhiteSpace(i.KodeTarif) ? null : i.KodeTarif, i.TarifPerKwh, i.TagihanBulananRp);
    }

    private sealed class BarisRiwayat
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("instalasi_id")] public long InstalasiId { get; set; }
        [JsonPropertyName("dibuat_pada")] public DateTimeOffset DibuatPada { get; set; }
        [JsonPropertyName("psh_rata_harian")] public double PshRataHarian { get; set; }
        [JsonPropertyName("produksi_tahunan_kwh")] public double ProduksiTahunanKwh { get; set; }
        [JsonPropertyName("hemat_tahunan_rp")] public double HematTahunanRp { get; set; }
        [JsonPropertyName("co2_tahunan_kg")] public double Co2TahunanKg { get; set; }
        [JsonPropertyName("setara_pohon")] public double SetaraPohon { get; set; }
        [JsonPropertyName("produksi_7_hari_kwh")] public double Produksi7HariKwh { get; set; }
        [JsonPropertyName("hemat_7_hari_rp")] public double Hemat7HariRp { get; set; }

        public RiwayatEstimasi KeModel() => new()
        {
            Id = Id, InstalasiId = InstalasiId, DibuatPada = DibuatPada.LocalDateTime, PshRataHarian = PshRataHarian,
            ProduksiTahunanKwh = ProduksiTahunanKwh, HematTahunanRp = HematTahunanRp, Co2TahunanKg = Co2TahunanKg,
            SetaraPohon = SetaraPohon, Produksi7HariKwh = Produksi7HariKwh, Hemat7HariRp = Hemat7HariRp,
        };
    }

    private sealed record TulisRiwayat(
        [property: JsonPropertyName("instalasi_id")] long InstalasiId,
        [property: JsonPropertyName("psh_rata_harian")] double PshRataHarian,
        [property: JsonPropertyName("produksi_tahunan_kwh")] double ProduksiTahunanKwh,
        [property: JsonPropertyName("hemat_tahunan_rp")] double HematTahunanRp,
        [property: JsonPropertyName("co2_tahunan_kg")] double Co2TahunanKg,
        [property: JsonPropertyName("setara_pohon")] double SetaraPohon,
        [property: JsonPropertyName("produksi_7_hari_kwh")] double Produksi7HariKwh,
        [property: JsonPropertyName("hemat_7_hari_rp")] double Hemat7HariRp)
    {
        public static TulisRiwayat Dari(RiwayatEstimasi r) => new(r.InstalasiId, Math.Round(r.PshRataHarian, 3),
            Math.Round(r.ProduksiTahunanKwh, 1), Math.Round(r.HematTahunanRp), Math.Round(r.Co2TahunanKg, 1),
            Math.Round(r.SetaraPohon, 2), Math.Round(r.Produksi7HariKwh, 2), Math.Round(r.Hemat7HariRp));
    }
}

// =====================================================================================
// Mode lokal: berkas JSON di folder data (dipakai saat belum masuk / Supabase belum diatur)
// =====================================================================================

public sealed class RepositoriLokal : IRepositoriInstalasi
{
    private readonly string _berkas = FolderData.Berkas("instalasi-lokal.json");
    private readonly Lock _kunci = new();
    private Isi? _isi;

    public string Nama => "Lokal";

    public Task<List<Instalasi>> DaftarAsync(CancellationToken ct = default)
    {
        lock (_kunci)
            return Task.FromResult(Muat().Instalasi.OrderByDescending(i => i.DiperbaruiPada).Select(i => i.Salin()).ToList());
    }

    public Task<Instalasi> SimpanAsync(Instalasi i, CancellationToken ct = default)
    {
        lock (_kunci)
        {
            var isi = Muat();
            var salinan = i.Salin();
            salinan.PoligonAtap = i.PoligonAtap?.ToList();
            salinan.DiperbaruiPada = DateTime.Now;
            if (salinan.Id is { } id)
            {
                var idx = isi.Instalasi.FindIndex(x => x.Id == id);
                if (idx < 0) throw new InvalidOperationException("Instalasi tidak ditemukan.");
                salinan.DibuatPada = isi.Instalasi[idx].DibuatPada;
                isi.Instalasi[idx] = salinan;
            }
            else
            {
                salinan.Id = isi.IdBerikut++;
                salinan.DibuatPada = DateTime.Now;
                isi.Instalasi.Add(salinan);
            }
            Tulis(isi);
            return Task.FromResult(salinan.Salin());
        }
    }

    public Task HapusAsync(long id, CancellationToken ct = default)
    {
        lock (_kunci)
        {
            var isi = Muat();
            isi.Instalasi.RemoveAll(x => x.Id == id);
            isi.Riwayat.RemoveAll(r => r.InstalasiId == id);
            Tulis(isi);
        }
        return Task.CompletedTask;
    }

    public Task<List<RiwayatEstimasi>> RiwayatAsync(long instalasiId, CancellationToken ct = default)
    {
        lock (_kunci)
            return Task.FromResult(Muat().Riwayat.Where(r => r.InstalasiId == instalasiId)
                .OrderByDescending(r => r.DibuatPada).Take(60).ToList());
    }

    public Task<RiwayatEstimasi> TambahRiwayatAsync(RiwayatEstimasi r, CancellationToken ct = default)
    {
        lock (_kunci)
        {
            var isi = Muat();
            r.Id = isi.IdBerikut++;
            r.DibuatPada = DateTime.Now;
            isi.Riwayat.Add(r);
            Tulis(isi);
            return Task.FromResult(r);
        }
    }

    private Isi Muat()
    {
        if (_isi is not null) return _isi;
        if (File.Exists(_berkas))
        {
            try
            {
                _isi = JsonSerializer.Deserialize<Isi>(File.ReadAllText(_berkas), Json.Berkas);
            }
            catch (JsonException)
            {
                // Simpan cadangan berkas rusak agar data tidak hilang diam-diam.
                File.Copy(_berkas, _berkas + ".rusak-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture), true);
            }
        }
        return _isi ??= new Isi();
    }

    private void Tulis(Isi isi) => FolderData.TulisAtomik(_berkas, JsonSerializer.Serialize(isi, Json.Berkas));

    private sealed class Isi
    {
        public long IdBerikut { get; set; } = 1;
        public List<Instalasi> Instalasi { get; set; } = [];
        public List<RiwayatEstimasi> Riwayat { get; set; } = [];
    }
}
