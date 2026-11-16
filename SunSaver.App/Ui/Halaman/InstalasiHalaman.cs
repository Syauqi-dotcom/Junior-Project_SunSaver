using SunSaver.App.Core.Estimasi;
using SunSaver.App.Core.Kalkulasi;
using SunSaver.App.Core.Model;
using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App.Ui.Halaman;

/// <summary>
/// "Instalasi Saya": daftar instalasi milik pengguna (Supabase saat masuk, lokal saat tidak)
/// dengan form tambah/ubah/hapus, validasi, dan pratinjau estimasi yang diperbarui otomatis.
/// </summary>
public sealed class InstalasiHalaman : HalamanDasar
{
    private readonly ListBox _daftar = new()
    {
        Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, BorderStyle = BorderStyle.None,
        IntegralHeight = false, Font = Tema.Font(10), AccessibleName = "Daftar instalasi",
    };
    private readonly LabelBungkus _mode = Teks.Bungkus("", Tema.Font(9));
    private readonly Kartu _kartuMode = new() { Padding = Tema.P(14, 10, 14, 10), Margin = new Padding(0, 0, 0, Tema.S(12)) };
    private readonly TombolBulat _btnMasuk = new("Masuk untuk sinkron cloud", GayaTombol.Teks);
    private readonly LabelBungkus _kosong = Teks.Bungkus("Belum ada instalasi. Klik \"Instalasi baru\" atau simpan hasil pencarian atap dari Beranda.", Tema.Font(9.5f));

    private readonly PanelGulir _form = new() { LebarMaks = Tema.S(980) };
    private readonly Label _judulForm = Teks.Judul("Instalasi baru", 20);
    private readonly TextBox _nama = Isian.Teks("mis. Rumah Kaliurang", 80);
    private readonly TextBox _alamat = Isian.Teks("Alamat lengkap", 300);
    private readonly TombolBulat _btnCariKoordinat = new("Cari koordinat dari alamat", GayaTombol.Teks);
    private readonly NumericUpDown _lat = Isian.Angka(-90, 90, 5, 0.0001m, -7.7956m);
    private readonly NumericUpDown _lon = Isian.Angka(-180, 180, 5, 0.0001m, 110.3695m);
    private readonly NumericUpDown _jumlah = Isian.Angka(1, 300, 0, 1, 8);
    private readonly NumericUpDown _daya = Isian.Angka(100, 1000, 0, 10, 400);
    private readonly Label _kapasitas = Teks.Buat("", Tema.Font(12, semibold: true), Tema.Teks);
    private readonly Penggeser _efisiensi = new() { Minimum = 10, Maksimum = 25 };
    private readonly Label _nilaiEfisiensi = Teks.Buat("", Tema.Font(10, semibold: true), Tema.Teks);
    private readonly NumericUpDown _luas = Isian.Angka(0, 5000, 1, 1, 0);
    private readonly ComboBox _golongan = Isian.Pilihan(DataReferensi.Tarif);
    private readonly NumericUpDown _tarif = Isian.Angka(100, 10000, 2, 1, 1444.70m);
    private readonly NumericUpDown _tagihan = Isian.Angka(0, 100_000_000, 0, 50_000, 500_000);
    private readonly Label _galatNama = Isian.Galat();

    private readonly Label _pHarian = Teks.Angka("—", 18);
    private readonly Label _pHemat = Teks.Angka("—", 18);
    private readonly Label _pCo2 = Teks.Angka("—", 18);
    private readonly Label _pPohon = Teks.Angka("—", 18);
    private readonly LabelBungkus _pInfo = Teks.Bungkus("", Tema.Font(8.5f));

    private readonly TombolBulat _btnSimpan = new("Simpan");
    private readonly TombolBulat _btnBatal = new("Batalkan perubahan", GayaTombol.Teks);
    private readonly TombolBulat _btnHapus = new("Hapus", GayaTombol.Teks);
    private readonly TombolBulat _btnDashboard = new("Buka di Dashboard", GayaTombol.Sekunder);
    private readonly TombolBulat _btnPeta = new("Lihat di peta", GayaTombol.Sekunder);
    private readonly ErrorProvider _galat = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private readonly System.Windows.Forms.Timer _tunda = new() { Interval = 400 };

    private List<Instalasi> _data = [];
    private Instalasi _sedangDiedit = new();
    private bool _mengisi, _kotor;
    private int _indeksTerakhir = -1;
    private int _versiPratinjau;

