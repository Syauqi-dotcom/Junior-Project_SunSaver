using SunSaver.App.Core.Estimasi;
using SunSaver.App.Core.Kalkulasi;
using SunSaver.App.Ui.Grafik;
using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App.Ui.Halaman;

/// <summary>
/// Estimator penghematan (padanan "Savings estimator" Project Sunroof): rekomendasi ukuran sistem,
/// perbandingan beli tunai vs cicilan, proyeksi arus kas 20 tahun, tagihan bulanan, dan dampak lingkungan.
/// </summary>
public sealed class EstimasiHalaman : HalamanDasar
{
    private readonly PanelGulir _gulir = new();
    private readonly Label _alamat = Teks.Redup("", 10);

    private readonly Label _kwp = Teks.Buat("—", Tema.Font(30, semibold: true), Tema.Teks);
    private readonly Label _detailSistem = Teks.Buat("", Tema.Font(11), Tema.TeksRedup);
    private readonly LabelBungkus _cakupan = Teks.Bungkus("", Tema.Font(10.5f), Tema.Teks);
    private readonly BilahProgres _bilahCakupan = new() { Warna = Tema.Biru, Margin = new Padding(0, Tema.S(4), 0, Tema.S(14)) };
    private readonly Penggeser _panel = new() { Minimum = 1, Maksimum = 40 };
    private readonly Label _nilaiPanel = Teks.Buat("", Tema.Font(10.5f, semibold: true), Tema.Teks);
    private readonly TombolBulat _btnRekomendasi = new("Pakai rekomendasi", GayaTombol.Teks);

    private readonly TombolBulat _pilTunai = new("Beli tunai", GayaTombol.Pil) { Terpilih = true };
    private readonly TombolBulat _pilCicilan = new("Cicilan", GayaTombol.Pil);
    private readonly KartuStat _statBiaya = new("Biaya awal", Ikon.Uang, Tema.Biru);
    private readonly KartuStat _statBersih = new("Hemat bersih 20 tahun", Ikon.Grafik, Tema.Hijau);
    private readonly KartuStat _statBalik = new("Balik modal", Ikon.Matahari, Tema.OranyeTua);
    private readonly KartuStat _statBulanan = new("Hemat tahun pertama", Ikon.Petir, Tema.Ungu);

    private readonly GrafikKombinasi _grafikArus = new() { Height = Tema.S(300) };
    private readonly GrafikKombinasi _grafikTagihan = new() { Height = Tema.S(280) };

    private readonly Label _co2 = Teks.Buat("—", Tema.Font(19, semibold: true), Tema.Hijau);
    private readonly LabelBungkus _co2Detail = Teks.Bungkus("", Tema.Font(9.5f));
    private readonly HutanVirtual _hutan = new() { Height = Tema.S(96), Margin = new Padding(0, Tema.S(8), 0, Tema.S(8)) };
    private readonly Label _lencana = Teks.Buat("", Tema.Font(11, semibold: true), Tema.Teks);

    private readonly NumericUpDown _biayaKwp = Isian.Angka(5, 40, 1, 0.5m, 15);
    private readonly NumericUpDown _kenaikan = Isian.Angka(0, 15, 1, 0.5m, 3);
    private readonly NumericUpDown _bunga = Isian.Angka(0, 30, 1, 0.5m, 10);
    private readonly NumericUpDown _tenor = Isian.Angka(1, 15, 0, 1, 5);
    private readonly NumericUpDown _dp = Isian.Angka(0, 100, 0, 5, 20);
    private readonly NumericUpDown _emisi = Isian.Angka(0.3m, 1.5m, 3, 0.01m, 0.794m);

    private readonly LabelBungkus _catatan = Teks.Bungkus("", Tema.Font(9.5f), Tema.OranyeTua);
    private readonly TombolBulat _btnSimpan = new("Simpan ke Instalasi Saya");
    private readonly System.Windows.Forms.Timer _tunda = new() { Interval = 150 };

    private HasilEstimasi? _hasil;
    private bool _mengisi, _cicilan;

