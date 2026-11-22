namespace SunSaver.App.Core.Model;

/// <summary>Titik koordinat geografis (derajat desimal, WGS84).</summary>
public readonly record struct TitikGeo(double Lat, double Lon);

/// <summary>Hasil pencarian alamat (geocoding).</summary>
public record HasilLokasi(string Nama, double Lat, double Lon, string Jenis);

/// <summary>Golongan tarif listrik PLN rumah tangga.</summary>
public record TarifListrik(string Kode, string Nama, int DayaVa, double TarifPerKwh, bool Subsidi)
{
    public override string ToString() => Nama;
}

/// <summary>
/// Instalasi panel surya milik pengguna (halaman "Instalasi Saya").
/// Disimpan ke tabel <c>public.instalasi</c> di Supabase, atau ke berkas lokal saat mode lokal.
/// </summary>
public class Instalasi
{
    public long? Id { get; set; }
    public string Nama { get; set; } = "Rumah Saya";
    public string Alamat { get; set; } = "";
    public double Latitude { get; set; } = -7.7956;
    public double Longitude { get; set; } = 110.3695;

    public double KapasitasKwp { get; set; } = 3.2;
    public int JumlahPanel { get; set; } = 8;
    public double DayaPanelWp { get; set; } = 400;
    public double EfisiensiPersen { get; set; } = 20;
    public double? LuasAtapM2 { get; set; }
    public List<TitikGeo>? PoligonAtap { get; set; }

    public string? KodeTarif { get; set; } = "R1-1300";
    public double TarifPerKwh { get; set; } = 1444.70;
    public double TagihanBulananRp { get; set; } = 500_000;

    public DateTime DibuatPada { get; set; }
    public DateTime DiperbaruiPada { get; set; }

    public Instalasi Salin() => (Instalasi)MemberwiseClone();

    public override string ToString() => Nama;
}

/// <summary>Snapshot hasil perhitungan sebuah instalasi pada satu waktu (tabel <c>public.riwayat_estimasi</c>).</summary>
public class RiwayatEstimasi
{
    public long? Id { get; set; }
    public long InstalasiId { get; set; }
    public DateTime DibuatPada { get; set; }
    public double PshRataHarian { get; set; }
    public double ProduksiTahunanKwh { get; set; }
    public double HematTahunanRp { get; set; }
    public double Co2TahunanKg { get; set; }
    public double SetaraPohon { get; set; }
    public double Produksi7HariKwh { get; set; }
    public double Hemat7HariRp { get; set; }
}

/// <summary>Kota-kota besar untuk halaman Jelajah Data.</summary>
public record Kota(string Nama, string Provinsi, double Lat, double Lon);

/// <summary>Data referensi bawaan aplikasi (juga di-seed ke tabel <c>tarif_listrik</c> di Supabase).</summary>
public static class DataReferensi
{
    /// <summary>
    /// Tarif tenaga listrik PLN rumah tangga (Rp/kWh). Nilai non-subsidi sama dengan versi Modul 4;
    /// periksa kembali di situs resmi PLN bila ada penyesuaian tarif triwulanan.
    /// </summary>
    public static readonly IReadOnlyList<TarifListrik> Tarif =
    [
        new("R1-450S", "R-1 / 450 VA (subsidi)", 450, 415.00, true),
        new("R1-900S", "R-1 / 900 VA (subsidi)", 900, 605.00, true),
        new("R1-900", "R-1 / 900 VA", 900, 1352.00, false),
        new("R1-1300", "R-1 / 1.300 VA", 1300, 1444.70, false),
        new("R1-2200", "R-1 / 2.200 VA", 2200, 1444.70, false),
        new("R2", "R-2 / 3.500–5.500 VA", 5500, 1699.53, false),
        new("R3", "R-3 / ≥6.600 VA", 6600, 1699.53, false),
    ];

    public static TarifListrik? CariTarif(string? kode) => Tarif.FirstOrDefault(t => t.Kode == kode);

    public static readonly IReadOnlyList<Kota> Kota =
    [
        new("Jakarta", "DKI Jakarta", -6.2088, 106.8456),
        new("Bandung", "Jawa Barat", -6.9175, 107.6191),
        new("Semarang", "Jawa Tengah", -6.9667, 110.4167),
        new("Yogyakarta", "DI Yogyakarta", -7.7956, 110.3695),
        new("Surabaya", "Jawa Timur", -7.2575, 112.7521),
        new("Denpasar", "Bali", -8.6705, 115.2126),
        new("Kupang", "Nusa Tenggara Timur", -10.1772, 123.6070),
        new("Medan", "Sumatera Utara", 3.5952, 98.6722),
        new("Palembang", "Sumatera Selatan", -2.9761, 104.7754),
        new("Balikpapan", "Kalimantan Timur", -1.2379, 116.8529),
        new("Makassar", "Sulawesi Selatan", -5.1477, 119.4327),
        new("Jayapura", "Papua", -2.5337, 140.7181),
    ];
}
