using SunSaver.App.Core.Estimasi;
using SunSaver.App.Core.Kalkulasi;
using SunSaver.App.Core.Model;
using SunSaver.App.Ui.Grafik;
using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App.Ui.Halaman;

/// <summary>
/// Dashboard Dampak Lingkungan: kondisi matahari real-time, prakiraan produksi 7 hari,
/// komparasi tagihan bulanan, metrik gamifikasi "setara pohon", dan riwayat snapshot instalasi.
/// </summary>
public sealed class DashboardHalaman : HalamanDasar
{
    private sealed record Sumber(string Nama, Instalasi? Instalasi)
    {
        public override string ToString() => Nama;
    }

    private readonly PanelGulir _gulir = new();
    private readonly ComboBox _sumber = Isian.Pilihan([]);
    private readonly TombolBulat _btnMuat = new("Muat ulang", GayaTombol.Sekunder);
    private readonly TombolBulat _btnSnapshot = new("Simpan snapshot", GayaTombol.Utama);
    private readonly LabelBungkus _subjudul = Teks.Bungkus("", Tema.Font(10));

    private readonly Label _rtRadiasi = Teks.Angka("—", 17);
    private readonly Label _rtDaya = Teks.Angka("—", 17);
    private readonly Label _rtAwan = Teks.Angka("—", 17);
    private readonly Label _rtSuhu = Teks.Angka("—", 17);
    private readonly Label _rtWaktu = Teks.Redup("", 8.5f);

    private readonly KartuStat _statProduksi = new("Produksi 7 hari ke depan", Ikon.Petir, Tema.OranyeTua);
    private readonly KartuStat _statHemat = new("Penghematan 7 hari", Ikon.Uang, Tema.Biru);
    private readonly KartuStat _statCo2 = new("Reduksi emisi 7 hari", Ikon.Daun, Tema.Hijau);
    private readonly KartuStat _statPohon = new("Setara pohon", Ikon.Pohon, Tema.HijauTerang);

    private readonly GrafikKombinasi _grafik7 = new() { Height = Tema.S(280) };
    private readonly GrafikKombinasi _grafikJam = new() { Height = Tema.S(280) };
    private readonly GrafikKombinasi _grafikBulan = new() { Height = Tema.S(300) };

    private readonly HutanVirtual _hutan = new() { Height = Tema.S(130) };
    private readonly Label _lencana = Teks.Buat("", Tema.Font(14, semibold: true), Tema.Hijau);
    private readonly LabelBungkus _lencanaDesk = Teks.Bungkus("", Tema.Font(9.5f));
    private readonly BilahProgres _progres = new() { Warna = Tema.HijauTerang, Margin = new Padding(0, Tema.S(6), 0, Tema.S(4)) };
    private readonly Label _progresTeks = Teks.Redup("", 8.5f);
    private readonly Tumpukan _tingkat = new() { Margin = new Padding(0, Tema.S(12), 0, 0) };

    private readonly DataGridView _tabelRiwayat = Isian.Tabel();
    private readonly LabelBungkus _infoRiwayat = Teks.Bungkus("", Tema.Font(9));
    private readonly DataGridView _tabelHarian = Isian.Tabel();

    private readonly System.Windows.Forms.Timer _segarkan = new() { Interval = 10 * 60 * 1000 };
    private HasilEstimasi? _hasil;
    private HasilPrakiraan? _prakiraan;
    private int _versi;
    private bool _mengisiSumber;