    public EstimasiHalaman(KonteksAplikasi k) : base(k)
    {
        var kembali = new TombolBulat("← Kembali ke peta atap", GayaTombol.Teks) { Margin = new Padding(-Tema.S(12), 0, 0, Tema.S(4)) };
        kembali.Click += (_, _) => K.Shell.Ke(HalamanId.HasilAtap);

        // ---------------- rekomendasi ukuran ----------------
        var rekom = new Kartu { Padding = Tema.P(28, 24, 28, 20) };
        var barisPanel = new Baris();
        barisPanel.Tambah(Teks.Buat("Sesuaikan jumlah panel", Tema.Font(9.5f, semibold: true), Tema.Teks), 1).Tambah(_nilaiPanel);
        _panel.AccessibleName = "Jumlah panel surya";
        _detailSistem.Margin = new Padding(0, 0, 0, Tema.S(12));
        _btnRekomendasi.Margin = new Padding(-Tema.S(12), 0, 0, 0);
        rekom.Controls.AddRange(
        [
            Teks.Redup("Ukuran instalasi yang direkomendasikan", 10.5f), _kwp, _detailSistem, _cakupan, _bilahCakupan,
            barisPanel, _panel, _btnRekomendasi,
        ]);

        // ---------------- opsi pembiayaan ----------------
        var pil = new Baris { Jarak = Tema.S(8), Margin = new Padding(0, Tema.S(8), 0, Tema.S(14)) };
        pil.Tambah(Teks.Buat("Cara pembayaran:", Tema.Font(10.5f, semibold: true), Tema.Teks)).Tambah(_pilTunai.Pas()).Tambah(_pilCicilan.Pas());
        var stat = new GridKartu { Kolom = 4, LebarMinKolom = Tema.S(200) };
        stat.Controls.AddRange([_statBiaya, _statBersih, _statBalik, _statBulanan]);

        var kartuArus = new Kartu();
        kartuArus.Controls.Add(Teks.Subjudul("Proyeksi arus kas kumulatif 20 tahun"));
        kartuArus.Controls.Add(Teks.Bungkus("Garis di atas nol berarti investasi panel surya sudah balik modal. Termasuk kenaikan tarif, " +
                                            "degradasi panel 0,5%/tahun, dan biaya perawatan 1%/tahun.", Tema.Font(9.5f)));
        kartuArus.Controls.Add(_grafikArus);
        _grafikArus.FormatKiri = Tema.RpRingkas;

        var kartuTagihan = new Kartu();
        kartuTagihan.Controls.Add(Teks.Subjudul("Tagihan listrik bulanan: tanpa vs dengan panel surya (tahun pertama)"));
        kartuTagihan.Controls.Add(Teks.Bungkus("Produksi bulanan mengikuti radiasi matahari & tutupan awan di lokasi Anda. Kelebihan produksi " +
                                               "tidak mengurangi tagihan (tanpa net-metering, Permen ESDM 2/2024).", Tema.Font(9.5f)));
        kartuTagihan.Controls.Add(_grafikTagihan);
        _grafikTagihan.FormatKiri = Tema.RpRingkas;
        _grafikTagihan.FormatTooltip = (_, v) => Tema.Rp(v);

        // ---------------- lingkungan + asumsi ----------------
        var kartuHijau = new Kartu { BackColor = Tema.HijauMuda, WarnaGaris = Color.Empty };
        kartuHijau.Controls.AddRange(
        [
            Teks.Subjudul("Dampak lingkungan"), _co2, _co2Detail, _hutan, _lencana,
        ]);

        var kartuAsumsi = new Kartu();
        var gridAsumsi = new GridKartu { Kolom = 2, LebarMinKolom = Tema.S(150), Jarak = Tema.S(12), Margin = Padding.Empty };
        gridAsumsi.Controls.AddRange(
        [
            Isian.Bidang("Biaya sistem", _biayaKwp, "jt/kWp"),
            Isian.Bidang("Kenaikan tarif", _kenaikan, "%/thn"),
            Isian.Bidang("Bunga cicilan", _bunga, "%/thn"),
            Isian.Bidang("Tenor", _tenor, "tahun"),
            Isian.Bidang("Uang muka", _dp, "%"),
            Isian.Bidang("Faktor emisi grid", _emisi, "kg/kWh"),
        ]);
        var reset = new TombolBulat("Kembalikan asumsi default", GayaTombol.Teks) { Margin = new Padding(-Tema.S(12), 0, 0, 0) };
        reset.Click += (_, _) => AturAsumsi(AsumsiFinansial.Default, KalkulatorKarbon.FaktorEmisiDefault, picu: true);
        kartuAsumsi.Controls.AddRange([Teks.Subjudul("Asumsi perhitungan"), gridAsumsi, reset]);

        var bawah = new GridKartu { Kolom = 2, LebarMinKolom = Tema.S(380) };
        bawah.Controls.AddRange([kartuHijau, kartuAsumsi]);

        var aksi = new Baris { Jarak = Tema.S(10), Margin = new Padding(0, Tema.S(8), 0, 0) };
        var keDashboard = new TombolBulat("Buka Dashboard Dampak", GayaTombol.Sekunder);
        keDashboard.Click += (_, _) => K.Shell.Ke(HalamanId.Dashboard);
        aksi.Tambah(_btnSimpan.Pas()).Tambah(keDashboard.Pas());

        _gulir.Controls.AddRange(
        [
            kembali, Teks.Judul("Estimasi penghematan Anda", 22), _alamat,
            rekom, pil, stat, kartuArus, kartuTagihan, bawah, _catatan, aksi,
        ]);
        _alamat.Margin = new Padding(0, 0, 0, Tema.S(18));
        Controls.Add(_gulir);

        // ---------------- perilaku ----------------
        _panel.NilaiBerubah += (_, _) =>
        {
            _nilaiPanel.Text = $"{_panel.Nilai} panel";
            if (_mengisi) return;
            K.Masukan = K.Masukan with { JumlahPanel = _panel.Nilai };
            Tunda();
        };
        _btnRekomendasi.Click += (_, _) => { K.Masukan = K.Masukan with { JumlahPanel = null }; Hitung(); };
        _pilTunai.Click += (_, _) => PilihOpsi(false);
        _pilCicilan.Click += (_, _) => PilihOpsi(true);
        foreach (var n in new[] { _biayaKwp, _kenaikan, _bunga, _tenor, _dp, _emisi })
            n.ValueChanged += (_, _) => { if (!_mengisi) Tunda(); };
        _tunda.Tick += (_, _) => { _tunda.Stop(); BacaAsumsi(); Hitung(); };
        _btnSimpan.Click += async (_, _) => await SimpanAsync();
    }

