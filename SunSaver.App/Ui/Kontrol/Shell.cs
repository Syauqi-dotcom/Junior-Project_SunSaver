using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace SunSaver.App.Ui.Kontrol;

/// <summary>Tautan navigasi di header; tautan aktif berwarna biru dengan garis bawah.</summary>
public class NavTautan : Button
{
    private bool _hover, _aktif;

    public NavTautan(string teks)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Text = teks;
        Font = Tema.Font(10, semibold: true);
        Cursor = Cursors.Hand;
        Height = Tema.S(64) - 1; // sisakan 1 px untuk garis bawah header
        Width = TextRenderer.MeasureText(teks, Font).Width + Tema.S(28);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Aktif
    {
        get => _aktif;
        set
        {
            _aktif = value;
            AccessibleDescription = value ? "halaman aktif" : null;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs pe)
    {
        var g = pe.Graphics;
        g.Clear(Parent?.BackColor ?? Color.White);
        Tema.Halus(g);
        if (_hover && !_aktif)
        {
            using var path = Tema.Bulat(new RectangleF(Tema.S(4), Height / 2f - Tema.S(18), Width - Tema.S(8), Tema.S(36)), Tema.S(18));
            using var b = new SolidBrush(Tema.LatarAbu);
            g.FillPath(b, path);
        }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, _aktif ? Tema.Biru : Tema.TeksRedup,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        if (_aktif)
        {
            using var b = new SolidBrush(Tema.Biru);
            using var path = Tema.Bulat(new RectangleF(Tema.S(10), Height - Tema.S(3), Width - Tema.S(20), Tema.S(6)), Tema.S(3));
            g.FillPath(b, path);
        }
        if (Focused && ShowFocusCues)
        {
            using var p = new Pen(Tema.Alfa(Tema.Biru, 160), Tema.S(2f));
            g.DrawRectangle(p, Tema.S(4), Tema.S(14), Width - Tema.S(8), Height - Tema.S(28));
        }
    }
}

/// <summary>Logo SunSaver: matahari + nama aplikasi.</summary>
public class Logo : Control
{
    public Logo()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        Size = new Size(Tema.S(170), Tema.S(64) - 1);
        AccessibleName = "SunSaver, ke beranda";
        AccessibleRole = AccessibleRole.Link;
        TabStop = false;
    }

    public static void GambarMatahari(Graphics g, RectangleF r)
    {
        using var lg = new LinearGradientBrush(r, Tema.Kuning, Tema.Oranye, 45f);
        var inti = r.Width * 0.5f;
        g.FillEllipse(lg, r.X + (r.Width - inti) / 2, r.Y + (r.Height - inti) / 2, inti, inti);
        using var p = new Pen(Tema.Kuning, Math.Max(1.5f, r.Width / 12)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var cx = r.X + r.Width / 2; var cy = r.Y + r.Height / 2;
        for (var i = 0; i < 8; i++)
        {
            var a = i * Math.PI / 4 + Math.PI / 8;
            float r1 = r.Width * 0.34f, r2 = r.Width * 0.48f;
            g.DrawLine(p, cx + (float)Math.Cos(a) * r1, cy + (float)Math.Sin(a) * r1, cx + (float)Math.Cos(a) * r2, cy + (float)Math.Sin(a) * r2);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Color.White);
        Tema.Halus(g);
        var uk = Tema.S(30f);
        GambarMatahari(g, new RectangleF(0, (Height - uk) / 2f, uk, uk));
        var f1 = Tema.Font(15, semibold: true);
        var w1 = TextRenderer.MeasureText(g, "Sun", f1, Size.Empty, TextFormatFlags.NoPadding).Width;
        var y = (Height - TextRenderer.MeasureText("Sun", f1).Height) / 2;
        TextRenderer.DrawText(g, "Sun", f1, new Point((int)uk + Tema.S(8), y), Tema.Teks, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "Saver", f1, new Point((int)uk + Tema.S(8) + w1, y), Tema.OranyeTua, TextFormatFlags.NoPadding);
    }
}

/// <summary>Notifikasi singkat di bawah layar (Material snackbar), hilang otomatis.</summary>
public class Snackbar : Control
{
    private readonly System.Windows.Forms.Timer _timer = new();
    private bool _galat;

    public Snackbar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Visible = false;
        Font = Tema.Font(10);
        AccessibleRole = AccessibleRole.Alert;
        _timer.Tick += (_, _) => { _timer.Stop(); Visible = false; };
        Click += (_, _) => { _timer.Stop(); Visible = false; };
        Cursor = Cursors.Hand;
    }

    public void Tampil(string pesan, bool galat)
    {
        Text = pesan;
        AccessibleName = pesan;
        _galat = galat;
        var maks = Math.Max(Tema.S(300), (Parent?.ClientSize.Width ?? 800) - Tema.S(80));
        var uk = TextRenderer.MeasureText(pesan, Font, new Size(Math.Min(maks, Tema.S(640)), 0), TextFormatFlags.WordBreak);
        Size = new Size(uk.Width + Tema.S(48), uk.Height + Tema.S(28));
        if (Parent is { } p) Location = new Point((p.ClientSize.Width - Width) / 2, p.ClientSize.Height - Height - Tema.S(28));
        using (var path = Tema.Bulat(new RectangleF(0, 0, Width, Height), Tema.S(8))) Region = new Region(path);
        Visible = true;
        BringToFront();
        Invalidate();
        _timer.Stop();
        _timer.Interval = galat ? 6500 : 3500;
        _timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_galat ? Tema.Hex("#B3261E") : Tema.Hex("#323232"));
        TextRenderer.DrawText(g, Text, Font, new Rectangle(Tema.S(24), 0, Width - Tema.S(48), Height), Color.White,
            TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.Left);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
