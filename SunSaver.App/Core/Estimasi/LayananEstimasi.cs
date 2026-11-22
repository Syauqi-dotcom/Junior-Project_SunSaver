using System.Globalization;
using SunSaver.App.Core.Cuaca;
using SunSaver.App.Core.Kalkulasi;
using SunSaver.App.Core.Model;

namespace SunSaver.App.Core.Estimasi;

/// <summary>Mengorkestrasi data iklim + kalkulator energi, karbon, dan finansial menjadi satu hasil estimasi.</summary>
public class LayananEstimasi(LayananIklim iklim)
{
    private static readonly CultureInfo Id = new("id-ID");

    public async Task<HasilEstimasi> HitungAsync(EstimasiMasukan m, CancellationToken ct = default)
        => Hitung(m, await iklim.RingkasanTahunanAsync(m.Latitude, m.Longitude, ct));

    public static HasilEstimasi Hitung(EstimasiMasukan m, RingkasanIklim r)
    {
        var catatan = new List<string>();
        var panel = new SpesifikasiPanel(m.DayaPanelWp, m.EfisiensiPersen);
        var hasilSpesifik = KalkulatorEnergi.HasilSpesifikTahunan(r);

        // --- konsumsi & ukuran sistem ---
        var konsumsiBulanan = m.TarifPerKwh > 0 ? m.TagihanBulananRp / m.TarifPerKwh : 0;
        var konsumsiTahunan = konsumsiBulanan * 12;

        int? panelMaks = m.LuasAtapM2 is { } luas ? KalkulatorEnergi.PanelMaksAtap(luas, panel) : null;
        var kebutuhanKwp = hasilSpesifik > 0 ? konsumsiTahunan / hasilSpesifik : 0;
        // Dibulatkan ke panel terdekat: tanpa net-metering, kelebihan produksi tidak bernilai,
        // jadi sedikit di bawah 100% lebih ekonomis daripada membulatkan ke atas.
        var panelRekomendasi = Math.Max(1, (int)Math.Round(kebutuhanKwp * 1000 / panel.DayaPanelWp, MidpointRounding.AwayFromZero));
        var dibatasiAtap = false;
        if (panelMaks is { } maks && panelRekomendasi > maks)
        {
            panelRekomendasi = Math.Max(1, maks);
            dibatasiAtap = true;
            catatan.Add("Ukuran rekomendasi dibatasi oleh luas atap yang tersedia.");
        }
        if (panelMaks is 0)
            catatan.Add("Atap terlalu kecil untuk satu panel standar; hasil memakai 1 panel sebagai ilustrasi.");

        var jumlahPanel = Math.Max(1, m.JumlahPanel ?? panelRekomendasi);
        var kapasitas = KalkulatorEnergi.KwpDariPanel(jumlahPanel, panel);

        if (panelMaks is { } pm && jumlahPanel > pm && m.JumlahPanel is not null)
            catatan.Add($"{jumlahPanel} panel butuh ±{KalkulatorEnergi.LuasDibutuhkanM2(jumlahPanel, panel).ToString("N0", Id)} m² atap, melebihi luas yang tersedia.");

        if (DataReferensi.CariTarif(m.KodeTarif)?.DayaVa is { } va && kapasitas * 1000 > va)
            catatan.Add($"Kapasitas {kapasitas.ToString("N1", Id)} kWp melebihi daya tersambung {va.ToString("N0", Id)} VA. Cek ketentuan kuota PLTS atap PLN; mungkin perlu tambah daya.");

        // --- produksi & penghematan bulanan (tahun pertama) ---
        var produksiBulanan = KalkulatorEnergi.ProduksiBulanan(kapasitas, r);
        var bulanan = r.Bulanan.Select((b, i) =>
        {
            var prod = produksiBulanan[i];
            var kons = konsumsiBulanan * b.Hari * 12 / 365.0;
            var terpakai = Math.Min(prod, kons);
            return new BulanEstimasi(
                b.Bulan, prod, kons, terpakai,
                kons * m.TarifPerKwh,
                (kons - terpakai) * m.TarifPerKwh,
                terpakai * m.TarifPerKwh,
                KalkulatorKarbon.Co2Kg(prod, m.FaktorEmisi));
        }).ToList();

        var produksiTahunan = produksiBulanan.Sum();
        var terpakaiTahunan = bulanan.Sum(b => b.TerpakaiKwh);
        var cakupan = konsumsiTahunan > 0 ? Math.Min(100, terpakaiTahunan / konsumsiTahunan * 100) : 0;
        if (konsumsiTahunan > 0 && produksiTahunan > konsumsiTahunan * 1.1)
            catatan.Add("Produksi melebihi konsumsi. Sejak Permen ESDM No. 2/2024, kelebihan energi yang diekspor ke PLN tidak mengurangi tagihan.");

        // --- finansial ---
        var finansial = KalkulatorFinansial.Hitung(kapasitas, produksiBulanan,
            bulanan.Select(b => b.KonsumsiKwh).ToArray(), m.TarifPerKwh, m.Asumsi);

        // --- lingkungan ---
        var co2Tahunan = KalkulatorKarbon.Co2Kg(produksiTahunan, m.FaktorEmisi);
        var co2Umur = finansial.ArusKas.Sum(a => KalkulatorKarbon.Co2Kg(a.ProduksiKwh, m.FaktorEmisi)) / 1000;
        var pohon = KalkulatorKarbon.SetaraPohon(co2Tahunan);

        return new HasilEstimasi(
            m,
            r,
            hasilSpesifik,
            new RingkasanAtap(m.LuasAtapM2, panel.LuasPanelM2, panelMaks, KalkulatorEnergi.LuasDibutuhkanM2(jumlahPanel, panel)),
            new RingkasanSistem(
                kapasitas,
                jumlahPanel,
                panelRekomendasi,
                KalkulatorEnergi.KwpDariPanel(panelRekomendasi, panel),
                cakupan,
                produksiTahunan,
                produksiTahunan / Math.Max(1, r.JumlahHari),
                produksiTahunan / Math.Max(1e-9, kapasitas * r.JamMatahariPuncakTahunan),
                dibatasiAtap),
            konsumsiBulanan,
            bulanan,
            finansial,
            new RingkasanLingkungan(m.FaktorEmisi, co2Tahunan, co2Umur, pohon, KalkulatorKarbon.TentukanLencana(pohon)),
            catatan);
    }

