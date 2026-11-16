using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace SunSaver.App.Ui.Kontrol;

/// <summary>Kartu putih bersudut membulat dengan garis tipis (gaya Material). Isinya ditumpuk vertikal.</summary>
public class Kartu : Tumpukan
{
    public Kartu()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint, true);
        BackColor = Color.White;
        Padding = Tema.P(20);
        Margin = Tema.P(0, 0, 0, 16);
    }

    public static Kartu Dengan(params Control[] isi)
    {
        var k = new Kartu();
        k.Controls.AddRange(isi);
        return k;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = Tema.S(12);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color WarnaGaris { get; set; } = Tema.Garis;

    /// <summary>Strip warna di sisi kiri (aksen status).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? WarnaAksen { get; set; }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Tema.Latar);
        Tema.Halus(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Tema.Bulat(r, Radius);
        using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
        if (WarnaAksen is { } aksen)
        {
            var klip = g.Clip;
            g.SetClip(path);
            using var ba = new SolidBrush(aksen);
            g.FillRectangle(ba, 0, 0, Tema.S(4), Height);
            g.Clip = klip;
        }
        if (WarnaGaris.A > 0)
        {
            using var p = new Pen(WarnaGaris, 1f);
            g.DrawPath(p, path);
        }
    }
}

/// <summary>Pabrik label dengan tipografi konsisten.</summary>
public static class Teks
{
    public static Label Judul(string teks, float pt = 20) => Buat(teks, Tema.Font(pt), Tema.Teks);
    public static Label Subjudul(string teks) => Buat(teks, Tema.FontSubjudul, Tema.Teks);
    public static Label Isi(string teks) => Buat(teks, Tema.FontIsi, Tema.Teks);
    public static Label Redup(string teks, float pt = 9.5f) => Buat(teks, Tema.Font(pt), Tema.TeksRedup);
    public static Label Angka(string teks, float pt = 22) => Buat(teks, Tema.Font(pt, semibold: true), Tema.Teks);

    public static Label Buat(string teks, Font font, Color warna) => new()
    {
        Text = teks,
        Font = font,
        ForeColor = warna,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, Tema.S(4)),
        UseMnemonic = false,
    };

    /// <summary>Label yang membungkus teks mengikuti lebar induknya.</summary>
    public static LabelBungkus Bungkus(string teks, Font? font = null, Color? warna = null) => new()
    {
        Text = teks,
        Font = font ?? Tema.FontIsi,
        ForeColor = warna ?? Tema.TeksRedup,
        UseMnemonic = false,
        Margin = new Padding(0, 0, 0, Tema.S(6)),
    };
}

/// <summary>Label multi-baris; tingginya dihitung kontainer tata letak berdasarkan lebar.</summary>
public class LabelBungkus : Label
{
    public LabelBungkus()
    {
        AutoSize = false;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Parent?.PerformLayout();
    }
}

/// <summary>Bilah progres membulat (dipakai untuk lencana gamifikasi dan cakupan kebutuhan listrik).</summary>
public class BilahProgres : Control
{
    private double _nilai;

    public BilahProgres()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        Height = Tema.S(10);
        AccessibleRole = AccessibleRole.ProgressBar;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Nilai
    {
        get => _nilai;
        set { _nilai = Math.Clamp(value, 0, 100); AccessibleDescription = $"{_nilai:0}%"; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Warna { get; set; } = Tema.Hijau;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Color.White);
        Tema.Halus(g);
        var r = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var path = Tema.Bulat(r, r.Height / 2))
        using (var b = new SolidBrush(Tema.GarisHalus))
            g.FillPath(b, path);
        if (_nilai <= 0) return;
        var isi = new RectangleF(0, 0, Math.Max(r.Height, (float)(r.Width * _nilai / 100)), r.Height);
        using (var path = Tema.Bulat(isi, r.Height / 2))
        using (var b = new SolidBrush(Warna))
            g.FillPath(b, path);
    }
}

/// <summary>Kotak ikon bulat berwarna (dipakai pada baris statistik).</summary>
public class IkonBulat : Control
{
    public IkonBulat(Action<Graphics, RectangleF, Color> gambar, Color warna, Color latar)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Gambar = gambar;
        Warna = warna;
        Latar = latar;
        Size = new Size(Tema.S(44), Tema.S(44));
        Margin = new Padding(0, 0, Tema.S(14), 0);
    }

    public Action<Graphics, RectangleF, Color> Gambar { get; }
    public Color Warna { get; }
    public Color Latar { get; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Color.White);
        Tema.Halus(g);
        using (var b = new SolidBrush(Latar)) g.FillEllipse(b, 0, 0, Width - 1, Height - 1);
        var m = Width * 0.25f;
        Gambar(g, new RectangleF(m, m, Width - 2 * m, Height - 2 * m), Warna);
    }
}

