using SunSaver.App.Core.Model;

namespace SunSaver.App.Ui.Kontrol;

/// <summary>
/// Kotak pencarian alamat bergaya hero Project Sunroof: input lebar membulat dengan bayangan
/// lembut dan tombol aksi di dalamnya. Hasil ganda ditampilkan sebagai menu tarik-turun.
/// </summary>
public class KotakCari : Control
{
    private readonly TextBox _input;
    private readonly TombolBulat _tombol;
    private readonly ContextMenuStrip _menuHasil;

    public KotakCari(string placeholder = "Masukkan alamat rumah Anda", string teksTombol = "Cek atap saya")
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint, true);

        _input = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = Tema.Font(13),
            PlaceholderText = placeholder,
            ForeColor = Tema.Teks,
            BackColor = Color.White,
            AccessibleName = "Alamat atau koordinat",
        };
        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            Kirim();
        };

        _tombol = new TombolBulat(teksTombol) { Height = Tema.S(46) };
        _tombol.Pas(Tema.S(150));
        _tombol.Click += (_, _) => Kirim();

        _menuHasil = new ContextMenuStrip { Font = Tema.Font(10), ShowImageMargin = false, MaximumSize = new Size(Tema.S(720), Tema.S(400)) };

        Controls.Add(_input);
        Controls.Add(_tombol);
        Height = Tema.S(64);
    }

    /// <summary>Dipicu saat pengguna menekan Enter / tombol, berisi teks pencarian.</summary>
    public event EventHandler<string>? Cari;

    /// <summary>Dipicu saat pengguna memilih salah satu hasil dari menu.</summary>
    public event EventHandler<HasilLokasi>? HasilDipilih;

    public string Teks { get => _input.Text; set => _input.Text = value; }

    private string? _teksTombolAsli;

    public void AturSibuk(bool sibuk)
    {
        _teksTombolAsli ??= _tombol.Text;
        _tombol.Enabled = !sibuk;
        _tombol.Text = sibuk ? "Mencari…" : _teksTombolAsli;
        _input.Enabled = !sibuk;
    }

    public void FokusInput() => _input.Focus();

    /// <summary>Tampilkan beberapa hasil geocoding untuk dipilih.</summary>
    public void TampilkanHasil(IReadOnlyList<HasilLokasi> hasil)
    {
        _menuHasil.Items.Clear();
        foreach (var h in hasil)
        {
            var nama = h.Nama.Length > 95 ? h.Nama[..92] + "…" : h.Nama;
            var item = new ToolStripMenuItem(nama) { ToolTipText = h.Nama, Padding = new Padding(0, Tema.S(4), 0, Tema.S(4)) };
            item.Click += (_, _) => HasilDipilih?.Invoke(this, h);
            _menuHasil.Items.Add(item);
        }
        _menuHasil.Show(this, new Point(Tema.S(24), Height - Tema.S(2)));
    }

    private void Kirim()
    {
        var t = _input.Text.Trim();
        if (t.Length >= 3) Cari?.Invoke(this, t);
        else _input.Focus();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_input is null || _tombol is null) return; // dipanggil sebelum konstruktor selesai
        var tinggiKotak = Height - Tema.S(8);
        _tombol.Location = new Point(Width - _tombol.Width - Tema.S(10), (tinggiKotak - _tombol.Height) / 2 + Tema.S(1));
        var kiri = Tema.S(56);
        _input.SetBounds(kiri, (tinggiKotak - _input.PreferredHeight) / 2, Math.Max(10, _tombol.Left - kiri - Tema.S(12)), _input.PreferredHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Tema.Latar);
        Tema.Halus(g);
        var tinggiKotak = Height - Tema.S(8);
        // bayangan lembut berlapis
        for (var i = 4; i >= 1; i--)
        {
            using var pb = Tema.Bulat(new RectangleF(1, i * 1.5f, Width - 3, tinggiKotak), tinggiKotak / 2f);
            using var bb = new SolidBrush(Color.FromArgb(10, 60, 64, 67));
            g.FillPath(bb, pb);
        }
        using var path = Tema.Bulat(new RectangleF(1, 1, Width - 3, tinggiKotak - 2), (tinggiKotak - 2) / 2f);
        g.FillPath(Brushes.White, path);
        using (var p = new Pen(_input.Focused ? Tema.Biru : Tema.Garis, _input.Focused ? 2f : 1f)) g.DrawPath(p, path);

        var uk = Tema.S(20f);
        Ikon.Cari(g, new RectangleF(Tema.S(22), (tinggiKotak - uk) / 2f, uk, uk), Tema.TeksRedup);
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        _input.GotFocus += (_, _) => Invalidate();
        _input.LostFocus += (_, _) => Invalidate();
    }
}