    public InstalasiHalaman(KonteksAplikasi k) : base(k)
    {
        // ---------------- kiri: daftar ----------------
        var kiri = new Panel { Dock = DockStyle.Left, Width = Tema.S(360), BackColor = Tema.LatarAbu, Padding = Tema.P(20, 20, 12, 12) };
        var atasKiri = new Tumpukan { Dock = DockStyle.Top };
        var baru = new TombolBulat("+  Instalasi baru") { Margin = new Padding(0, 0, 0, Tema.S(12)) };
        baru.Click += (_, _) => { if (BolehTinggalkan()) MulaiBaru(); };
        _btnMasuk.Margin = new Padding(-Tema.S(12), Tema.S(2), 0, 0);
        _btnMasuk.Click += async (_, _) => await K.Shell.PastikanMasukAsync("menyimpan instalasi ke cloud");
        _kartuMode.Controls.AddRange([_mode, _btnMasuk]);
        atasKiri.Controls.AddRange([Teks.Judul("Instalasi Saya", 18), _kartuMode, baru.Pas(), _kosong]);
        _daftar.BackColor = Tema.LatarAbu;
        _daftar.ItemHeight = Tema.S(76);
        _daftar.DrawItem += GambarItem;
        _daftar.SelectedIndexChanged += (_, _) => PilihDariDaftar();
        kiri.Controls.Add(_daftar);
        kiri.Controls.Add(atasKiri);

        // ---------------- kanan: form ----------------
        var kLokasi = new Kartu();
        var gridKoordinat = new GridKartu { Kolom = 2, LebarMinKolom = Tema.S(180), Jarak = Tema.S(16), Margin = Padding.Empty };
        gridKoordinat.Controls.AddRange([Isian.Bidang("Latitude", _lat), Isian.Bidang("Longitude", _lon)]);
        _btnCariKoordinat.Margin = new Padding(-Tema.S(12), 0, 0, Tema.S(10));
        kLokasi.Controls.AddRange(
        [
            Teks.Subjudul("Lokasi"),
            Isian.Bidang("Nama instalasi", _nama, galat: _galatNama),
            Isian.Bidang("Alamat", _alamat),
            _btnCariKoordinat.Pas(),
            gridKoordinat,
            Teks.Bungkus("Koordinat menentukan data radiasi, suhu, dan tutupan awan dari Open-Meteo.", Tema.Font(8.5f), Tema.TeksPudar),
        ]);

        var kPanel = new Kartu();
        var gridPanel = new GridKartu { Kolom = 2, LebarMinKolom = Tema.S(180), Jarak = Tema.S(16), Margin = Padding.Empty };
        gridPanel.Controls.AddRange([Isian.Bidang("Jumlah panel", _jumlah, "unit"), Isian.Bidang("Daya per panel", _daya, "Wp")]);
        var barisKap = new Baris { Margin = new Padding(0, 0, 0, Tema.S(12)) };
        barisKap.Tambah(Teks.Redup("Kapasitas total", 10), 1).Tambah(_kapasitas);
        var barisEf = new Baris();
        barisEf.Tambah(Teks.Buat("Efisiensi panel", Tema.Font(9.5f, semibold: true), Tema.Teks), 1).Tambah(_nilaiEfisiensi);
        _efisiensi.AccessibleName = "Efisiensi panel, persen";
        _efisiensi.Margin = new Padding(0, 0, 0, Tema.S(12));
        kPanel.Controls.AddRange(
        [
            Teks.Subjudul("Spesifikasi panel"), gridPanel, barisKap, barisEf, _efisiensi,
            Isian.Bidang("Luas atap tersedia", _luas, "m²", bantuan: "Opsional. Isi 0 bila belum diukur."),
        ]);

        var kTarif = new Kartu();
        var gridTarif = new GridKartu { Kolom = 2, LebarMinKolom = Tema.S(180), Jarak = Tema.S(16), Margin = Padding.Empty };
        gridTarif.Controls.AddRange([Isian.Bidang("Golongan tarif", _golongan), Isian.Bidang("Harga per kWh", _tarif, "Rp")]);
        kTarif.Controls.AddRange([Teks.Subjudul("Tarif listrik"), gridTarif, Isian.Bidang("Rata-rata tagihan per bulan", _tagihan, "Rp")]);

        var kPratinjau = new Kartu { BackColor = Tema.BiruMuda, WarnaGaris = Color.Empty };
        var gridP = new GridKartu { Kolom = 4, LebarMinKolom = Tema.S(140), Jarak = Tema.S(12), Margin = new Padding(0, Tema.S(6), 0, Tema.S(6)) };
        gridP.Controls.AddRange([BlokPratinjau("Produksi harian", _pHarian), BlokPratinjau("Hemat per bulan", _pHemat),
            BlokPratinjau("Reduksi CO₂ per bulan", _pCo2), BlokPratinjau("Setara pohon", _pPohon)]);
        kPratinjau.Controls.AddRange([Teks.Subjudul("Pratinjau estimasi"), Teks.Redup("Diperbarui otomatis saat data diubah", 9), gridP, _pInfo]);

        var aksi = new Baris { Jarak = Tema.S(8), Margin = new Padding(0, Tema.S(4), 0, 0) };
        _btnHapus.Ikon = null;
        aksi.Tambah(_btnSimpan.Pas(Tema.S(120))).Tambah(_btnBatal.Pas()).Tambah(new Panel { Width = Tema.S(1), Height = 1 }, 1)
            .Tambah(_btnPeta.Pas()).Tambah(_btnDashboard.Pas()).Tambah(_btnHapus.Pas());

        var gridForm = new GridKartu { Kolom = 2, LebarMinKolom = Tema.S(380), Margin = Padding.Empty };
        gridForm.Controls.AddRange([kLokasi, kPanel]);
        _form.Controls.AddRange([_judulForm, gridForm, kTarif, kPratinjau, aksi]);
        _judulForm.Margin = new Padding(0, 0, 0, Tema.S(16));
        gridForm.Margin = new Padding(0, 0, 0, Tema.S(16));

        Controls.Add(_form);
        Controls.Add(new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Tema.Garis });
        Controls.Add(kiri);

