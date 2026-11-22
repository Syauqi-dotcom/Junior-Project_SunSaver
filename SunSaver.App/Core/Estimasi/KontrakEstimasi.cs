using SunSaver.App.Core.Kalkulasi;

namespace SunSaver.App.Core.Estimasi;

/// <summary>Masukan kalkulator. Hanya lokasi, tagihan, dan tarif yang wajib; sisanya punya nilai default.</summary>
public record EstimasiMasukan
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }

    /// <summary>Rata-rata tagihan listrik per bulan (Rp).</summary>
    public double TagihanBulananRp { get; init; } = 500_000;
    public double TarifPerKwh { get; init; } = 1444.70;
    public string? KodeTarif { get; init; } = "R1-1300";

    /// <summary>Luas atap (m²) hasil gambar di peta atau isian manual. Null = tidak dibatasi.</summary>
    public double? LuasAtapM2 { get; init; }

    /// <summary>Jumlah panel yang dipilih pengguna. Null = rekomendasi otomatis.</summary>
    public int? JumlahPanel { get; init; }

    public double DayaPanelWp { get; init; } = 400;
    public double EfisiensiPersen { get; init; } = 20;
    public double FaktorEmisi { get; init; } = KalkulatorKarbon.FaktorEmisiDefault;
    public AsumsiFinansial Asumsi { get; init; } = AsumsiFinansial.Default;
}

public record RingkasanAtap(
    double? LuasAtapM2,
    double LuasPanelM2,
    int? PanelMaks,
    double LuasDibutuhkanM2);

public record RingkasanSistem(
    double KapasitasKwp,
    int JumlahPanel,
    int PanelRekomendasi,
    double RekomendasiKwp,
    double CakupanPersen,
    double ProduksiTahunanKwh,
    double ProduksiHarianRataKwh,
    double PerformanceRatio,
    bool DibatasiAtap);

public record BulanEstimasi(
    int Bulan,
    double ProduksiKwh,
    double KonsumsiKwh,
    double TerpakaiKwh,
    double TagihanTanpaSuryaRp,
    double TagihanDenganSuryaRp,
    double HematRp,
    double Co2Kg);

public record RingkasanLingkungan(
    double FaktorEmisi,
    double Co2TahunanKg,
    double Co2UmurSistemTon,
    double SetaraPohon,
    Lencana Lencana);

public record HasilEstimasi(
    EstimasiMasukan Masukan,
    RingkasanIklim Iklim,
    double HasilSpesifikKwhPerKwp,
    RingkasanAtap Atap,
    RingkasanSistem Sistem,
    double KonsumsiBulananKwh,
    IReadOnlyList<BulanEstimasi> Bulanan,
    HasilFinansial Finansial,
    RingkasanLingkungan Lingkungan,
    IReadOnlyList<string> Catatan);

// ------------------------------- Prakiraan -------------------------------

public record KondisiSaatIniDto(DateTime Waktu, double Suhu, double TutupanAwan, double RadiasiWm2, bool Siang, double DayaKw);

public record HariPrakiraan(
    DateOnly Tanggal,
    double RadiasiMj,
    double Psh,
    double JamSinarMatahari,
    double SuhuMaks,
    double SuhuMin,
    double TutupanAwan,
    double ProduksiKwh,
    double HematRp,
    double Co2Kg);

public record JamPrakiraan(DateTime Waktu, double RadiasiWm2, double TutupanAwan, double Suhu, double DayaKw);

public record HasilPrakiraan(
    string ZonaWaktu,
    double KapasitasKwp,
    KondisiSaatIniDto? SaatIni,
    IReadOnlyList<HariPrakiraan> Harian,
    IReadOnlyList<JamPrakiraan> HariIni,
    double TotalProduksiKwh,
    double TotalHematRp,
    double TotalCo2Kg,
    double SetaraPohonPerTahun);