    public override string Judul => "Estimasi penghematan";

    public override void Tampil(object? argumen)
    {
        if (K.HasilTerakhir is null || K.Lokasi is null)
        {
            K.Shell.Ke(K.Lokasi is null ? HalamanId.Beranda : HalamanId.HasilAtap);
            return;
        }
        _alamat.Text = K.Lokasi.Nama;
        AturAsumsi(K.Masukan.Asumsi, K.Masukan.FaktorEmisi, picu: false);
        _gulir.KeAtas();
        Hitung();
    }

    private void Tunda()
    {
        _tunda.Stop();
        _tunda.Start();
    }

    private void AturAsumsi(AsumsiFinansial a, double emisi, bool picu)
    {
        _mengisi = true;
        _biayaKwp.Value = Math.Clamp((decimal)(a.BiayaPerKwp / 1e6), _biayaKwp.Minimum, _biayaKwp.Maximum);
        _kenaikan.Value = Math.Clamp((decimal)a.KenaikanTarifPersen, _kenaikan.Minimum, _kenaikan.Maximum);
        _bunga.Value = Math.Clamp((decimal)a.BungaPinjamanPersen, _bunga.Minimum, _bunga.Maximum);
        _tenor.Value = Math.Clamp(a.TenorTahun, _tenor.Minimum, _tenor.Maximum);
        _dp.Value = Math.Clamp((decimal)a.UangMukaPersen, _dp.Minimum, _dp.Maximum);
        _emisi.Value = Math.Clamp((decimal)emisi, _emisi.Minimum, _emisi.Maximum);
        _mengisi = false;
        if (picu) { BacaAsumsi(); Hitung(); }
    }