    public DashboardHalaman(KonteksAplikasi k) : base(k)
    {
        _sumber.Tag = TandaLebarTetap.Ya;
        _sumber.Width = Tema.S(300);
        _sumber.AccessibleName = "Sumber data dashboard";
        var header = new Baris { Margin = new Padding(0, 0, 0, Tema.S(4)) };
        header.Tambah(Teks.Judul("Dashboard Dampak Lingkungan", 22), 1).Tambah(_sumber).Tambah(_btnMuat.Pas()).Tambah(_btnSnapshot.Pas());

        // real-time
        var rt = new Kartu { BackColor = Tema.KuningMuda, WarnaGaris = Color.Empty, Padding = Tema.P(22, 16, 22, 14) };
        var barisRt = new GridKartu { Kolom = 4, LebarMinKolom = Tema.S(150), Margin = new Padding(0, Tema.S(8), 0, Tema.S(4)) };
        barisRt.Controls.AddRange(
        [
            BlokRt("Radiasi matahari", _rtRadiasi, Ikon.Matahari, Tema.OranyeTua),
            BlokRt("Daya panel saat ini", _rtDaya, Ikon.Petir, Tema.Biru),
            BlokRt("Tutupan awan", _rtAwan, Ikon.Awan, Tema.TeksRedup),
            BlokRt("Suhu udara", _rtSuhu, Ikon.Termometer, Tema.Merah),
        ]);
        rt.Controls.AddRange([Teks.Buat("Kondisi saat ini di lokasi Anda", Tema.Font(10, semibold: true), Tema.Teks), barisRt, _rtWaktu]);

        var stat = new GridKartu { Kolom = 4, LebarMinKolom = Tema.S(210) };
        stat.Controls.AddRange([_statProduksi, _statHemat, _statCo2, _statPohon]);

        var k7 = new Kartu();
        k7.Controls.AddRange([Teks.Subjudul("Produksi harian vs radiasi matahari (prakiraan 7 hari)"), _grafik7]);
        _grafik7.FormatKiri = v => Tema.N(v, 1);
        _grafik7.JudulSumbuKiri = "kWh";
        _grafik7.JudulSumbuKanan = "kWh/m²";
        var kJam = new Kartu();
        kJam.Controls.AddRange([Teks.Subjudul("Kurva daya hari ini (per jam)"), _grafikJam]);
        _grafikJam.FormatKiri = v => Tema.N(v, 1);
        _grafikJam.JudulSumbuKiri = "kW";
        _grafikJam.FormatKanan = v => Tema.N(v, 0);
        _grafikJam.JudulSumbuKanan = "W/m²";
        var gridGrafik = new GridKartu { Kolom = 2, LebarMinKolom = Tema.S(420), BobotKolom = [3, 2] };
        gridGrafik.Controls.AddRange([k7, kJam]);

        var kBulan = new Kartu();
        kBulan.Controls.AddRange(
        [
            Teks.Subjudul("Komparasi biaya listrik bulanan"),
            Teks.Bungkus("Tagihan tanpa panel surya dibandingkan dengan tagihan setelah dikurangi produksi panel (berdasarkan iklim tahun lalu).", Tema.Font(9.5f)),
            _grafikBulan,
        ]);
        _grafikBulan.FormatKiri = Tema.RpRingkas;
        _grafikBulan.FormatTooltip = (_, v) => Tema.Rp(v);

        var kHutan = new Kartu { Padding = Tema.P(22) };
        kHutan.Controls.AddRange(
        [
            Teks.Subjudul("Hutan virtual Anda"),
            Teks.Bungkus("Setiap pohon mewakili 21,77 kg CO₂ yang diserap per tahun. Atap surya Anda bekerja seperti hutan ini.", Tema.Font(9.5f)),
            _hutan, _lencana, _lencanaDesk, _progres, _progresTeks, _tingkat,
        ]);

        var kRiwayat = new Kartu();
        _tabelRiwayat.Height = Tema.S(260);
        _tabelRiwayat.Columns.AddRange(
            Isian.Kolom("Waktu", nameof(BarisRiwayat.Waktu), kanan: false, bobot: 130),
            Isian.Kolom("Produksi/th (kWh)", nameof(BarisRiwayat.Produksi), "N0"),
            Isian.Kolom("Hemat/th", nameof(BarisRiwayat.Hemat), "N0"),
            Isian.Kolom("CO₂/th (kg)", nameof(BarisRiwayat.Co2), "N0"),
            Isian.Kolom("Pohon", nameof(BarisRiwayat.Pohon), "N1", bobot: 70));
        kRiwayat.Controls.AddRange([Teks.Subjudul("Riwayat snapshot"), _infoRiwayat, _tabelRiwayat]);

        var gridBawah = new GridKartu { Kolom = 2, LebarMinKolom = Tema.S(400) };
        gridBawah.Controls.AddRange([kHutan, kRiwayat]);

        var kTabel = new Kartu();
        _tabelHarian.Height = Tema.S(290);
        _tabelHarian.Columns.AddRange(
            Isian.Kolom("Tanggal", nameof(BarisHarian.Tanggal), kanan: false, bobot: 130),
            Isian.Kolom("Radiasi (MJ/m²)", nameof(BarisHarian.Radiasi), "N1"),
            Isian.Kolom("Jam puncak", nameof(BarisHarian.Psh), "N2"),
            Isian.Kolom("Penyinaran (jam)", nameof(BarisHarian.Sinar), "N1"),
            Isian.Kolom("Suhu (°C)", nameof(BarisHarian.Suhu), kanan: true),
            Isian.Kolom("Awan (%)", nameof(BarisHarian.Awan), "N0"),
            Isian.Kolom("Produksi (kWh)", nameof(BarisHarian.Produksi), "N2"),
            Isian.Kolom("Hemat (Rp)", nameof(BarisHarian.Hemat), "N0"),
            Isian.Kolom("CO₂ (kg)", nameof(BarisHarian.Co2), "N2"));
        kTabel.Controls.AddRange([Teks.Subjudul("Rincian harian (prakiraan Open-Meteo)"), _tabelHarian]);

        _gulir.Controls.AddRange([header, _subjudul, rt, stat, gridGrafik, kBulan, gridBawah, kTabel]);
        _subjudul.Margin = new Padding(0, 0, 0, Tema.S(16));
        Controls.Add(_gulir);

        _sumber.SelectedIndexChanged += async (_, _) => { if (!_mengisiSumber) await MuatAsync(false); };
        _btnMuat.Click += async (_, _) => await MuatAsync(true);
        _btnSnapshot.Click += async (_, _) => await SnapshotAsync();
        _segarkan.Tick += async (_, _) => { if (Visible) await MuatAsync(true); };
        K.InstalasiBerubah += async (_, _) => { if (Visible) await IsiSumberAsync(null); };
        K.Akun.Berubah += async (_, _) => { if (Visible) await IsiSumberAsync(null); };
    }

