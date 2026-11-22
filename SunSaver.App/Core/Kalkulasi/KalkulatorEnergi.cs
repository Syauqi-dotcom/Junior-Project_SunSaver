namespace SunSaver.App.Core.Kalkulasi;

/// <summary>Spesifikasi panel surya standar yang dipakai untuk menghitung luas & kapasitas.</summary>
public readonly record struct SpesifikasiPanel(double DayaPanelWp, double EfisiensiPersen)
{
    public static readonly SpesifikasiPanel Standar = new(400, 20);

    /// <summary>Luas satu panel = Wp ÷ (1000 W/m² × η). Contoh 400 Wp, η 20% → 2,0 m².</summary>
    public double LuasPanelM2 => DayaPanelWp / (1000 * EfisiensiPersen / 100);
}

/// <summary>
/// Algoritma estimasi produksi listrik PLTS atap.
/// <code>
///   E_harian [kWh] = P [kWp] × PSH × PR_sistem × f_suhu
///   PSH    = radiasi gelombang pendek harian [MJ/m²] ÷ 3,6       (kWh/m²/hari)
///   f_suhu = 1 + γ (T_sel − 25 °C),   γ = −0,4 %/°C
///   T_sel  ≈ T_udara_rata + 20 °C (harian) | T_udara + G × (NOCT − 20)/800 (per jam)
/// </code>
/// PR_sistem 0,86 mewakili rugi inverter, kabel, debu, dan mismatch. Di iklim tropis Indonesia
/// PR total (sistem × suhu) yang dihasilkan sekitar 0,77–0,80.
/// </summary>
public static class KalkulatorEnergi
{
    public const double PrSistem = 0.86;
    public const double KoefisienSuhu = -0.004;
    public const double KenaikanSuhuSelHarian = 20;
    public const double FaktorNoct = (45 - 20) / 800.0;

    /// <summary>Porsi atap yang realistis bisa dipasangi panel (jarak tepi, jalur perawatan, bayangan).</summary>
    public const double PorsiAtapTerpakai = 0.75;

    public static double PshDariMj(double radiasiMjPerM2) => radiasiMjPerM2 / 3.6;

    public static double FaktorSuhu(double suhuSelC) => Math.Clamp(1 + KoefisienSuhu * (suhuSelC - 25), 0.70, 1.05);

    public static double PerformanceRatio(double suhuUdaraRataC) => PrSistem * FaktorSuhu(suhuUdaraRataC + KenaikanSuhuSelHarian);

    public static double ProduksiHarianKwh(double kapasitasKwp, double psh, double suhuUdaraRataC)
        => Math.Max(0, kapasitasKwp * psh * PerformanceRatio(suhuUdaraRataC));

    /// <summary>Daya keluaran sesaat (kW) dari radiasi [W/m²] dan suhu udara.</summary>
    public static double DayaSesaatKw(double kapasitasKwp, double radiasiWm2, double suhuUdaraC)
    {
        if (radiasiWm2 <= 0) return 0;
        var suhuSel = suhuUdaraC + FaktorNoct * radiasiWm2;
        return kapasitasKwp * radiasiWm2 / 1000 * PrSistem * FaktorSuhu(suhuSel);
    }

    /// <summary>Produksi tiap bulan (kWh) untuk sistem berkapasitas tertentu.</summary>
    public static double[] ProduksiBulanan(double kapasitasKwp, RingkasanIklim iklim)
        => iklim.Bulanan.Select(b => ProduksiHarianKwh(kapasitasKwp, b.PshRata, b.SuhuRata) * b.Hari).ToArray();

    /// <summary>Hasil spesifik (kWh per kWp per tahun), tolok ukur potensi surya suatu lokasi.</summary>
    public static double HasilSpesifikTahunan(RingkasanIklim iklim) => ProduksiBulanan(1, iklim).Sum();

    public static int PanelMaksAtap(double luasAtapM2, SpesifikasiPanel panel)
        => Math.Max(0, (int)Math.Floor(luasAtapM2 * PorsiAtapTerpakai / panel.LuasPanelM2));

    public static double LuasDibutuhkanM2(int jumlahPanel, SpesifikasiPanel panel)
        => jumlahPanel * panel.LuasPanelM2 / PorsiAtapTerpakai;

    public static double KwpDariPanel(int jumlahPanel, SpesifikasiPanel panel) => jumlahPanel * panel.DayaPanelWp / 1000;
}