        // ---------------- perilaku ----------------
        foreach (var c in new Control[] { _nama, _alamat })
            c.TextChanged += (_, _) => Berubah();
        foreach (var n in new[] { _lat, _lon, _jumlah, _daya, _luas, _tarif, _tagihan })
            n.ValueChanged += (_, _) => Berubah();
        _efisiensi.NilaiBerubah += (_, _) => { _nilaiEfisiensi.Text = $"{_efisiensi.Nilai}%"; Berubah(); };
        _golongan.SelectedIndexChanged += (_, _) =>
        {
            if (!_mengisi && _golongan.SelectedItem is TarifListrik t) _tarif.Value = (decimal)t.TarifPerKwh;
            Berubah();
        };
        _nama.Validating += (_, _) => ValidasiNama();
        _tunda.Tick += async (_, _) => { _tunda.Stop(); await PerbaruiPratinjauAsync(); };
        _btnSimpan.Click += async (_, _) => await SimpanAsync();
        _btnBatal.Click += (_, _) => { _kotor = false; Isi(_sedangDiedit); };
        _btnHapus.Click += async (_, _) => await HapusAsync();
        _btnCariKoordinat.Click += async (_, _) => await CariKoordinatAsync();
        _btnDashboard.Click += (_, _) => { if (_sedangDiedit.Id is not null && BolehTinggalkan()) K.Shell.Ke(HalamanId.Dashboard, _sedangDiedit); };
        _btnPeta.Click += (_, _) => BukaDiPeta();

