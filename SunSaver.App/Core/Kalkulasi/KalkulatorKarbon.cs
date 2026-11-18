namespace SunSaver.App.Core.Kalkulasi;

public record Lencana(int Level, string Nama, string Deskripsi, double AmbangPohon, double? AmbangBerikutnya, double ProgresPersen);

/// <summary>
/// Ekuivalensi emisi karbon: CO₂ [kg] = E [kWh] × faktor emisi grid lokal [kg CO₂e/kWh],
/// lalu dikonversi ke "setara pohon" (gamifikasi) memakai serapan rata-rata satu pohon per tahun.
/// </summary>
public static class KalkulatorKarbon
{
    /// <summary>Faktor emisi grid Jawa-Madura-Bali, kg CO₂e/kWh (sama dengan Modul 4).</summary>
    public const double FaktorEmisiDefault = 0.794;

    /// <summary>Serapan CO₂ rata-rata satu pohon dewasa per tahun (kg).</summary>
    public const double SerapanPohonKgPerTahun = 21.77;

    public static readonly IReadOnlyList<(double Ambang, string Nama, string Deskripsi)> Tingkat =
    [
        (0, "Benih", "Mulai perjalanan energi bersihmu."),
        (1, "Tunas Surya", "Setara menanam pohon pertamamu."),
        (10, "Kebun Hijau", "Kontribusimu setara sebuah kebun kecil."),
        (25, "Taman Kota", "Setara taman kota yang rindang."),
        (50, "Hutan Kecil", "Atapmu bekerja seperti hutan kecil."),
        (100, "Penjaga Hutan", "Dampak setara lebih dari seratus pohon."),
    ];

    public static double Co2Kg(double energiKwh, double faktorEmisi) => energiKwh * faktorEmisi;

    /// <summary>Jumlah pohon yang dibutuhkan untuk menyerap CO₂ tahunan yang sama.</summary>
    public static double SetaraPohon(double co2KgPerTahun) => co2KgPerTahun / SerapanPohonKgPerTahun;

    public static Lencana TentukanLencana(double pohon)
    {
        var idx = 0;
        for (var i = 0; i < Tingkat.Count; i++)
            if (pohon >= Tingkat[i].Ambang) idx = i;

        var t = Tingkat[idx];
        double? berikut = idx + 1 < Tingkat.Count ? Tingkat[idx + 1].Ambang : null;
        var progres = berikut is null ? 100 : Math.Clamp((pohon - t.Ambang) / (berikut.Value - t.Ambang) * 100, 0, 100);
        return new Lencana(idx, t.Nama, t.Deskripsi, t.Ambang, berikut, Math.Round(progres, 1));
    }
}
