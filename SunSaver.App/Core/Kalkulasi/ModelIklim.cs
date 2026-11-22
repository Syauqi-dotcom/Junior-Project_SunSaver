namespace SunSaver.App.Core.Kalkulasi;

/// <summary>Agregat iklim satu bulan dari data harian Open-Meteo Archive.</summary>
public record IklimBulanan(
    int Bulan,
    int Hari,
    double PshRata,
    double JamSinarMatahari,
    double SuhuRata,
    double TutupanAwanRata);

/// <summary>Ringkasan iklim satu tahun kalender untuk suatu koordinat.</summary>
public record RingkasanIklim(
    double Latitude,
    double Longitude,
    double Elevasi,
    string ZonaWaktu,
    int Tahun,
    IReadOnlyList<IklimBulanan> Bulanan)
{
    /// <summary>Σ PSH setahun = jam matahari puncak (setara 1000 W/m²) per tahun.</summary>
    public double JamMatahariPuncakTahunan => Bulanan.Sum(b => b.PshRata * b.Hari);

    /// <summary>Total durasi penyinaran (definisi WMO: radiasi langsung &gt; 120 W/m²) setahun.</summary>
    public double JamSinarMatahariTahunan => Bulanan.Sum(b => b.JamSinarMatahari);

    public int JumlahHari => Bulanan.Sum(b => b.Hari);
    public double PshRataHarian => JumlahHari == 0 ? 0 : JamMatahariPuncakTahunan / JumlahHari;
    public double SuhuRata => JumlahHari == 0 ? 0 : Bulanan.Sum(b => b.SuhuRata * b.Hari) / JumlahHari;
    public double TutupanAwanRata => JumlahHari == 0 ? 0 : Bulanan.Sum(b => b.TutupanAwanRata * b.Hari) / JumlahHari;
}