    public override string Judul => "Dashboard";

    public override async void Tampil(object? argumen)
    {
        _segarkan.Start();
        await IsiSumberAsync(argumen as Instalasi);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) _segarkan.Stop();
    }

    private static Tumpukan BlokRt(string judul, Label nilai, Action<Graphics, RectangleF, Color> ikon, Color warna)
    {
        var t = new Tumpukan();
        var atas = new Baris { Jarak = Tema.S(6) };
        atas.Tambah(new IkonBulat(ikon, warna, Color.White) { Size = new Size(Tema.S(26), Tema.S(26)), Margin = Padding.Empty })
            .Tambah(Teks.Redup(judul, 9));
        nilai.Margin = new Padding(0, Tema.S(4), 0, 0);
        t.Controls.AddRange([atas, nilai]);
        return t;
    }

    private async Task IsiSumberAsync(Instalasi? pilih)
    {
        var dipilihSebelumnya = (_sumber.SelectedItem as Sumber)?.Instalasi?.Id;
        List<Instalasi> daftar = [];
        try
        {
            daftar = await K.Akun.Repositori.DaftarAsync();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            K.Shell.Notifikasi("Gagal memuat Instalasi Saya: " + KonteksAplikasi.PesanGalat(e), galat: true);
        }

        _mengisiSumber = true;
        _sumber.Items.Clear();
        if (K.HasilTerakhir is not null)
            _sumber.Items.Add(new Sumber("Estimasi saat ini · " + (K.Lokasi?.Nama.Split(',')[0] ?? "lokasi"), null));
        foreach (var i in daftar) _sumber.Items.Add(new Sumber($"{i.Nama} · {Tema.N(i.KapasitasKwp, 1)} kWp", i));

        var target = pilih?.Id ?? dipilihSebelumnya;
        var idx = target is null ? 0 : _sumber.Items.Cast<Sumber>().ToList().FindIndex(s => s.Instalasi?.Id == target);
        _sumber.SelectedIndex = _sumber.Items.Count == 0 ? -1 : Math.Max(0, idx);
        _mengisiSumber = false;

        if (_sumber.Items.Count == 0)
        {
            _subjudul.Text = "Belum ada data. Cari alamat di Beranda atau tambahkan instalasi di \"Instalasi Saya\" terlebih dahulu.";
            _btnSnapshot.Enabled = false;
            _grafik7.Kosongkan("Belum ada instalasi");
            _grafikJam.Kosongkan("Belum ada instalasi");
            _grafikBulan.Kosongkan("Belum ada instalasi");
            return;
        }
        await MuatAsync(false);
    }

    private async Task MuatAsync(bool paksaSegar)
    {
        if (_sumber.SelectedItem is not Sumber s) return;
        var versi = ++_versi;
        _btnMuat.Enabled = false;
        _btnSnapshot.Enabled = s.Instalasi?.Id is not null;
        _subjudul.Text = "Memuat data Open-Meteo…";
        try
        {
            EstimasiMasukan m;
            string lokasi;
            if (s.Instalasi is { } inst)
            {
                m = KonteksAplikasi.MasukanDari(inst, K.Masukan);
                lokasi = string.IsNullOrWhiteSpace(inst.Alamat) ? $"{inst.Latitude:0.####}, {inst.Longitude:0.####}" : inst.Alamat;
            }
            else
            {
                m = K.Masukan with { JumlahPanel = K.HasilTerakhir!.Sistem.JumlahPanel };
                lokasi = K.Lokasi?.Nama ?? "";
            }

            var hasil = await K.Estimasi.HitungAsync(m);
            var prak = await K.Estimasi.PrakiraanAsync(m.Latitude, m.Longitude, hasil.Sistem.KapasitasKwp, m.TarifPerKwh, m.FaktorEmisi, paksaSegar);
            if (versi != _versi) return;
            _hasil = hasil;
            _prakiraan = prak;
            _subjudul.Text = $"{lokasi} · sistem {Tema.N(hasil.Sistem.KapasitasKwp, 1)} kWp ({hasil.Sistem.JumlahPanel} panel) · " +
                             $"tarif {Tema.Rp(m.TarifPerKwh)}/kWh · estimasi dari data Open-Meteo, bukan pengukuran sensor";
            Render();
            await MuatRiwayatAsync(s.Instalasi);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (versi != _versi) return;
            _subjudul.Text = KonteksAplikasi.PesanGalat(e);
        }
        finally
        {
            if (versi == _versi) _btnMuat.Enabled = true;
        }
    }

    private void Render()
    {
        if (_hasil is not { } h || _prakiraan is not { } p) return;

        if (p.SaatIni is { } n)
        {
            _rtRadiasi.Text = $"{Tema.N(n.RadiasiWm2)} W/m²";
            _rtDaya.Text = $"{Tema.N(n.DayaKw, 2)} kW";
            _rtAwan.Text = $"{Tema.N(n.TutupanAwan)}%";
            _rtSuhu.Text = $"{Tema.N(n.Suhu, 1)} °C";
            _rtWaktu.Text = $"Diperbarui {n.Waktu:dddd, d MMM yyyy HH:mm} ({p.ZonaWaktu}) · {(n.Siang ? "siang hari" : "malam hari — panel tidak berproduksi")} · " +
                            $"otomatis disegarkan tiap 10 menit";
        }

        var hemat7 = p.TotalHematRp;
        _statProduksi.Atur($"{Tema.N(p.TotalProduksiKwh, 1)} kWh", $"rata-rata {Tema.N(p.TotalProduksiKwh / Math.Max(1, p.Harian.Count), 1)} kWh/hari");
        _statHemat.Atur(Tema.Rp(hemat7), $"tarif {Tema.Rp(h.Masukan.TarifPerKwh)}/kWh · ±{Tema.RpRingkas(h.Finansial.HematTahunPertama / 12)}/bulan");
        _statCo2.Atur($"{Tema.N(p.TotalCo2Kg, 1)} kg CO₂", $"faktor emisi grid {Tema.N(h.Lingkungan.FaktorEmisi, 3)} kg/kWh");
        _statPohon.Atur($"{Tema.N(h.Lingkungan.SetaraPohon, 1)} pohon", "penyerapan CO₂ setahun penuh");

        var hari = p.Harian.Select(d => d.Tanggal.ToString("ddd d/M", Tema.Id)).ToArray();
        _grafik7.IndeksPenanda = 0;
        _grafik7.Atur(hari,
            new Seri("Produksi (kWh)", p.Harian.Select(d => d.ProduksiKwh).ToArray(), Tema.Kuning),
            new Seri("Radiasi (kWh/m²)", p.Harian.Select(d => d.Psh).ToArray(), Tema.OranyeTua, JenisSeri.Garis, SumbuKanan: true));

        var jam = p.HariIni.Select(j => j.Waktu.ToString("HH", Tema.Id)).ToArray();
        _grafikJam.IndeksPenanda = p.SaatIni is { } si ? p.HariIni.ToList().FindIndex(j => j.Waktu.Hour == si.Waktu.Hour) : -1;
        _grafikJam.Atur(jam,
            new Seri("Daya (kW)", p.HariIni.Select(j => j.DayaKw).ToArray(), Tema.Biru, JenisSeri.Area),
            new Seri("Radiasi (W/m²)", p.HariIni.Select(j => j.RadiasiWm2).ToArray(), Tema.Kuning, JenisSeri.Garis, SumbuKanan: true) { PutusPutus = true });

        _grafikBulan.IndeksPenanda = DateTime.Now.Month - 1;
        _grafikBulan.Atur(Tema.NamaBulanPendek,
            new Seri("Tanpa panel surya", h.Bulanan.Select(b => b.TagihanTanpaSuryaRp).ToArray(), Tema.Hex("#BDC1C6")),
            new Seri("Dengan panel surya", h.Bulanan.Select(b => b.TagihanDenganSuryaRp).ToArray(), Tema.Biru),
            new Seri("Penghematan", h.Bulanan.Select(b => b.HematRp).ToArray(), Tema.Hijau, JenisSeri.Garis));

        // gamifikasi
        var l = h.Lingkungan;
        _hutan.JumlahPohon = l.SetaraPohon;
        _lencana.Text = $"Level {l.Lencana.Level + 1}: {l.Lencana.Nama}";
        _lencanaDesk.Text = $"{l.Lencana.Deskripsi} Sistem ini mengurangi ±{Tema.N(l.Co2TahunanKg)} kg CO₂ per tahun " +
                            $"atau ±{Tema.N(l.Co2UmurSistemTon, 1)} ton selama 20 tahun.";
        _progres.Nilai = l.Lencana.ProgresPersen;
        _progresTeks.Text = l.Lencana.AmbangBerikutnya is { } b
            ? $"{Tema.N(l.SetaraPohon, 1)} / {Tema.N(b)} pohon menuju level berikutnya ({Tema.N(b - l.SetaraPohon, 1)} pohon lagi — tambah panel untuk naik level)"
            : "Level tertinggi tercapai!";
        _tingkat.Controls.Clear();
        for (var i = 0; i < KalkulatorKarbon.Tingkat.Count; i++)
        {
            var t = KalkulatorKarbon.Tingkat[i];
            var tercapai = i <= l.Lencana.Level;
            var label = Teks.Buat($"{(tercapai ? "✓" : "○")}  {t.Nama} — {Tema.N(t.Ambang)}+ pohon", Tema.Font(9.5f, semibold: i == l.Lencana.Level),
                tercapai ? Tema.Hijau : Tema.TeksPudar);
            label.Margin = new Padding(0, 0, 0, Tema.S(2));
            _tingkat.Controls.Add(label);
        }

        _tabelHarian.DataSource = p.Harian.Select(d => new BarisHarian(d.Tanggal.ToString("dddd, d MMM", Tema.Id), d.RadiasiMj, d.Psh,
            d.JamSinarMatahari, $"{Tema.N(d.SuhuMin, 0)}–{Tema.N(d.SuhuMaks, 0)}", d.TutupanAwan, d.ProduksiKwh, d.HematRp, d.Co2Kg)).ToList();
        _gulir.PerformLayout();
    }

    private async Task MuatRiwayatAsync(Instalasi? inst)
    {
        if (inst?.Id is not { } id)
        {
            _infoRiwayat.Text = "Snapshot hanya tersedia untuk instalasi yang sudah disimpan. Simpan estimasi ini ke \"Instalasi Saya\" lalu pilih di daftar atas.";
            _tabelRiwayat.DataSource = null;
            return;
        }
        var riwayat = await K.Akun.Repositori.RiwayatAsync(id);
        _infoRiwayat.Text = riwayat.Count == 0
            ? "Belum ada snapshot. Klik \"Simpan snapshot\" untuk merekam hasil estimasi hari ini."
            : $"{riwayat.Count} snapshot tersimpan di {(K.Akun.Repositori.Nama == "Supabase" ? "Supabase (tabel riwayat_estimasi)" : "komputer ini")}.";
        _tabelRiwayat.DataSource = riwayat.Select(r => new BarisRiwayat(r.DibuatPada.ToString("d MMM yyyy HH:mm", Tema.Id),
            r.ProduksiTahunanKwh, r.HematTahunanRp, r.Co2TahunanKg, r.SetaraPohon)).ToList();
    }

    private async Task SnapshotAsync()
    {
        if (_sumber.SelectedItem is not Sumber { Instalasi.Id: { } id } s || _hasil is not { } h || _prakiraan is not { } p) return;
        _btnSnapshot.Enabled = false;
        await Aman(async () =>
        {
            await K.Akun.Repositori.TambahRiwayatAsync(new RiwayatEstimasi
            {
                InstalasiId = id,
                PshRataHarian = h.Iklim.PshRataHarian,
                ProduksiTahunanKwh = h.Sistem.ProduksiTahunanKwh,
                HematTahunanRp = h.Finansial.HematTahunPertama,
                Co2TahunanKg = h.Lingkungan.Co2TahunanKg,
                SetaraPohon = h.Lingkungan.SetaraPohon,
                Produksi7HariKwh = p.TotalProduksiKwh,
                Hemat7HariRp = p.TotalHematRp,
            });
            await MuatRiwayatAsync(s.Instalasi);
        }, "Snapshot estimasi tersimpan.");
        _btnSnapshot.Enabled = true;
    }

    private sealed record BarisHarian(string Tanggal, double Radiasi, double Psh, double Sinar, string Suhu, double Awan, double Produksi, double Hemat, double Co2);
    private sealed record BarisRiwayat(string Waktu, double Produksi, double Hemat, double Co2, double Pohon);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _segarkan.Dispose();
        base.Dispose(disposing);
    }
}
