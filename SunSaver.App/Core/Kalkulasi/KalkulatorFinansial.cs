namespace SunSaver.App.Core.Kalkulasi;

public record AsumsiFinansial(
    double BiayaPerKwp,
    double KenaikanTarifPersen,
    double DegradasiPanelPersen,
    double PerawatanPersenPerTahun,
    double UangMukaPersen,
    double BungaPinjamanPersen,
    int TenorTahun,
    int UmurAnalisisTahun)
{
    public static readonly AsumsiFinansial Default = new(
        BiayaPerKwp: 15_000_000,
        KenaikanTarifPersen: 3,
        DegradasiPanelPersen: 0.5,
        PerawatanPersenPerTahun: 1,
        UangMukaPersen: 20,
        BungaPinjamanPersen: 10,
        TenorTahun: 5,
        UmurAnalisisTahun: 20);
}

public record OpsiPembiayaan(
    string Kode,
    string Nama,
    double BiayaAwal,
    double CicilanBulanan,
    double TotalBiaya,
    double TotalHemat,
    double HematBersih,
    double? BalikModalTahun);

public record ArusKasTahunan(
    int Tahun,
    double ProduksiKwh,
    double HematRp,
    double TagihanTanpaSuryaRp,
    double TagihanDenganSuryaRp,
    double KumulatifTunai,
    double KumulatifCicilan);

public record HasilFinansial(
    double BiayaSistem,
    double HematTahunPertama,
    double TotalTagihanTanpaSurya,
    double TotalTagihanDenganSurya,
    OpsiPembiayaan Tunai,
    OpsiPembiayaan Cicilan,
    IReadOnlyList<ArusKasTahunan> ArusKas);

/// <summary>
/// Proyeksi keuangan N tahun. Mengikuti Permen ESDM No. 2/2024 (tanpa kompensasi ekspor ke PLN):
/// listrik surya hanya mengurangi tagihan sebesar konsumsi sendiri, jadi
/// hemat bulanan = min(produksi, konsumsi) × tarif.
/// </summary>
public static class KalkulatorFinansial
{
    /// <summary>Anuitas: A = P·r / (1 − (1 + r)^−n).</summary>
    public static double CicilanBulanan(double pokok, double bungaPersenTahun, int tenorTahun)
    {
        if (pokok <= 0 || tenorTahun <= 0) return 0;
        var r = bungaPersenTahun / 100 / 12;
        var n = tenorTahun * 12;
        return r == 0 ? pokok / n : pokok * r / (1 - Math.Pow(1 + r, -n));
    }

    public static HasilFinansial Hitung(
        double kapasitasKwp,
        IReadOnlyList<double> produksiBulananTahun1,
        IReadOnlyList<double> konsumsiBulananKwh,
        double tarifPerKwh,
        AsumsiFinansial a)
    {
        var biaya = kapasitasKwp * a.BiayaPerKwp;
        var perawatan = biaya * a.PerawatanPersenPerTahun / 100;
        var uangMuka = biaya * a.UangMukaPersen / 100;
        var cicilan = CicilanBulanan(biaya - uangMuka, a.BungaPinjamanPersen, a.TenorTahun);

        var arus = new List<ArusKasTahunan> { new(0, 0, 0, 0, 0, -biaya, -uangMuka) };

        double kumTunai = -biaya, kumCicilan = -uangMuka;
        double totalHemat = 0, totalTanpa = 0, totalDengan = 0, totalCicilan = 0;
        double? balikTunai = null, balikCicilan = null;

        for (var t = 1; t <= a.UmurAnalisisTahun; t++)
        {
            var faktorDegradasi = Math.Pow(1 - a.DegradasiPanelPersen / 100, t - 1);
            var tarif = tarifPerKwh * Math.Pow(1 + a.KenaikanTarifPersen / 100, t - 1);

            double produksi = 0, terpakai = 0, konsumsi = 0;
            for (var b = 0; b < produksiBulananTahun1.Count; b++)
            {
                var pt = produksiBulananTahun1[b] * faktorDegradasi;
                var kb = b < konsumsiBulananKwh.Count ? konsumsiBulananKwh[b] : 0;
                produksi += pt;
                konsumsi += kb;
                terpakai += Math.Min(pt, kb);
            }

            var hemat = terpakai * tarif;
            var tanpa = konsumsi * tarif;
            var cicilanTahunIni = t <= a.TenorTahun ? cicilan * 12 : 0;

            var sebelumTunai = kumTunai;
            var sebelumCicilan = kumCicilan;
            kumTunai += hemat - perawatan;
            kumCicilan += hemat - perawatan - cicilanTahunIni;

            balikTunai ??= Lintas(sebelumTunai, kumTunai, t);
            balikCicilan ??= Lintas(sebelumCicilan, kumCicilan, t);

            totalHemat += hemat;
            totalTanpa += tanpa;
            totalDengan += tanpa - hemat;
            totalCicilan += cicilanTahunIni;

            arus.Add(new ArusKasTahunan(t, produksi, hemat, tanpa, tanpa - hemat, kumTunai, kumCicilan));
        }

        var totalPerawatan = perawatan * a.UmurAnalisisTahun;
        var tunai = new OpsiPembiayaan("tunai", "Beli tunai", biaya, 0, biaya + totalPerawatan, totalHemat,
            totalHemat - biaya - totalPerawatan, balikTunai);
        var kredit = new OpsiPembiayaan("cicilan", $"Cicilan {a.TenorTahun} tahun", uangMuka, cicilan,
            uangMuka + totalCicilan + totalPerawatan, totalHemat,
            totalHemat - uangMuka - totalCicilan - totalPerawatan, balikCicilan);

        return new HasilFinansial(biaya, arus.Count > 1 ? arus[1].HematRp : 0, totalTanpa, totalDengan, tunai, kredit, arus);
    }

    /// <summary>Tahun (pecahan) saat arus kas kumulatif melewati nol; null bila belum.</summary>
    private static double? Lintas(double sebelum, double sesudah, int tahun)
    {
        if (sebelum >= 0) return tahun - 1;
        if (sesudah < 0) return null;
        return Math.Round(tahun - 1 + -sebelum / (sesudah - sebelum), 1);
    }
}