        K.InstalasiBerubah += async (_, _) => { if (Visible) await MuatAsync(_sedangDiedit.Id); };
        K.Akun.Berubah += async (_, _) => { PerbaruiMode(); if (Visible) await MuatAsync(null); };
    }

    public override string Judul => "Instalasi Saya";

    public override async void Tampil(object? argumen)
    {
        PerbaruiMode();
        await MuatAsync((argumen as Instalasi)?.Id ?? _sedangDiedit.Id);
    }

    private static Tumpukan BlokPratinjau(string judul, Label nilai)
    {
        var t = new Tumpukan();
        nilai.Margin = new Padding(0, Tema.S(2), 0, 0);
        t.Controls.AddRange([Teks.Redup(judul, 9), nilai]);
        return t;
    }

    private void PerbaruiMode()
    {
        var akun = K.Akun;
        _kartuMode.WarnaAksen = akun.SudahMasuk ? Tema.Hijau : Tema.Oranye;
        _mode.Text = akun.SudahMasuk
            ? $"Tersinkron ke cloud Supabase sebagai {akun.Sesi!.Email}."
            : akun.CloudDiatur
                ? "Mode lokal: data disimpan di komputer ini. Masuk untuk menyimpan ke cloud."
                : "Mode lokal: data disimpan di komputer ini. Atur Supabase di Pengaturan untuk sinkron cloud.";
        _btnMasuk.Visible = akun.CloudDiatur && !akun.SudahMasuk;
        _kartuMode.PerformLayout();
    }

    private async Task MuatAsync(long? pilihId)
    {
        try
        {
            _data = await K.Akun.Repositori.DaftarAsync();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _data = [];
            K.Shell.Notifikasi("Gagal memuat instalasi: " + KonteksAplikasi.PesanGalat(e), galat: true);
        }

        _mengisi = true;
        _daftar.BeginUpdate();
        _daftar.Items.Clear();
        foreach (var i in _data) _daftar.Items.Add(i);
        _daftar.EndUpdate();
        _kosong.Visible = _data.Count == 0;
        var idx = pilihId is null ? -1 : _data.FindIndex(i => i.Id == pilihId);
        _mengisi = false;

        if (idx >= 0) { _daftar.SelectedIndex = idx; }
        else if (_data.Count > 0 && pilihId is null && _sedangDiedit.Id is null && !_kotor) { _daftar.SelectedIndex = 0; }
        else if (!_kotor) MulaiBaru();
    }

    private void PilihDariDaftar()
    {
        if (_mengisi || _daftar.SelectedIndex < 0 || _daftar.SelectedIndex == _indeksTerakhir) return;
        if (!BolehTinggalkan())
        {
            _mengisi = true;
            _daftar.SelectedIndex = _indeksTerakhir;
            _mengisi = false;
            return;
        }
        _indeksTerakhir = _daftar.SelectedIndex;
        Isi(((Instalasi)_daftar.SelectedItem!).Salin());
    }

    private bool BolehTinggalkan()
    {
        if (!_kotor) return true;
        var r = MessageBox.Show(this, "Ada perubahan yang belum disimpan. Buang perubahan?", "Instalasi Saya",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (r == DialogResult.Yes) { _kotor = false; return true; }
        return false;
    }

    private void MulaiBaru()
    {
        _mengisi = true;
        _daftar.ClearSelected();
        _mengisi = false;
        _indeksTerakhir = -1;
        var baru = new Instalasi { Nama = "", DibuatPada = DateTime.Now };
        if (K.Lokasi is { } l) { baru.Latitude = l.Lat; baru.Longitude = l.Lon; baru.Alamat = l.Nama.Length > 300 ? l.Nama[..300] : l.Nama; }
        Isi(baru);
        _nama.Focus();
    }

    private void Isi(Instalasi i)
    {
        _mengisi = true;
        _sedangDiedit = i;
        _judulForm.Text = i.Id is null ? "Instalasi baru" : $"Ubah: {i.Nama}";
        _nama.Text = i.Nama;
        _alamat.Text = i.Alamat;
        _lat.Value = (decimal)Math.Clamp(i.Latitude, -90, 90);
        _lon.Value = (decimal)Math.Clamp(i.Longitude, -180, 180);
        _jumlah.Value = Math.Clamp(i.JumlahPanel, 1, 300);
        _daya.Value = (decimal)Math.Clamp(Math.Round(i.JumlahPanel > 0 ? i.KapasitasKwp * 1000 / i.JumlahPanel : i.DayaPanelWp), 100, 1000);
        _efisiensi.AturDiam((int)Math.Round(i.EfisiensiPersen));
        _nilaiEfisiensi.Text = $"{_efisiensi.Nilai}%";
        _luas.Value = (decimal)Math.Clamp(i.LuasAtapM2 ?? 0, 0, 5000);
        _golongan.SelectedItem = DataReferensi.CariTarif(i.KodeTarif);
        if (_golongan.SelectedIndex < 0) _golongan.SelectedIndex = 3;
        _tarif.Value = (decimal)Math.Clamp(i.TarifPerKwh, 100, 10000);
        _tagihan.Value = (decimal)Math.Clamp(i.TagihanBulananRp, 0, 100_000_000);
        _galat.Clear();
        _galatNama.Visible = false;
        _mengisi = false;
        _kotor = false;
        PerbaruiTombol();
        PerbaruiKapasitas();
        _ = PerbaruiPratinjauAsync();
    }

    private void Berubah()
    {
        if (_mengisi) return;
        _kotor = true;
        PerbaruiKapasitas();
        PerbaruiTombol();
        _tunda.Stop();
        _tunda.Start();
    }

    private void PerbaruiKapasitas() => _kapasitas.Text = $"{Tema.N((double)(_jumlah.Value * _daya.Value) / 1000, 2)} kWp";

    private void PerbaruiTombol()
    {
        var tersimpan = _sedangDiedit.Id is not null;
        _btnHapus.Visible = tersimpan;
        _btnDashboard.Enabled = tersimpan && !_kotor;
        _btnBatal.Visible = _kotor;
        _btnSimpan.Text = tersimpan ? "Simpan perubahan" : "Simpan instalasi";
        _btnSimpan.Pas(Tema.S(120));
        _btnSimpan.Parent?.PerformLayout();
    }

    private bool ValidasiNama()
    {
        var pesan = string.IsNullOrWhiteSpace(_nama.Text) ? "Nama instalasi wajib diisi" : null;
        _galat.SetError(_nama, pesan ?? "");
        _galatNama.Text = pesan ?? "";
        _galatNama.Visible = pesan is not null;
        _galatNama.Parent?.PerformLayout();
        return pesan is null;
    }

    private Instalasi BacaForm()
    {
        var i = _sedangDiedit.Salin();
        i.Nama = _nama.Text.Trim();
        i.Alamat = _alamat.Text.Trim();
        i.Latitude = (double)_lat.Value;
        i.Longitude = (double)_lon.Value;
        i.JumlahPanel = (int)_jumlah.Value;
        i.DayaPanelWp = (double)_daya.Value;
        i.KapasitasKwp = Math.Round(i.JumlahPanel * i.DayaPanelWp / 1000, 3);
        i.EfisiensiPersen = _efisiensi.Nilai;
        i.LuasAtapM2 = _luas.Value > 0 ? (double)_luas.Value : null;
        i.KodeTarif = (_golongan.SelectedItem as TarifListrik)?.Kode;
        i.TarifPerKwh = (double)_tarif.Value;
        i.TagihanBulananRp = (double)_tagihan.Value;
        return i;
    }

    private async Task PerbaruiPratinjauAsync()
    {
        var i = BacaForm();
        var versi = ++_versiPratinjau;
        var m = KonteksAplikasi.MasukanDari(i);
        try
        {
            var r = await K.Iklim.RingkasanTahunanAsync(m.Latitude, m.Longitude);
            if (versi != _versiPratinjau) return;
            var h = LayananEstimasi.Hitung(m, r);
            _pHarian.Text = $"{Tema.N(h.Sistem.ProduksiHarianRataKwh, 1)} kWh";
            _pHemat.Text = Tema.RpRingkas(h.Finansial.HematTahunPertama / 12);
            _pCo2.Text = $"{Tema.N(h.Lingkungan.Co2TahunanKg / 12, 0)} kg";
            _pPohon.Text = $"{Tema.N(h.Lingkungan.SetaraPohon, 1)}";
            _pInfo.Text = $"Berdasarkan iklim {r.Tahun}: {Tema.N(r.PshRataHarian, 2)} jam matahari puncak/hari, suhu rata-rata {Tema.N(r.SuhuRata, 1)} °C, " +
                          $"PR {Tema.N(h.Sistem.PerformanceRatio, 2)}. Cakupan kebutuhan listrik {Tema.N(h.Sistem.CakupanPersen)}%. " +
                          $"Faktor emisi grid {Tema.N(KalkulatorKarbon.FaktorEmisiDefault, 3)} kg CO₂/kWh.";
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (versi != _versiPratinjau) return;
            _pInfo.Text = "Pratinjau belum tersedia: " + KonteksAplikasi.PesanGalat(e);
        }
    }

    private async Task SimpanAsync()
    {
        if (!ValidasiNama())
        {
            _nama.Focus();
            return;
        }
        _btnSimpan.Enabled = false;
        await Aman(async () =>
        {
            var tersimpan = await K.Akun.Repositori.SimpanAsync(BacaForm());
            _kotor = false;
            _sedangDiedit = tersimpan;
            await MuatAsync(tersimpan.Id);
            K.UmumkanInstalasiBerubah();
            K.Shell.Notifikasi(K.Akun.Repositori.Nama == "Supabase" ? $"\"{tersimpan.Nama}\" tersimpan di Supabase." : $"\"{tersimpan.Nama}\" tersimpan di komputer ini.");
        });
        _btnSimpan.Enabled = true;
    }

    private async Task HapusAsync()
    {
        if (_sedangDiedit.Id is not { } id) return;
        var r = MessageBox.Show(this, $"Hapus instalasi \"{_sedangDiedit.Nama}\" beserta riwayatnya? Tindakan ini tidak bisa dibatalkan.",
            "Hapus instalasi", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (r != DialogResult.Yes) return;
        await Aman(async () =>
        {
            await K.Akun.Repositori.HapusAsync(id);
            _kotor = false;
            _sedangDiedit = new Instalasi();
            await MuatAsync(null);
            K.UmumkanInstalasiBerubah();
        }, "Instalasi dihapus.");
    }

    private async Task CariKoordinatAsync()
    {
        var q = _alamat.Text.Trim();
        if (q.Length < 3) { K.Shell.Notifikasi("Isi alamat terlebih dahulu.", galat: true); return; }
        _btnCariKoordinat.Enabled = false;
        await Aman(async () =>
        {
            var hasil = await K.Geocoder.CariAsync(q);
            if (hasil.Count == 0) { K.Shell.Notifikasi("Alamat tidak ditemukan.", galat: true); return; }
            var h = hasil[0];
            _lat.Value = (decimal)Math.Round(h.Lat, 5);
            _lon.Value = (decimal)Math.Round(h.Lon, 5);
            K.Shell.Notifikasi($"Koordinat diisi dari: {h.Nama}");
        });
        _btnCariKoordinat.Enabled = true;
    }

    private void BukaDiPeta()
    {
        var i = BacaForm();
        K.Lokasi = new HasilLokasi(string.IsNullOrWhiteSpace(i.Alamat) ? i.Nama : i.Alamat, i.Latitude, i.Longitude, "instalasi");
        K.Masukan = KonteksAplikasi.MasukanDari(i, K.Masukan) with { JumlahPanel = null };
        K.PoligonAtap = i.PoligonAtap?.ToList();
        K.Shell.Ke(HalamanId.HasilAtap);
    }

    private void GambarItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _daftar.Items.Count) return;
        var i = (Instalasi)_daftar.Items[e.Index];
        var g = e.Graphics;
        Tema.Halus(g);
        var terpilih = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(Tema.LatarAbu)) g.FillRectangle(bg, e.Bounds);
        var r = new RectangleF(e.Bounds.X + 2, e.Bounds.Y + Tema.S(4), e.Bounds.Width - Tema.S(8), e.Bounds.Height - Tema.S(8));
        using (var path = Tema.Bulat(r, Tema.S(10)))
        {
            using var b = new SolidBrush(terpilih ? Tema.BiruMuda : Color.White);
            g.FillPath(b, path);
            using var p = new Pen(terpilih ? Tema.Biru : Tema.GarisHalus);
            g.DrawPath(p, path);
        }
        var x = (int)r.X + Tema.S(14);
        var lebar = (int)r.Width - Tema.S(28);
        TextRenderer.DrawText(g, i.Nama, Tema.Font(10.5f, semibold: true), new Rectangle(x, (int)r.Y + Tema.S(8), lebar, Tema.S(22)), Tema.Teks,
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g, string.IsNullOrWhiteSpace(i.Alamat) ? $"{i.Latitude:0.####}, {i.Longitude:0.####}" : i.Alamat, Tema.Font(8.5f),
            new Rectangle(x, (int)r.Y + Tema.S(30), lebar, Tema.S(18)), Tema.TeksRedup, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g, $"{Tema.N(i.KapasitasKwp, 1)} kWp · {i.JumlahPanel} panel · diperbarui {i.DiperbaruiPada:d MMM yyyy}", Tema.Font(8.5f),
            new Rectangle(x, (int)r.Y + Tema.S(48), lebar, Tema.S(18)), terpilih ? Tema.Biru : Tema.TeksPudar, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        if ((e.State & DrawItemState.Focus) != 0 && _daftar.Focused) e.DrawFocusRectangle();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tunda.Dispose(); _galat.Dispose(); }
        base.Dispose(disposing);
    }
}
