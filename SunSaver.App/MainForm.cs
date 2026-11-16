using SunSaver.App.Ui;
using SunSaver.App.Ui.Dialog;
using SunSaver.App.Ui.Halaman;
using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App;

/// <summary>
/// Jendela utama: header bergaya Project Sunroof (logo, navigasi, status akun) dan area konten
/// yang menampilkan satu halaman pada satu waktu.
/// </summary>
public sealed class MainForm : Form, IShell
{
    private readonly KonteksAplikasi _k;
    private readonly Panel _header = new() { Dock = DockStyle.Top, BackColor = Color.White };
    private readonly Panel _konten = new() { Dock = DockStyle.Fill, BackColor = Tema.Latar };
    private readonly Logo _logo = new();
    private readonly Dictionary<HalamanId, NavTautan> _nav = [];
    private readonly Dictionary<HalamanId, HalamanDasar> _halaman = [];
    private readonly Label _lencanaMode = new() { AutoSize = true, Font = Tema.Font(8.5f, semibold: true), Padding = new Padding(Tema.S(10), Tema.S(4), Tema.S(10), Tema.S(4)) };
    private readonly TombolBulat _btnPengaturan = new("Pengaturan", GayaTombol.Teks);
    private readonly TombolBulat _btnMasuk = new("Masuk");
    private readonly TombolBulat _btnAkun = new("", GayaTombol.Sekunder);
    private readonly ContextMenuStrip _menuAkun = new() { Font = Tema.Font(10), ShowImageMargin = false };
    private readonly Snackbar _snackbar = new();
    private HalamanId? _aktif;

    public MainForm()
    {
        Text = "SunSaver — Petakan potensi hemat surya atap Anda";
        AutoScaleMode = AutoScaleMode.None;
        Font = Tema.FontIsi;
        BackColor = Color.White;
        StartPosition = FormStartPosition.CenterScreen;
        var layar = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1600, 900);
        Size = new Size(Math.Min(Tema.S(1400), layar.Width - 40), Math.Min(Tema.S(900), layar.Height - 40));
        MinimumSize = new Size(Math.Min(Tema.S(1080), layar.Width), Math.Min(Tema.S(680), layar.Height));
        Icon = BuatIkon();

        _k = new KonteksAplikasi(this);

        // ---------------- header ----------------
        _header.Height = Tema.S(64);
        _header.Paint += (_, e) => { using var p = new Pen(Tema.Garis); e.Graphics.DrawLine(p, 0, _header.Height - 1, _header.Width, _header.Height - 1); };
        _logo.Click += (_, _) => Ke(HalamanId.Beranda);
        _header.Controls.Add(_logo);
        foreach (var (id, teks) in new[]
                 {
                     (HalamanId.Beranda, "Beranda"), (HalamanId.Jelajah, "Jelajah Data"), (HalamanId.Instalasi, "Instalasi Saya"),
                     (HalamanId.Dashboard, "Dashboard Dampak"), (HalamanId.Tentang, "Tentang"),
                 })
        {
            var n = new NavTautan(teks);
            n.Click += (_, _) => Ke(id);
            _nav[id] = n;
            _header.Controls.Add(n);
        }
        _btnPengaturan.Pas();
        _btnPengaturan.Click += (_, _) => BukaPengaturan();
        _btnMasuk.Pas(Tema.S(96));
        _btnMasuk.Click += async (_, _) => await PastikanMasukAsync("menyinkronkan data");
        _btnAkun.Click += (_, _) => _menuAkun.Show(_btnAkun, new Point(0, _btnAkun.Height));
        _menuAkun.Items.Add("Instalasi Saya", null, (_, _) => Ke(HalamanId.Instalasi));
        _menuAkun.Items.Add("Pengaturan", null, (_, _) => BukaPengaturan());
        _menuAkun.Items.Add(new ToolStripSeparator());
        _menuAkun.Items.Add("Keluar", null, async (_, _) =>
        {
            await _k.Akun.KeluarAsync();
            Notifikasi("Anda telah keluar. Data baru akan disimpan di komputer ini.");
        });
        _header.Controls.AddRange([_lencanaMode, _btnPengaturan, _btnMasuk, _btnAkun]);
        _header.Resize += (_, _) => TataHeader();

        Controls.Add(_konten);
        Controls.Add(_header);
        Controls.Add(_snackbar);
        _snackbar.BringToFront();