/// <summary>Ikon vektor sederhana (tanpa berkas gambar eksternal).</summary>
public static class Ikon
{
    public static void Matahari(Graphics g, RectangleF r, Color c)
    {
        var cx = r.X + r.Width / 2; var cy = r.Y + r.Height / 2;
        var rad = r.Width * 0.22f;
        using var b = new SolidBrush(c);
        using var p = new Pen(c, Math.Max(1.5f, r.Width / 11)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2);
        for (var i = 0; i < 8; i++)
        {
            var a = i * Math.PI / 4;
            float r1 = r.Width * 0.34f, r2 = r.Width * 0.48f;
            g.DrawLine(p, cx + (float)Math.Cos(a) * r1, cy + (float)Math.Sin(a) * r1,
                cx + (float)Math.Cos(a) * r2, cy + (float)Math.Sin(a) * r2);
        }
    }

    public static void Atap(Graphics g, RectangleF r, Color c)
    {
        using var p = new Pen(c, Math.Max(1.5f, r.Width / 11)) { LineJoin = LineJoin.Round };
        var pts = new[]
        {
            new PointF(r.X, r.Y + r.Height * 0.55f), new PointF(r.X + r.Width / 2, r.Y + r.Height * 0.12f),
            new PointF(r.Right, r.Y + r.Height * 0.55f),
        };
        g.DrawLines(p, pts);
        g.DrawRectangle(p, r.X + r.Width * 0.18f, r.Y + r.Height * 0.5f, r.Width * 0.64f, r.Height * 0.42f);
        using var b = new SolidBrush(c);
        g.FillRectangle(b, r.X + r.Width * 0.3f, r.Y + r.Height * 0.6f, r.Width * 0.16f, r.Height * 0.12f);
        g.FillRectangle(b, r.X + r.Width * 0.54f, r.Y + r.Height * 0.6f, r.Width * 0.16f, r.Height * 0.12f);
    }

    public static void Uang(Graphics g, RectangleF r, Color c)
    {
        using var p = new Pen(c, Math.Max(1.5f, r.Width / 11));
        var kotak = new RectangleF(r.X, r.Y + r.Height * 0.22f, r.Width, r.Height * 0.56f);
        using (var path = Tema.Bulat(kotak, r.Width * 0.08f)) g.DrawPath(p, path);
        var d = r.Height * 0.3f;
        g.DrawEllipse(p, r.X + r.Width / 2 - d / 2, r.Y + r.Height / 2 - d / 2, d, d);
    }

    public static void Daun(Graphics g, RectangleF r, Color c)
    {
        using var b = new SolidBrush(c);
        using var path = new GraphicsPath();
        path.AddBezier(r.X + r.Width * 0.1f, r.Bottom - r.Height * 0.1f, r.X, r.Y + r.Height * 0.2f,
            r.X + r.Width * 0.6f, r.Y, r.Right, r.Y);
        path.AddBezier(r.Right, r.Y, r.Right, r.Y + r.Height * 0.6f, r.X + r.Width * 0.6f, r.Bottom,
            r.X + r.Width * 0.1f, r.Bottom - r.Height * 0.1f);
        g.FillPath(b, path);
        using var p = new Pen(Color.White, Math.Max(1f, r.Width / 14));
        g.DrawLine(p, r.X + r.Width * 0.15f, r.Bottom - r.Height * 0.15f, r.X + r.Width * 0.75f, r.Y + r.Height * 0.25f);
    }

    public static void Pohon(Graphics g, RectangleF r, Color c)
    {
        using var batang = new SolidBrush(Tema.Hex("#8D6E63"));
        g.FillRectangle(batang, r.X + r.Width * 0.44f, r.Y + r.Height * 0.62f, r.Width * 0.12f, r.Height * 0.38f);
        using var b = new SolidBrush(c);
        g.FillEllipse(b, r.X + r.Width * 0.12f, r.Y + r.Height * 0.22f, r.Width * 0.76f, r.Height * 0.5f);
        g.FillEllipse(b, r.X + r.Width * 0.24f, r.Y, r.Width * 0.52f, r.Height * 0.46f);
    }

