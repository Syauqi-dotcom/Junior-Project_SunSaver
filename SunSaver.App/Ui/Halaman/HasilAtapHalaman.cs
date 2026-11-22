using SunSaver.App.Core.Estimasi;
using SunSaver.App.Core.Kalkulasi;
using SunSaver.App.Core.Model;
using SunSaver.App.Ui.Grafik;
using SunSaver.App.Ui.Kontrol;
using SunSaver.App.Ui.Peta;

namespace SunSaver.App.Ui.Halaman;

/// <summary>
/// Halaman hasil alamat (padanan halaman "Your roof" Project Sunroof):
/// peta satelit di kiri untuk menggambar atap, panel ringkasan potensi di kanan,
/// dan isian "sesuaikan informasi Anda" yang langsung memperbarui estimasi.
/// </summary>
public sealed class HasilAtapHalaman : HalamanDasar
{
    private readonly PetaSatelit _peta = new() { Dock = DockStyle.Fill };
    private readonly TombolBulat _btnGambar = new("Gambar atap", GayaTombol.Utama);
    private readonly TombolBulat _btnSelesai = new("Selesai", GayaTombol.Utama) { Visible = false };
    private readonly TombolBulat _btnHapus = new("Hapus gambar", GayaTombol.Teks);
    private readonly TombolBulat _btnLapisan = new("Peta jalan", GayaTombol.Sekunder);
    private readonly TextBox _cariLain = Isian.Teks("Cari alamat lain…", 200);

    private readonly LabelBungkus _alamat = Teks.Bungkus("", Tema.Font(15), Tema.Teks);
    private readonly Label _koordinat = Teks.Redup("");
    private readonly BarisStat _statJam = new(Ikon.Matahari, Tema.OranyeTua, Tema.KuningMuda);
    private readonly BarisStat _statLuas = new(Ikon.Atap, Tema.Biru, Tema.BiruMuda);
    private readonly BarisStat _statHemat = new(Ikon.Uang, Tema.Hijau, Tema.HijauMuda);
    private readonly LabelBungkus _status = Teks.Bungkus("", Tema.Font(9.5f), Tema.TeksRedup);
    private readonly Label _nilaiTagihan = Teks.Buat("", Tema.Font(12, semibold: true), Tema.Teks);
    private readonly Penggeser _tagihan = new() { Minimum = 50, Maksimum = 5000, Langkah = 25 };
    private readonly ComboBox _tarif = Isian.Pilihan(DataReferensi.Tarif);
    private readonly NumericUpDown _luas = Isian.Angka(0, 5000, 0, 5, 0);
    private readonly TombolBulat _btnHemat = new("Lihat penghematan saya");
    private readonly TombolBulat _btnSimpan = new("Simpan ke Instalasi Saya", GayaTombol.Sekunder);
    private readonly GrafikKombinasi _grafik = new() { Height = Tema.S(210) };
    private readonly LabelBungkus _catatan = Teks.Bungkus("", Tema.Font(9), Tema.OranyeTua);
    private readonly System.Windows.Forms.Timer _tunda = new() { Interval = 200 };

    private RingkasanIklim? _iklim;
    private bool _mengisi;
    private int _versiMuat;

    public HasilAtapHalaman(KonteksAplikasi k) : base(k)
    {
        // ---------------- kiri: toolbar + peta ----------------
        var toolbar = new Baris { Dock = DockStyle.Top, Height = Tema.S(60), Padding = Tema.P(16, 10, 16, 10), Jarak = Tema.S(8), BackColor = Color.White };
        _btnGambar.Ikon = Ikon.Pensil; _btnGambar.Pas();
        _btnLapisan.Ikon = Ikon.Lapisan; _btnLapisan.Pas();
        _cariLain.MaximumSize = new Size(Tema.S(360), 0);
        _cariLain.AccessibleName = "Cari alamat lain";
        var kembali = new TombolBulat("← Beranda", GayaTombol.Teks);
        kembali.Click += (_, _) => K.Shell.Ke(HalamanId.Beranda);
        toolbar.Tambah(kembali).Tambah(_cariLain, 1).Tambah(_btnGambar).Tambah(_btnSelesai).Tambah(_btnHapus).Tambah(_btnLapisan);

        var garisBawah = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Tema.Garis };
        var kiri = new Panel { Dock = DockStyle.Fill };
        kiri.Controls.Add(_peta);
        kiri.Controls.Add(garisBawah);
        kiri.Controls.Add(toolbar);