    private void BacaAsumsi()
    {
        K.Masukan = K.Masukan with
        {
            Asumsi = K.Masukan.Asumsi with
            {
                BiayaPerKwp = (double)_biayaKwp.Value * 1e6,
                KenaikanTarifPersen = (double)_kenaikan.Value,
                BungaPinjamanPersen = (double)_bunga.Value,
                TenorTahun = (int)_tenor.Value,
                UangMukaPersen = (double)_dp.Value,
            },
            FaktorEmisi = (double)_emisi.Value,
        };
    }

    private void PilihOpsi(bool cicilan)
    {
        _cicilan = cicilan;
        _pilTunai.Terpilih = !cicilan;
        _pilCicilan.Terpilih = cicilan;
        TampilkanFinansial();
    }

    private void Hitung()
    {
        if (K.HasilTerakhir is not { } lama) return;
        var h = LayananEstimasi.Hitung(K.Masukan, lama.Iklim);
        K.HasilTerakhir = h;
        _hasil = h;
        var s = h.Sistem;

        _kwp.Text = $"{Tema.N(s.KapasitasKwp, 1)} kWp";
        _detailSistem.Text = $"{s.JumlahPanel} panel × {Tema.N(h.Masukan.DayaPanelWp)} Wp · butuh ±{Tema.N(h.Atap.LuasDibutuhkanM2)} m² atap · " +
                             $"produksi ±{Tema.N(s.ProduksiTahunanKwh)} kWh/tahun";
        _cakupan.Text = K.Masukan.JumlahPanel is null
            ? $"Ukuran ini mencakup sekitar {Tema.N(s.CakupanPersen)}% kebutuhan listrik Anda ({Tema.N(h.KonsumsiBulananKwh)} kWh/bulan)."
            : $"Dengan {s.JumlahPanel} panel, sekitar {Tema.N(s.CakupanPersen)}% kebutuhan listrik Anda tercukupi. Rekomendasi: {s.PanelRekomendasi} panel ({Tema.N(s.RekomendasiKwp, 1)} kWp).";
        _bilahCakupan.Nilai = s.CakupanPersen;
        _btnRekomendasi.Visible = K.Masukan.JumlahPanel is not null && K.Masukan.JumlahPanel != s.PanelRekomendasi;

        _mengisi = true;
        _panel.Maksimum = Math.Max(10, Math.Max(h.Atap.PanelMaks ?? 0, s.PanelRekomendasi * 2));
        _panel.AturDiam(s.JumlahPanel);
        _nilaiPanel.Text = $"{s.JumlahPanel} panel";
        _mengisi = false;

        _pilCicilan.Text = $"Cicilan {h.Masukan.Asumsi.TenorTahun} tahun";
        _pilCicilan.Pas();
        _pilCicilan.Parent?.PerformLayout();
        TampilkanFinansial();

        // tagihan bulanan
        _grafikTagihan.IndeksPenanda = DateTime.Now.Month - 1;
        _grafikTagihan.Atur(Tema.NamaBulanPendek,
            new Seri("Tanpa panel surya", h.Bulanan.Select(b => b.TagihanTanpaSuryaRp).ToArray(), Tema.Hex("#BDC1C6")),
            new Seri("Dengan panel surya", h.Bulanan.Select(b => b.TagihanDenganSuryaRp).ToArray(), Tema.Biru),
            new Seri("Hemat", h.Bulanan.Select(b => b.HematRp).ToArray(), Tema.Hijau, JenisSeri.Garis));

        // lingkungan
        var l = h.Lingkungan;
        _co2.Text = $"{Tema.N(l.Co2TahunanKg / 1000, 1)} ton CO₂/tahun";
        _co2Detail.Text = $"Selama 20 tahun sistem ini menghindari ±{Tema.N(l.Co2UmurSistemTon, 1)} ton CO₂, " +
                          $"setara penyerapan {Tema.N(l.SetaraPohon)} pohon setiap tahun (faktor emisi grid {Tema.N(l.FaktorEmisi, 3)} kg CO₂/kWh).";
        _hutan.JumlahPohon = l.SetaraPohon;
        _lencana.Text = $"Lencana: {l.Lencana.Nama} — {l.Lencana.Deskripsi}";

        _catatan.Text = string.Join(Environment.NewLine, h.Catatan.Select(c => "• " + c));
        _catatan.Visible = h.Catatan.Count > 0;
        _gulir.PerformLayout();
    }