        _k.Akun.Berubah += (_, _) => PerbaruiAkun();
        PerbaruiAkun();
    }

    public IWin32Window Jendela => this;

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Control.Visible baru bernilai benar setelah form tampil, jadi header ditata ulang di sini.
        TataHeader();
        Ke(HalamanId.Beranda);
        await _k.Akun.PulihkanSesiAsync();
        if (_k.Akun.SudahMasuk) Notifikasi($"Selamat datang kembali, {_k.Akun.Sesi!.Nama}.");
    }

    // ------------------------------------------------------------------ IShell

    public void Ke(HalamanId id, object? argumen = null)
    {
        if (!_halaman.TryGetValue(id, out var h))
        {
            h = id switch
            {
                HalamanId.Beranda => new BerandaHalaman(_k),
                HalamanId.HasilAtap => new HasilAtapHalaman(_k),
                HalamanId.Estimasi => new EstimasiHalaman(_k),
                HalamanId.Dashboard => new DashboardHalaman(_k),
                HalamanId.Instalasi => new InstalasiHalaman(_k),
                HalamanId.Jelajah => new JelajahHalaman(_k),
                HalamanId.Tentang => new TentangHalaman(_k),
                _ => throw new ArgumentOutOfRangeException(nameof(id)),
            };
            h.Visible = false;
            _halaman[id] = h;
            _konten.Controls.Add(h);
        }

        _konten.SuspendLayout();
        foreach (var (lain, hal) in _halaman)
            if (lain != id) hal.Visible = false;
        h.Visible = true;
        h.BringToFront();
        _konten.ResumeLayout(true);
        _aktif = id;

        foreach (var (navId, n) in _nav)
            n.Aktif = navId == id || (navId == HalamanId.Beranda && id is HalamanId.HasilAtap or HalamanId.Estimasi);
        Text = $"{h.Judul} · SunSaver";
        h.Tampil(argumen);
    }

    public void Notifikasi(string pesan, bool galat = false) => _snackbar.Tampil(pesan, galat);

    public async Task<bool> PastikanMasukAsync(string alasan)
    {
        if (_k.Akun.SudahMasuk) return true;
        if (!_k.Akun.CloudDiatur)
        {
            var r = MessageBox.Show(this,
                $"Untuk {alasan}, SunSaver perlu terhubung ke project Supabase Anda. Buka Pengaturan sekarang?",
                "Supabase belum diatur", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (r != DialogResult.Yes || !BukaPengaturan()) return false;
        }
        using var d = new MasukDialog(_k.Akun);
        var ok = d.ShowDialog(this) == DialogResult.OK && _k.Akun.SudahMasuk;
        if (ok) Notifikasi($"Masuk sebagai {_k.Akun.Sesi!.Email}. Instalasi kini disimpan di Supabase.");
        await Task.CompletedTask;
        return ok;
    }

    // ------------------------------------------------------------------ header

    private bool BukaPengaturan()
    {
        using var d = new PengaturanDialog(_k.Akun);
        var ok = d.ShowDialog(this) == DialogResult.OK;
        if (ok) Notifikasi(_k.Akun.CloudDiatur ? "Pengaturan Supabase disimpan. Silakan masuk untuk sinkron cloud." : "Mode lokal aktif.");
        return ok && _k.Akun.CloudDiatur;
    }

    private void PerbaruiAkun()
    {
        var a = _k.Akun;
        _lencanaMode.Text = a.LabelMode;
        _lencanaMode.BackColor = a.SudahMasuk ? Tema.HijauMuda : Tema.LatarAbu;
        _lencanaMode.ForeColor = a.SudahMasuk ? Tema.Hijau : Tema.TeksRedup;
        _btnMasuk.Visible = !a.SudahMasuk;
        _btnAkun.Visible = a.SudahMasuk;
        if (a.Sesi is { } s)
        {
            var nama = s.Nama.Length > 18 ? s.Nama[..17] + "…" : s.Nama;
            _btnAkun.Text = nama + "  ▾";
            _btnAkun.AccessibleName = $"Akun {s.Email}";
            _btnAkun.Pas();
        }
        TataHeader();
    }

    private void TataHeader()
    {
        var h = _header.Height;
        var x = Tema.S(24);
        _logo.Location = new Point(x, 0);
        x += _logo.Width + Tema.S(16);
        foreach (var n in _nav.Values)
        {
            n.Location = new Point(x, 0);
            x += n.Width;
        }

        _lencanaMode.Visible = _lencanaMode.Text.Length > 0;
        var kanan = _header.Width - Tema.S(24);
        var masuk = _k.Akun.SudahMasuk;
        foreach (var c in new Control[] { masuk ? _btnAkun : _btnMasuk, _btnPengaturan, _lencanaMode })
        {
            var w = c is Label l ? l.PreferredWidth : c.Width;
            var tinggi = c is Label lb ? lb.PreferredHeight : c.Height;
            kanan -= w;
            c.Location = new Point(kanan, (h - tinggi) / 2);
            kanan -= Tema.S(10);
        }
        // Sembunyikan label mode bila bertabrakan dengan navigasi (jendela sempit).
        if (_lencanaMode.Left < x + Tema.S(8)) _lencanaMode.Visible = false;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_snackbar.Visible) _snackbar.Location = new Point((ClientSize.Width - _snackbar.Width) / 2, ClientSize.Height - _snackbar.Height - Tema.S(28));
    }

    private static Icon BuatIkon()
    {
        using var bmp = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(bmp))
        {
            Tema.Halus(g);
            g.Clear(Color.Transparent);
            Logo.GambarMatahari(g, new RectangleF(2, 2, 60, 60));
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