    // ------------------------------- Prakiraan (real-time) -------------------------------

    public async Task<HasilPrakiraan> PrakiraanAsync(double lat, double lon, double kapasitasKwp, double tarif,
        double faktorEmisi, bool paksaSegar = false, CancellationToken ct = default)
        => HitungPrakiraan(await iklim.PrakiraanAsync(lat, lon, paksaSegar, ct), kapasitasKwp, tarif, faktorEmisi);

    public static HasilPrakiraan HitungPrakiraan(PrakiraanMentah m, double kwp, double tarif, double ef)
    {
        var h = m.Harian;
        var harian = new List<HariPrakiraan>();
        for (var i = 0; i < h.Tanggal.Count; i++)
        {
            if (!DateOnly.TryParseExact(h.Tanggal[i], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tgl))
                continue;
            var mj = Nilai(h.RadiasiMj, i);
            var psh = KalkulatorEnergi.PshDariMj(mj);
            var tMaks = Nilai(h.SuhuMaks, i);
            var tMin = Nilai(h.SuhuMin, i);
            var prod = KalkulatorEnergi.ProduksiHarianKwh(kwp, psh, (tMaks + tMin) / 2);
            harian.Add(new HariPrakiraan(tgl, mj, psh, Nilai(h.SinarDetik, i) / 3600, tMaks, tMin, Nilai(h.Awan, i),
                prod, prod * tarif, KalkulatorKarbon.Co2Kg(prod, ef)));
        }

        var hariIni = new List<JamPrakiraan>();
        var tanggalPertama = h.Tanggal.FirstOrDefault() ?? "";
        var j = m.PerJam;
        for (var i = 0; i < j.Waktu.Count; i++)
        {
            if (!j.Waktu[i].StartsWith(tanggalPertama, StringComparison.Ordinal)
                || !DateTime.TryParse(j.Waktu[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var w)) continue;
            var g = Nilai(j.Radiasi, i);
            var t = Nilai(j.Suhu, i);
            hariIni.Add(new JamPrakiraan(w, g, Nilai(j.Awan, i), t, KalkulatorEnergi.DayaSesaatKw(kwp, g, t)));
        }

        KondisiSaatIniDto? saatIni = null;
        if (m.SaatIni is { } s && DateTime.TryParse(s.Waktu, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ws))
            saatIni = new KondisiSaatIniDto(ws, s.Suhu ?? 0, s.Awan ?? 0, s.Radiasi ?? 0, s.Siang == 1,
                KalkulatorEnergi.DayaSesaatKw(kwp, s.Radiasi ?? 0, s.Suhu ?? 27));

        var totalProd = harian.Sum(x => x.ProduksiKwh);
        var totalCo2 = KalkulatorKarbon.Co2Kg(totalProd, ef);
        var pohon = harian.Count == 0 ? 0 : KalkulatorKarbon.SetaraPohon(totalCo2 / harian.Count * 365);

        return new HasilPrakiraan(m.ZonaWaktu, kwp, saatIni, harian, hariIni, totalProd, totalProd * tarif, totalCo2, pohon);
    }

    private static double Nilai(List<double?> data, int i) => i < data.Count && data[i] is { } v ? v : 0;
}