        // ---------------- kanan: ringkasan + penyesuaian ----------------
        var kanan = new PanelGulir { Dock = DockStyle.Right, Width = Tema.S(440), LebarMaks = int.MaxValue, Padding = Tema.P(24, 20, 24, 28), BackColor = Color.White };
        var garisKiri = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Tema.Garis };

        _alamat.Margin = new Padding(0, 0, 0, Tema.S(2));
        _koordinat.Margin = new Padding(0, 0, 0, Tema.S(16));

        var kartuAtap = new Kartu { Padding = Tema.P(20, 18, 20, 4) };
        kartuAtap.Controls.Add(Teks.Buat("Atap Anda", Tema.Font(10, semibold: true), Tema.TeksRedup));
        kartuAtap.Controls[0].Margin = new Padding(0, 0, 0, Tema.S(14));
        kartuAtap.Controls.AddRange([_statJam, _statLuas, _statHemat]);

        var barisTagihan = new Baris { Margin = new Padding(0, Tema.S(4), 0, 0) };
        barisTagihan.Tambah(Teks.Buat("Rata-rata tagihan listrik per bulan", Tema.Font(9.5f, semibold: true), Tema.Teks), 1).Tambah(_nilaiTagihan);
        _tagihan.AccessibleName = "Rata-rata tagihan listrik per bulan, ribu rupiah";
        _tagihan.Margin = new Padding(0, Tema.S(2), 0, Tema.S(14));

        var tombol = new Tumpukan { Margin = new Padding(0, Tema.S(6), 0, Tema.S(18)) };
        _btnHemat.Pas(Tema.S(240));
        _btnSimpan.Pas(Tema.S(240));
        _btnSimpan.Margin = new Padding(0, Tema.S(8), 0, 0);
        tombol.Controls.AddRange([_btnHemat, _btnSimpan]);

        var kartuGrafik = new Kartu { Padding = Tema.P(16) };
        kartuGrafik.Controls.Add(Teks.Buat("Jam matahari puncak per hari, rata-rata bulanan", Tema.Font(9.5f, semibold: true), Tema.Teks));
        kartuGrafik.Controls.Add(_grafik);
        _grafik.FormatKiri = v => Tema.N(v, 1);
        _grafik.FormatKanan = v => Tema.N(v, 0) + "%";

        kanan.Controls.AddRange(
        [
            _alamat, _koordinat, kartuAtap, _status, new Pemisah(),
            Teks.Judul("Sesuaikan informasi Anda", 13),
            Teks.Bungkus("Atur tagihan listrik dan golongan tarif untuk mendapatkan rekomendasi ukuran sistem dan penghematan.", Tema.Font(9.5f)),
            barisTagihan, _tagihan,
            Isian.Bidang("Golongan tarif PLN", _tarif),
            Isian.Bidang("Luas atap tersedia", _luas, "m²", bantuan: "Terisi otomatis dari gambar atap. Isi 0 bila belum tahu."),
            _catatan, tombol, kartuGrafik,
        ]);

        Controls.Add(kiri);
        Controls.Add(garisKiri);
        Controls.Add(kanan);

        // ---------------- perilaku ----------------
        _btnGambar.Click += (_, _) => _peta.MulaiGambar();
        _btnSelesai.Click += (_, _) => _peta.SelesaiGambar();
        _btnHapus.Click += (_, _) => _peta.HapusPoligon();
        _btnLapisan.Click += (_, _) =>
        {
            _peta.Lapisan = _peta.Lapisan == LapisanPeta.Satelit ? LapisanPeta.Jalan : LapisanPeta.Satelit;
            _btnLapisan.Text = _peta.Lapisan == LapisanPeta.Satelit ? "Peta jalan" : "Satelit";
            _btnLapisan.Pas();
            toolbar.PerformLayout();
        };
        _peta.ModeGambarBerubah += (_, _) => PerbaruiTombolGambar();
        _peta.PoligonBerubah += (_, _) => PoligonBerubah();
        _peta.LokasiDipilih += async (_, t) => await PilihTitikAsync(t);
        _cariLain.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            await CariLainAsync();
        };

        _tagihan.NilaiBerubah += (_, _) => { PerbaruiLabelTagihan(); Tunda(); };
        _tarif.SelectedIndexChanged += (_, _) => Tunda();
        _luas.ValueChanged += (_, _) => Tunda();
        _tunda.Tick += (_, _) => { _tunda.Stop(); Hitung(); };
        _btnHemat.Click += (_, _) => { Hitung(); if (K.HasilTerakhir is not null) K.Shell.Ke(HalamanId.Estimasi); };
        _btnSimpan.Click += async (_, _) => await SimpanAsync();
    }

    public override string Judul => "Atap Anda";

    public override void Tampil(object? argumen)
    {
        if (argumen is HasilLokasi h)
        {
            var lokasiBaru = K.Lokasi is null || Math.Abs(K.Lokasi.Lat - h.Lat) > 1e-6 || Math.Abs(K.Lokasi.Lon - h.Lon) > 1e-6;
            K.Lokasi = h;
            if (lokasiBaru)
            {
                K.PoligonAtap = null;
                K.Masukan = K.Masukan with { Latitude = h.Lat, Longitude = h.Lon, LuasAtapM2 = null, JumlahPanel = null };
                _iklim = null;
            }
        }
        if (K.Lokasi is not { } lokasi)
        {
            K.Shell.Ke(HalamanId.Beranda);
            return;
        }

        _alamat.Text = lokasi.Nama;
        _koordinat.Text = $"{lokasi.Lat.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture)}, " +
                          $"{lokasi.Lon.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture)}";

        // Isi form dulu: AturPoligon memicu PoligonBerubah, yang tidak boleh membaca isian kosong.
        IsiForm();
        _mengisi = true;
        _peta.Penanda = new TitikGeo(lokasi.Lat, lokasi.Lon);
        _peta.AturPoligon(K.PoligonAtap);
        _peta.Pusatkan(new TitikGeo(lokasi.Lat, lokasi.Lon), 19);
        _peta.Petunjuk = K.PoligonAtap is null ? "Klik \"Gambar atap\" untuk menandai atap rumah Anda" : null;
        _mengisi = false;
        PerbaruiTombolGambar();

        if (_iklim is null) _ = MuatIklimAsync();
        else Hitung();
    }

    private void IsiForm()
    {
        _mengisi = true;
        var m = K.Masukan;
        _tagihan.AturDiam((int)Math.Round(m.TagihanBulananRp / 1000));
        _tarif.SelectedItem = DataReferensi.CariTarif(m.KodeTarif) ?? DataReferensi.Tarif[3];
        _luas.Value = (decimal)Math.Clamp(Math.Round(m.LuasAtapM2 ?? 0), 0, 5000);
        PerbaruiLabelTagihan();
        _mengisi = false;
    }

    private void PerbaruiLabelTagihan() => _nilaiTagihan.Text = Tema.Rp(_tagihan.Nilai * 1000.0);

    private void Tunda()
    {
        if (_mengisi) return;
        _tunda.Stop();
        _tunda.Start();
    }

    private async Task MuatIklimAsync()
    {
        if (K.Lokasi is not { } lokasi) return;
        var versi = ++_versiMuat;
        _statJam.Atur("…", "jam sinar matahari efektif per tahun", "Mengambil data cuaca harian dari Open-Meteo…");
        _statLuas.Atur("…", "", "");
        _statHemat.Atur("…", "", "");
        _grafik.Kosongkan("Memuat data cuaca…");
        _status.Text = "";
        try
        {
            var r = await K.Iklim.RingkasanTahunanAsync(lokasi.Lat, lokasi.Lon);
            if (versi != _versiMuat) return;
            _iklim = r;
            Hitung();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (versi != _versiMuat) return;
            _status.ForeColor = Tema.Merah;
            _status.Text = KonteksAplikasi.PesanGalat(e) + " Klik di sini untuk mencoba lagi.";
            _status.Cursor = Cursors.Hand;
            _status.Click -= CobaLagi;
            _status.Click += CobaLagi;
            _grafik.Kosongkan("Data cuaca belum tersedia");
        }
    }

    private void CobaLagi(object? s, EventArgs e)
    {
        _status.Click -= CobaLagi;
        _status.Cursor = Cursors.Default;
        _ = MuatIklimAsync();
    }

    private void PerbaruiTombolGambar()
    {
        _btnSelesai.Visible = _peta.ModeGambar;
        _btnHapus.Visible = _peta.Poligon.Count > 0;
        _btnGambar.Text = _peta.ModeGambar ? "Mulai ulang" : _peta.Poligon.Count >= 3 ? "Gambar ulang" : "Gambar atap";
        _btnGambar.Gaya = _peta.ModeGambar ? GayaTombol.Sekunder : GayaTombol.Utama;
        _btnGambar.Pas();
        _btnGambar.Parent?.PerformLayout();
    }

    private void PoligonBerubah()
    {
        PerbaruiTombolGambar();
        if (_mengisi || _peta.ModeGambar) return;
        _peta.Petunjuk = null;
        if (_peta.Poligon.Count >= 3)
        {
            K.PoligonAtap = _peta.Poligon.ToList();
            _mengisi = true;
            _luas.Value = (decimal)Math.Clamp(Math.Round(_peta.LuasPoligonM2), 1, 5000);
            _mengisi = false;
        }
        else
        {
            K.PoligonAtap = null;
        }
        Hitung();
    }

    /// <summary>Baca isian → hitung ulang estimasi (lokal, tanpa jaringan karena data iklim sudah ada).</summary>
    private void Hitung()
    {
        var tarif = _tarif.SelectedItem as TarifListrik ?? DataReferensi.Tarif[3];
        K.Masukan = K.Masukan with
        {
            TagihanBulananRp = _tagihan.Nilai * 1000.0,
            KodeTarif = tarif.Kode,
            TarifPerKwh = tarif.TarifPerKwh,
            LuasAtapM2 = _luas.Value > 0 ? (double)_luas.Value : null,
            JumlahPanel = null, // halaman ini selalu memakai rekomendasi; diatur manual di halaman Estimasi
        };
        if (_iklim is null) return;

        var h = LayananEstimasi.Hitung(K.Masukan, _iklim);
        K.HasilTerakhir = h;
        var i = h.Iklim;

        _status.ForeColor = Tema.TeksRedup;
        _status.Text = "";
        _statJam.Atur($"{Tema.N(i.JamMatahariPuncakTahunan)} jam", "sinar matahari efektif per tahun",
            $"Rata-rata {Tema.N(i.PshRataHarian, 1)} jam/hari setara 1.000 W/m² · berdasarkan analisis cuaca harian Open-Meteo {i.Tahun} " +
            $"(durasi penyinaran {Tema.N(i.JamSinarMatahariTahunan)} jam)");

        if (h.Atap.LuasAtapM2 is { } luas)
            _statLuas.Atur($"{Tema.N(luas)} m²", "tersedia untuk panel surya",
                $"Muat hingga {h.Atap.PanelMaks} panel {Tema.N(h.Masukan.DayaPanelWp)} Wp " +
                $"({Tema.N(KalkulatorEnergi.KwpDariPanel(h.Atap.PanelMaks ?? 0, new SpesifikasiPanel(h.Masukan.DayaPanelWp, h.Masukan.EfisiensiPersen)), 1)} kWp)" +
                (K.PoligonAtap is not null ? " · dari gambar atap Anda" : " · isian manual"));
        else
            _statLuas.Atur("Gambar atap", "untuk menghitung luas tersedia",
                $"Klik \"Gambar atap\" lalu tandai sudut-sudut atap di peta. Sementara, sistem {Tema.N(h.Sistem.KapasitasKwp, 1)} kWp butuh ±{Tema.N(h.Atap.LuasDibutuhkanM2)} m².");

        var tunai = h.Finansial.Tunai;
        if (tunai.HematBersih > 0)
            _statHemat.Atur(Tema.RpRingkas(tunai.HematBersih), "estimasi hemat bersih 20 tahun",
                $"Sistem {Tema.N(h.Sistem.KapasitasKwp, 1)} kWp ({h.Sistem.JumlahPanel} panel) · hemat ±{Tema.RpRingkas(h.Finansial.HematTahunPertama / 12)}/bulan · " +
                $"balik modal ±{Tema.Tahun(tunai.BalikModalTahun)}");
        else
            _statHemat.Atur("Belum untung", "dalam 20 tahun dengan asumsi saat ini",
                "Tagihan listrik terlalu kecil dibanding biaya sistem. Coba naikkan tagihan atau ubah asumsi biaya di halaman penghematan.");

        _catatan.Text = string.Join(Environment.NewLine, h.Catatan.Select(c => "• " + c));
        _catatan.Visible = h.Catatan.Count > 0;

        _grafik.IndeksPenanda = DateTime.Now.Month - 1;
        _grafik.Atur(Tema.NamaBulanPendek,
            new Seri("Jam matahari puncak", i.Bulanan.Select(b => b.PshRata).ToArray(), Tema.Kuning),
            new Seri("Tutupan awan", i.Bulanan.Select(b => b.TutupanAwanRata).ToArray(), Tema.TeksPudar, JenisSeri.Garis, SumbuKanan: true) { PutusPutus = true });
    }

    private async Task PilihTitikAsync(TitikGeo t)
    {
        await Aman(async () =>
        {
            var h = await K.Geocoder.BalikAsync(t.Lat, t.Lon)
                    ?? new HasilLokasi($"Titik {t.Lat:0.#####}, {t.Lon:0.#####}", t.Lat, t.Lon, "koordinat");
            Tampil(h with { Lat = t.Lat, Lon = t.Lon });
        });
    }

    private async Task CariLainAsync()
    {
        var q = _cariLain.Text.Trim();
        if (q.Length < 3) return;
        _cariLain.Enabled = false;
        await Aman(async () =>
        {
            var hasil = await K.Geocoder.CariAsync(q);
            if (hasil.Count == 0) K.Shell.Notifikasi("Alamat tidak ditemukan.", galat: true);
            else
            {
                if (hasil.Count > 1)
                {
                    var menu = new ContextMenuStrip { Font = Tema.Font(10), ShowImageMargin = false };
                    foreach (var x in hasil)
                        menu.Items.Add(x.Nama.Length > 95 ? x.Nama[..92] + "…" : x.Nama, null, (_, _) => { _cariLain.Clear(); Tampil(x); });
                    menu.Show(_cariLain, new Point(0, _cariLain.Height));
                }
                else
                {
                    _cariLain.Clear();
                    Tampil(hasil[0]);
                }
            }
        });
        _cariLain.Enabled = true;
    }

    private async Task SimpanAsync()
    {
        Hitung();
        if (K.HasilTerakhir is null)
        {
            K.Shell.Notifikasi("Tunggu data cuaca selesai dimuat.", galat: true);
            return;
        }
        _btnSimpan.Enabled = false;
        await Aman(async () =>
        {
            var repo = K.Akun.Repositori;
            await repo.SimpanAsync(K.InstalasiDariSesi());
            K.UmumkanInstalasiBerubah();
            K.Shell.Notifikasi(repo.Nama == "Supabase"
                ? "Tersimpan ke Instalasi Saya (cloud Supabase)."
                : "Tersimpan ke Instalasi Saya di komputer ini. Masuk akun untuk menyimpan ke cloud.");
        });
        _btnSimpan.Enabled = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tunda.Dispose();
        base.Dispose(disposing);
    }
}