    public static void Petir(Graphics g, RectangleF r, Color c)
    {
        using var b = new SolidBrush(c);
        g.FillPolygon(b, new[]
        {
            new PointF(r.X + r.Width * 0.58f, r.Y), new PointF(r.X + r.Width * 0.15f, r.Y + r.Height * 0.58f),
            new PointF(r.X + r.Width * 0.47f, r.Y + r.Height * 0.58f), new PointF(r.X + r.Width * 0.38f, r.Bottom),
            new PointF(r.X + r.Width * 0.85f, r.Y + r.Height * 0.4f), new PointF(r.X + r.Width * 0.53f, r.Y + r.Height * 0.4f),
        });
    }

    public static void Awan(Graphics g, RectangleF r, Color c)
    {
        using var b = new SolidBrush(c);
        g.FillEllipse(b, r.X, r.Y + r.Height * 0.4f, r.Width * 0.45f, r.Height * 0.45f);
        g.FillEllipse(b, r.X + r.Width * 0.22f, r.Y + r.Height * 0.15f, r.Width * 0.5f, r.Height * 0.6f);
        g.FillEllipse(b, r.X + r.Width * 0.5f, r.Y + r.Height * 0.35f, r.Width * 0.5f, r.Height * 0.5f);
        g.FillRectangle(b, r.X + r.Width * 0.2f, r.Y + r.Height * 0.6f, r.Width * 0.6f, r.Height * 0.25f);
    }

    public static void Termometer(Graphics g, RectangleF r, Color c)
    {
        using var p = new Pen(c, Math.Max(1.5f, r.Width / 11));
        using var b = new SolidBrush(c);
        var w = r.Width * 0.28f;
        using (var path = Tema.Bulat(new RectangleF(r.X + r.Width / 2 - w / 2, r.Y, w, r.Height * 0.7f), w / 2))
            g.DrawPath(p, path);
        var d = r.Width * 0.46f;
        g.FillEllipse(b, r.X + r.Width / 2 - d / 2, r.Bottom - d, d, d);
    }

    public static void Pin(Graphics g, RectangleF r, Color c)
    {
        using var b = new SolidBrush(c);
        using var path = new GraphicsPath();
        var cx = r.X + r.Width / 2;
        path.AddArc(r.X + r.Width * 0.15f, r.Y, r.Width * 0.7f, r.Width * 0.7f, 150, 240);
        path.AddLine(r.X + r.Width * 0.82f, r.Y + r.Width * 0.5f, cx, r.Bottom);
        path.CloseFigure();
        g.FillPath(b, path);
        using var putih = new SolidBrush(Color.White);
        var d = r.Width * 0.26f;
        g.FillEllipse(putih, cx - d / 2, r.Y + r.Width * 0.35f - d / 2, d, d);
    }

    public static void Grafik(Graphics g, RectangleF r, Color c)
    {
        using var b = new SolidBrush(c);
        var w = r.Width / 5;
        g.FillRectangle(b, r.X, r.Y + r.Height * 0.55f, w, r.Height * 0.45f);
        g.FillRectangle(b, r.X + w * 2, r.Y + r.Height * 0.25f, w, r.Height * 0.75f);
        g.FillRectangle(b, r.X + w * 4, r.Y, w, r.Height);
    }

