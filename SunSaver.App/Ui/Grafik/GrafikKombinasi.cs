using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace SunSaver.App.Ui.Grafik;

public enum JenisSeri { Batang, Garis, Area }

/// <summary>Satu seri data pada grafik.</summary>
public sealed record Seri(string Nama, double[] Nilai, Color Warna, JenisSeri Jenis = JenisSeri.Batang, bool SumbuKanan = false)
{
    public bool PutusPutus { get; init; }
}

/// <summary>
/// Grafik kombinasi batang/garis/area dengan dua sumbu Y, legenda, dan tooltip saat kursor
/// diarahkan. Digambar manual dengan GDI+ sehingga tidak butuh paket grafik pihak ketiga.
/// </summary>
public class GrafikKombinasi : Control
{
    private string[] _kategori = [];
    private readonly List<Seri> _seri = [];
    private int _sorot = -1;
    private Rectangle _areaPlot;

    public GrafikKombinasi()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        Height = Tema.S(280);
        Font = Tema.Font(8.5f);
        TabStop = true;
        AccessibleRole = AccessibleRole.Chart;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<double, string> FormatKiri { get; set; } = v => Tema.N(v, Math.Abs(v) < 10 ? 1 : 0);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<double, string> FormatKanan { get; set; } = v => Tema.N(v, Math.Abs(v) < 10 ? 1 : 0);

    /// <summary>Format nilai pada tooltip per seri (default: format sumbu masing-masing).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Seri, double, string>? FormatTooltip { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? JudulSumbuKiri { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? JudulSumbuKanan { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string PesanKosong { get; set; } = "Memuat dataâ€¦";

    /// <summary>Kategori yang ditandai (mis. hari ini / bulan berjalan).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int IndeksPenanda { get; set; } = -1;

    public void Atur(string[] kategori, params Seri[] seri)
    {
        _kategori = kategori;
        _seri.Clear();
        _seri.AddRange(seri);
        _sorot = -1;
        AccessibleDescription = string.Join("; ", seri.Select(s => $"{s.Nama}: {string.Join(", ", s.Nilai.Select(v => FormatKiri(v)))}"));
        Invalidate();
    }

