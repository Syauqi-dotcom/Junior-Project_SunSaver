namespace SunSaver.App.Data;

/// <summary>Satu baris rincian harian yang ditampilkan di dashboard.</summary>
public record BarisHarian(
    string Hari,
    DateOnly Tanggal,
    double RadiasiWm2,
    double SuhuC,
    double ProduksiKwh,
    double HematRp);

/// <summary>Data instalasi yang diisi pengguna di halaman "Instalasi Saya".</summary>
public class Instalasi
{
    public string Nama { get; set; } = "Rumah Kaliurang";
    public string Alamat { get; set; } = "Jl. Kaliurang Km 5, Sleman";
    public double Latitude { get; set; } = -7.7956;
    public double Longitude { get; set; } = 110.3695;
    public double KapasitasKwp { get; set; } = 3.5;
    public double EfisiensiPersen { get; set; } = 18;
    public int JumlahPanel { get; set; } = 8;
    public string GolonganTarif { get; set; } = "R-1 / 1.300 VA";
    public double TarifPerKwh { get; set; } = 1444.70;
}

/// <summary>
/// Data contoh untuk tampilan UI. Nanti diganti hasil OpenMeteoSolarClient + KalkulatorEnergi
/// dari SunSaver.Core — bentuk datanya sengaja dibuat mirip supaya penggantiannya mudah.
/// </summary>
public static class DataContoh
{
    public const double PerformanceRatio = 0.75;
    public const double FaktorEmisiJamali = 0.794;   // kg CO2e / kWh
    public const double SerapanPohonKgPerTahun = 21.77;
    public const double PshRataYogyakarta = 4.65;    // peak sun hours rata-rata, jam/hari

    // Produksi untuk sistem acuan 3,5 kWp; diskalakan ke kapasitas instalasi aktif.
    private const double KapasitasAcuan = 3.5;

    private static readonly (string Hari, int Tgl, double Radiasi, double Suhu, double Kwh)[] Mentah =
    [
        ("Sen", 1, 248, 33.1, 6.4),
        ("Sel", 2, 231, 32.4, 5.9),
        ("Rab", 3, 262, 33.8, 6.8),
        ("Kam", 4, 204, 31.2, 5.2),
        ("Jum", 5, 238, 32.9, 6.1),
        ("Sab", 6, 251, 33.5, 6.5),
        ("Min", 7, 229, 32.2, 5.9),
    ];

    public static IReadOnlyList<BarisHarian> RincianMingguan(Instalasi inst)
    {
        var skala = inst.KapasitasKwp / KapasitasAcuan;
        return Mentah.Select(m =>
        {
            var kwh = m.Kwh * skala;
            return new BarisHarian(m.Hari, new DateOnly(2026, 9, m.Tgl), m.Radiasi, m.Suhu,
                kwh, kwh * inst.TarifPerKwh);
        }).ToList();
    }

    /// <summary>E = kWp × PSH × PR. Dipakai untuk pratinjau di form instalasi.</summary>
    public static double EstimasiHarianKwh(double kapasitasKwp)
        => kapasitasKwp * PshRataYogyakarta * PerformanceRatio;

    public static double SetaraPohon(double totalCo2Kg, int jumlahHari)
        => jumlahHari <= 0 ? 0 : totalCo2Kg / jumlahHari * 365 / SerapanPohonKgPerTahun;
}