    private void TampilkanFinansial()
    {
        if (_hasil is not { } h) return;
        var f = h.Finansial;
        var o = _cicilan ? f.Cicilan : f.Tunai;

        _statBiaya.Atur(Tema.RpRingkas(o.BiayaAwal), _cicilan
            ? $"Uang muka {Tema.N(h.Masukan.Asumsi.UangMukaPersen)}% · cicilan {Tema.Rp(o.CicilanBulanan)}/bulan selama {h.Masukan.Asumsi.TenorTahun} tahun"
            : $"Harga sistem {Tema.N(h.Sistem.KapasitasKwp, 1)} kWp × {Tema.RpRingkas(h.Masukan.Asumsi.BiayaPerKwp)}/kWp");
        _statBersih.Atur(Tema.RpRingkas(o.HematBersih), $"Total hemat {Tema.RpRingkas(o.TotalHemat)} − total biaya {Tema.RpRingkas(o.TotalBiaya)}");
        _statBalik.Atur(Tema.Tahun(o.BalikModalTahun), o.BalikModalTahun is null ? "Belum balik modal dalam 20 tahun" : "Waktu hingga penghematan menutup biaya");
        _statBulanan.Atur(Tema.RpRingkas(f.HematTahunPertama), $"±{Tema.Rp(f.HematTahunPertama / 12)} per bulan");

        var tahun = f.ArusKas.Select(a => a.Tahun == 0 ? "Awal" : $"Th {a.Tahun}").ToArray();
        _grafikArus.Atur(tahun,
            new Seri("Beli tunai", f.ArusKas.Select(a => a.KumulatifTunai).ToArray(), Tema.Biru, _cicilan ? JenisSeri.Garis : JenisSeri.Area) { PutusPutus = _cicilan },
            new Seri($"Cicilan {h.Masukan.Asumsi.TenorTahun} tahun", f.ArusKas.Select(a => a.KumulatifCicilan).ToArray(), Tema.Ungu, _cicilan ? JenisSeri.Area : JenisSeri.Garis) { PutusPutus = !_cicilan });
        _grafikArus.FormatTooltip = (_, v) => Tema.Rp(v);
    }

    private async Task SimpanAsync()
    {
        _btnSimpan.Enabled = false;
        await Aman(async () =>
        {
            var repo = K.Akun.Repositori;
            var inst = await repo.SimpanAsync(K.InstalasiDariSesi());
            if (_hasil is { } h && inst.Id is { } id)
                await repo.TambahRiwayatAsync(new Core.Model.RiwayatEstimasi
                {
                    InstalasiId = id,
                    PshRataHarian = h.Iklim.PshRataHarian,
                    ProduksiTahunanKwh = h.Sistem.ProduksiTahunanKwh,
                    HematTahunanRp = h.Finansial.HematTahunPertama,
                    Co2TahunanKg = h.Lingkungan.Co2TahunanKg,
                    SetaraPohon = h.Lingkungan.SetaraPohon,
                });
            K.UmumkanInstalasiBerubah();
            K.Shell.Notifikasi(repo.Nama == "Supabase"
                ? $"\"{inst.Nama}\" tersimpan ke cloud (Supabase)."
                : $"\"{inst.Nama}\" tersimpan di komputer ini. Masuk akun untuk menyimpan ke cloud.");
        });
        _btnSimpan.Enabled = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tunda.Dispose();
        base.Dispose(disposing);
    }
}