    public void Kosongkan(string pesan)
    {
        PesanKosong = pesan;
        _kategori = [];
        _seri.Clear();
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var idx = IndeksDi(e.X);
        if (idx != _sorot) { _sorot = idx; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _sorot = -1;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_kategori.Length == 0) return;
        if (e.KeyCode == Keys.Right) _sorot = Math.Min(_kategori.Length - 1, _sorot + 1);
        else if (e.KeyCode == Keys.Left) _sorot = Math.Max(0, _sorot < 0 ? 0 : _sorot - 1);
        else return;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); _sorot = -1; Invalidate(); }

    private int IndeksDi(int x)
    {
        if (_kategori.Length == 0 || _areaPlot.Width <= 0 || x < _areaPlot.Left || x > _areaPlot.Right) return -1;
        var slot = _areaPlot.Width / (float)_kategori.Length;
        return Math.Clamp((int)((x - _areaPlot.Left) / slot), 0, _kategori.Length - 1);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Color.White);
        Tema.Halus(g);

        if (_kategori.Length == 0 || _seri.Count == 0)
        {
            TextRenderer.DrawText(g, PesanKosong, Tema.Font(10), ClientRectangle, Tema.TeksPudar,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var kiri = _seri.Where(s => !s.SumbuKanan).ToList();
        var kanan = _seri.Where(s => s.SumbuKanan).ToList();
        var (minK, maksK, langkahK) = Skala(kiri);
        var (minR, maksR, langkahR) = Skala(kanan);

        // ---------- legenda ----------
        var tinggiLegenda = Tema.S(26);
        var xl = 0f;
        foreach (var s in _seri)
        {
            var uk = TextRenderer.MeasureText(s.Nama, Font);
            using (var b = new SolidBrush(s.Warna))
            {
                if (s.Jenis == JenisSeri.Batang)
                    g.FillRectangle(b, xl, Tema.S(6), Tema.S(10), Tema.S(10));
                else
                {
                    using var p = new Pen(s.Warna, Tema.S(2.5f)) { DashStyle = s.PutusPutus ? DashStyle.Dash : DashStyle.Solid };
                    g.DrawLine(p, xl, Tema.S(11), xl + Tema.S(14), Tema.S(11));
                }
            }
            TextRenderer.DrawText(g, s.Nama, Font, new Point((int)xl + Tema.S(18), Tema.S(3)), Tema.TeksRedup);
            xl += Tema.S(18) + uk.Width + Tema.S(16);
        }

        // ---------- area plot ----------
        var labelKiri = LabelSumbu(minK, maksK, langkahK, FormatKiri);
        var labelKanan = kanan.Count > 0 ? LabelSumbu(minR, maksR, langkahR, FormatKanan) : [];
        var lebarKiri = labelKiri.Max(l => TextRenderer.MeasureText(l.Teks, Font).Width) + Tema.S(8);
        var lebarKanan = labelKanan.Length > 0 ? labelKanan.Max(l => TextRenderer.MeasureText(l.Teks, Font).Width) + Tema.S(8) : Tema.S(8);
        var atasJudul = JudulSumbuKiri is not null || JudulSumbuKanan is not null ? Tema.S(16) : 0;
        _areaPlot = new Rectangle(lebarKiri, tinggiLegenda + atasJudul + Tema.S(6),
            Math.Max(10, Width - lebarKiri - lebarKanan), Math.Max(10, Height - tinggiLegenda - atasJudul - Tema.S(6) - Tema.S(24)));
        var a = _areaPlot;

        if (JudulSumbuKiri is not null)
            TextRenderer.DrawText(g, JudulSumbuKiri, Font, new Point(0, tinggiLegenda), Tema.TeksPudar);
        if (JudulSumbuKanan is not null && kanan.Count > 0)
        {
            var uk = TextRenderer.MeasureText(JudulSumbuKanan, Font);
            TextRenderer.DrawText(g, JudulSumbuKanan, Font, new Point(Width - uk.Width, tinggiLegenda), Tema.TeksPudar);
        }

        float YKiri(double v) => (float)(a.Bottom - (v - minK) / (maksK - minK) * a.Height);
        float YKanan(double v) => (float)(a.Bottom - (v - minR) / (maksR - minR) * a.Height);

        // garis bantu + label sumbu
        using (var pGaris = new Pen(Tema.GarisHalus, 1f))
        {
            foreach (var (nilai, teks) in labelKiri)
            {
                var y = YKiri(nilai);
                g.DrawLine(pGaris, a.Left, y, a.Right, y);
                var uk = TextRenderer.MeasureText(teks, Font);
                TextRenderer.DrawText(g, teks, Font, new Point(a.Left - uk.Width - Tema.S(4), (int)(y - uk.Height / 2f)), Tema.TeksPudar);
            }
            foreach (var (nilai, teks) in labelKanan)
            {
                var y = YKanan(nilai);
                var uk = TextRenderer.MeasureText(teks, Font);
                TextRenderer.DrawText(g, teks, Font, new Point(a.Right + Tema.S(4), (int)(y - uk.Height / 2f)), Tema.TeksPudar);
            }
        }
        if (minK < 0)
        {
            using var pNol = new Pen(Tema.TeksPudar, 1f);
            g.DrawLine(pNol, a.Left, YKiri(0), a.Right, YKiri(0));
        }

        var n = _kategori.Length;
        var slot = a.Width / (float)n;

        // penanda & sorotan kategori
        if (IndeksPenanda >= 0 && IndeksPenanda < n)
            using (var b = new SolidBrush(Tema.Alfa(Tema.Kuning, 40)))
                g.FillRectangle(b, a.Left + slot * IndeksPenanda, a.Top, slot, a.Height);
        if (_sorot >= 0)
            using (var b = new SolidBrush(Tema.Alfa(Tema.Biru, 18)))
                g.FillRectangle(b, a.Left + slot * _sorot, a.Top, slot, a.Height);

        // label kategori (dilewati bila terlalu rapat)
        var lebarLabel = _kategori.Max(k => TextRenderer.MeasureText(k, Font).Width) + Tema.S(6);
        var lompat = Math.Max(1, (int)Math.Ceiling(lebarLabel / slot));
        for (var i = 0; i < n; i += lompat)
        {
            var r = new Rectangle((int)(a.Left + slot * i - slot), a.Bottom + Tema.S(4), (int)(slot * 3), Tema.S(18));
            TextRenderer.DrawText(g, _kategori[i], Font, r, i == IndeksPenanda ? Tema.Teks : Tema.TeksRedup,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
        }

        // batang (dikelompokkan per sumbu)
        var batang = _seri.Where(s => s.Jenis == JenisSeri.Batang).ToList();
        if (batang.Count > 0)
        {
            var grup = Math.Min(slot * 0.72f, Tema.S(60f) * batang.Count);
            var lebar = grup / batang.Count;
            for (var j = 0; j < batang.Count; j++)
            {
                var s = batang[j];
                using var b = new SolidBrush(s.Warna);
                for (var i = 0; i < Math.Min(n, s.Nilai.Length); i++)
                {
                    var x = a.Left + slot * i + (slot - grup) / 2 + lebar * j;
                    var y0 = s.SumbuKanan ? YKanan(Math.Max(0, minR)) : YKiri(Math.Max(0, minK));
                    var y1 = s.SumbuKanan ? YKanan(s.Nilai[i]) : YKiri(s.Nilai[i]);
                    var r = RectangleF.FromLTRB(x + 1, Math.Min(y0, y1), x + lebar - 1, Math.Max(y0, y1));
                    if (r.Height < 0.5f) continue;
                    using var path = TopBulat(r, Math.Min(Tema.S(3f), r.Width / 3), y1 <= y0);
                    g.FillPath(b, path);
                }
            }
        }

        // area & garis
        foreach (var s in _seri.Where(s => s.Jenis != JenisSeri.Batang))
        {
            var titik = new PointF[Math.Min(n, s.Nilai.Length)];
            for (var i = 0; i < titik.Length; i++)
                titik[i] = new PointF(a.Left + slot * i + slot / 2, s.SumbuKanan ? YKanan(s.Nilai[i]) : YKiri(s.Nilai[i]));
            if (titik.Length == 0) continue;

            if (s.Jenis == JenisSeri.Area && titik.Length > 1)
            {
                var dasar = s.SumbuKanan ? YKanan(Math.Max(0, minR)) : YKiri(Math.Max(0, minK));
                using var path = new GraphicsPath();
                path.AddLine(titik[0].X, dasar, titik[0].X, titik[0].Y);
                path.AddLines(titik);
                path.AddLine(titik[^1].X, titik[^1].Y, titik[^1].X, dasar);
                path.CloseFigure();
                using var lg = new LinearGradientBrush(new RectangleF(a.Left, a.Top, a.Width, a.Height + 1),
                    Tema.Alfa(s.Warna, 90), Tema.Alfa(s.Warna, 10), LinearGradientMode.Vertical);
                g.FillPath(lg, path);
            }

            using var pen = new Pen(s.Warna, Tema.S(2.25f)) { LineJoin = LineJoin.Round, DashStyle = s.PutusPutus ? DashStyle.Dash : DashStyle.Solid };
            if (titik.Length > 1) g.DrawLines(pen, titik);
            if (titik.Length <= 31)
            {
                using var pTitik = new Pen(s.Warna, Tema.S(2f));
                var d = Tema.S(6f);
                foreach (var t in titik)
                {
                    g.FillEllipse(Brushes.White, t.X - d / 2, t.Y - d / 2, d, d);
                    g.DrawEllipse(pTitik, t.X - d / 2, t.Y - d / 2, d, d);
                }
            }
        }

        if (_sorot >= 0) GambarTooltip(g, _sorot, slot);
    }

    private void GambarTooltip(Graphics g, int i, float slot)
    {
        var baris = new List<(Color Warna, string Teks)>();
        foreach (var s in _seri)
        {
            if (i >= s.Nilai.Length) continue;
            var nilai = FormatTooltip?.Invoke(s, s.Nilai[i]) ?? (s.SumbuKanan ? FormatKanan(s.Nilai[i]) : FormatKiri(s.Nilai[i]));
            baris.Add((s.Warna, $"{s.Nama}: {nilai}"));
        }
        var judul = _kategori[i];
        var fontJudul = Tema.Font(9, semibold: true);
        var lebar = Math.Max(TextRenderer.MeasureText(judul, fontJudul).Width,
            baris.Count == 0 ? 0 : baris.Max(b => TextRenderer.MeasureText(b.Teks, Font).Width + Tema.S(16))) + Tema.S(20);
        var tinggiBaris = Tema.S(18);
        var tinggi = Tema.S(12) + tinggiBaris * (baris.Count + 1);
        var x = _areaPlot.Left + slot * i + slot / 2 + Tema.S(12);
        if (x + lebar > Width) x = _areaPlot.Left + slot * i + slot / 2 - lebar - Tema.S(12);
        var r = new RectangleF(Math.Max(0, x), _areaPlot.Top + Tema.S(4), lebar, tinggi);

        using (var bayang = Tema.Bulat(new RectangleF(r.X + 1, r.Y + 2, r.Width, r.Height), Tema.S(8)))
        using (var bb = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            g.FillPath(bb, bayang);
        using (var path = Tema.Bulat(r, Tema.S(8)))
        {
            g.FillPath(Brushes.White, path);
            using var p = new Pen(Tema.Garis);
            g.DrawPath(p, path);
        }
        var y = (int)r.Y + Tema.S(6);
        TextRenderer.DrawText(g, judul, fontJudul, new Point((int)r.X + Tema.S(10), y), Tema.Teks);
        foreach (var (warna, teks) in baris)
        {
            y += tinggiBaris;
            using (var b = new SolidBrush(warna)) g.FillEllipse(b, r.X + Tema.S(10), y + Tema.S(4), Tema.S(8), Tema.S(8));
            TextRenderer.DrawText(g, teks, Font, new Point((int)r.X + Tema.S(24), y), Tema.TeksRedup);
        }
    }

    private static GraphicsPath TopBulat(RectangleF r, float radius, bool atas)
    {
        var p = new GraphicsPath();
        if (radius < 0.5f) { p.AddRectangle(r); return p; }
        var d = radius * 2;
        if (atas)
        {
            p.AddLine(r.Left, r.Bottom, r.Left, r.Top + radius);
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddLine(r.Right, r.Top + radius, r.Right, r.Bottom);
        }
        else
        {
            p.AddLine(r.Left, r.Top, r.Right, r.Top);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        }
        p.CloseFigure();
        return p;
    }

    /// <summary>Rentang sumbu "rapi" (kelipatan 1/2/2,5/5 Ã— 10â¿), selalu menyertakan nol.</summary>
    private static (double Min, double Maks, double Langkah) Skala(List<Seri> seri)
    {
        var nilai = seri.SelectMany(s => s.Nilai).Where(double.IsFinite).ToList();
        if (nilai.Count == 0) return (0, 1, 0.25);
        double min = Math.Min(0, nilai.Min()), maks = Math.Max(0, nilai.Max());
        if (maks - min < 1e-9) maks = min + 1;
        var langkah = LangkahRapi((maks - min) / 4);
        min = Math.Floor(min / langkah) * langkah;
        maks = Math.Ceiling(maks * 1.05 / langkah) * langkah;
        return (min, maks, langkah);
    }

    private static double LangkahRapi(double kasar)
    {
        var pangkat = Math.Pow(10, Math.Floor(Math.Log10(kasar)));
        foreach (var m in new[] { 1, 2, 2.5, 5, 10 })
            if (m * pangkat >= kasar) return m * pangkat;
        return 10 * pangkat;
    }

    private static (double Nilai, string Teks)[] LabelSumbu(double min, double maks, double langkah, Func<double, string> format)
    {
        var hasil = new List<(double, string)>();
        for (var v = min; v <= maks + langkah * 0.01; v += langkah)
            hasil.Add((v, format(Math.Abs(v) < langkah * 1e-6 ? 0 : v)));
        return hasil.ToArray();
    }
}