    public static void Cari(Graphics g, RectangleF r, Color c)
    {
        using var p = new Pen(c, Math.Max(1.5f, r.Width / 9)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var d = r.Width * 0.62f;
        g.DrawEllipse(p, r.X, r.Y, d, d);
        g.DrawLine(p, r.X + d * 0.85f, r.Y + d * 0.85f, r.Right, r.Bottom);
    }

    public static void Pensil(Graphics g, RectangleF r, Color c)
    {
        using var p = new Pen(c, Math.Max(1.5f, r.Width / 9)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawPolygon(p, new[]
        {
            new PointF(r.X + r.Width * 0.15f, r.Bottom - r.Height * 0.05f), new PointF(r.X + r.Width * 0.05f, r.Bottom - r.Height * 0.05f),
            new PointF(r.X + r.Width * 0.05f, r.Bottom - r.Height * 0.2f), new PointF(r.Right - r.Width * 0.2f, r.Y + r.Height * 0.05f),
            new PointF(r.Right - r.Width * 0.05f, r.Y + r.Height * 0.2f),
        });
    }

    public static void Lapisan(Graphics g, RectangleF r, Color c)
    {
        using var p = new Pen(c, Math.Max(1.5f, r.Width / 11)) { LineJoin = LineJoin.Round };
        for (var i = 0; i < 2; i++)
        {
            var y = r.Y + r.Height * (0.15f + i * 0.35f);
            g.DrawPolygon(p, new[]
            {
                new PointF(r.X + r.Width / 2, y), new PointF(r.Right, y + r.Height * 0.2f),
                new PointF(r.X + r.Width / 2, y + r.Height * 0.4f), new PointF(r.X, y + r.Height * 0.2f),
            });
        }
    }

    public static void Simpan(Graphics g, RectangleF r, Color c)
    {
        using var p = new Pen(c, Math.Max(1.5f, r.Width / 11)) { LineJoin = LineJoin.Round };
        g.DrawRectangle(p, r.X + r.Width * 0.08f, r.Y + r.Height * 0.08f, r.Width * 0.84f, r.Height * 0.84f);
        g.DrawRectangle(p, r.X + r.Width * 0.28f, r.Y + r.Height * 0.08f, r.Width * 0.44f, r.Height * 0.28f);
    }
}

/// <summary>
/// Baris statistik gaya panel kanan Project Sunroof: ikon bulat + angka besar + keterangan.
/// Contoh: "1.540 jam sinar matahari efektif per tahun — Berdasarkan analisis cuaca harian".
/// </summary>
public class BarisStat : Baris
{
    private readonly Label _nilai;
    private readonly Label _judul;
    private readonly LabelBungkus _keterangan;

    public BarisStat(Action<Graphics, RectangleF, Color> ikon, Color warna, Color latar)
    {
        RataTengah = false;
        Jarak = Tema.S(14);
        Margin = new Padding(0, 0, 0, Tema.S(18));
        var teks = new Tumpukan();
        _nilai = Teks.Buat("—", Tema.Font(17, semibold: true), Tema.Teks);
        _nilai.Margin = Padding.Empty;
        _judul = Teks.Buat("", Tema.Font(10.5f), Tema.Teks);
        _judul.Margin = new Padding(0, 0, 0, Tema.S(2));
        _keterangan = Teks.Bungkus("", Tema.Font(8.5f), Tema.TeksRedup);
        teks.Controls.AddRange([_nilai, _judul, _keterangan]);
        Tambah(new IkonBulat(ikon, warna, latar) { Margin = new Padding(0, Tema.S(2), 0, 0) });
        Tambah(teks, 1);
    }

    public void Atur(string nilai, string judul, string keterangan)
    {
        _nilai.Text = nilai;
        _judul.Text = judul;
        _keterangan.Text = keterangan;
        AccessibleName = $"{nilai} {judul}";
        PerformLayout();
    }
}

/// <summary>Kartu ringkasan angka (dashboard): label kecil, angka besar, keterangan.</summary>
public class KartuStat : Kartu
{
    private readonly Label _judul;
    private readonly Label _nilai;
    private readonly LabelBungkus _keterangan;

    public KartuStat(string judul, Action<Graphics, RectangleF, Color>? ikon = null, Color? warna = null)
    {
        Margin = Padding.Empty;
        Padding = Tema.P(18, 16, 18, 16);
        var atas = new Baris { Jarak = Tema.S(8) };
        if (ikon is not null)
            atas.Tambah(new IkonBulat(ikon, warna ?? Tema.Biru, Tema.Alfa(warna ?? Tema.Biru, 28))
            {
                Size = new Size(Tema.S(30), Tema.S(30)), Margin = Padding.Empty,
            });
        _judul = Teks.Redup(judul, 9.5f);
        _judul.Margin = Padding.Empty;
        atas.Tambah(_judul);
        _nilai = Teks.Buat("—", Tema.Font(19, semibold: true), Tema.Teks);
        _nilai.Margin = new Padding(0, Tema.S(10), 0, Tema.S(2));
        _keterangan = Teks.Bungkus("", Tema.Font(8.5f), Tema.TeksRedup);
        _keterangan.Margin = Padding.Empty;
        Controls.AddRange([atas, _nilai, _keterangan]);
    }

    public void Atur(string nilai, string keterangan = "")
    {
        _nilai.Text = nilai;
        _keterangan.Text = keterangan;
        AccessibleName = $"{_judul.Text}: {nilai}";
    }
}

/// <summary>Garis pemisah horizontal tipis.</summary>
public class Pemisah : Control
{
    public Pemisah()
    {
        Height = 1;
        BackColor = Tema.GarisHalus;
        Margin = new Padding(0, Tema.S(8), 0, Tema.S(14));
    }
}
