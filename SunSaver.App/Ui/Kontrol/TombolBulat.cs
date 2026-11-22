using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace SunSaver.App.Ui.Kontrol;

public enum GayaTombol { Utama, Sekunder, Teks, Pil }

/// <summary>
/// Tombol bergaya Material (pill). Turunan <see cref="Button"/> sehingga keyboard (Enter/Spasi),
/// fokus Tab, dan aksesibilitas (screen reader) tetap berfungsi seperti tombol standar.
/// </summary>
public class TombolBulat : Button
{
    private bool _hover, _tekan, _terpilih;
    private GayaTombol _gaya = GayaTombol.Utama;

    public TombolBulat(string teks = "", GayaTombol gaya = GayaTombol.Utama)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = Tema.FontTombol;
        Text = teks;
        AccessibleName = teks;
        _gaya = gaya;
        Height = Tema.S(40);
        Pas();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GayaTombol Gaya { get => _gaya; set { _gaya = value; Invalidate(); } }

    /// <summary>Untuk tombol segmen/pil (mis. pilihan "Beli tunai" vs "Cicilan").</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Terpilih { get => _terpilih; set { _terpilih = value; Invalidate(); } }

    /// <summary>Ikon kecil di kiri teks (digambar vektor).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<Graphics, RectangleF, Color>? Ikon { get; set; }

    /// <summary>Atur lebar agar pas dengan teks + padding.</summary>
    public TombolBulat Pas(int minimal = 0)
    {
        var ukuran = TextRenderer.MeasureText(Text, Font);
        var lebar = ukuran.Width + Tema.S(Ikon is null ? 40 : 60);
        Width = Math.Max(lebar, minimal);
        return this;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        AccessibleName = Text;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _tekan = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _tekan = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _tekan = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs pe)
    {
        var g = pe.Graphics;
        g.Clear(Parent?.BackColor ?? Tema.Latar);
        Tema.Halus(g);

        var (isi, garis, teks) = Warna();
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Tema.Bulat(r, r.Height / 2))
        {
            if (isi.A > 0) using (var b = new SolidBrush(isi)) g.FillPath(b, path);
            if (garis.A > 0) using (var p = new Pen(garis, Tema.S(1f))) g.DrawPath(p, path);
        }

        var areaTeks = ClientRectangle;
        if (Ikon is not null)
        {
            var ukTeks = TextRenderer.MeasureText(Text, Font);
            var uk = Tema.S(18f);
            var total = uk + Tema.S(8) + ukTeks.Width;
            var x = (Width - total) / 2f;
            Ikon(g, new RectangleF(x, (Height - uk) / 2f, uk, uk), teks);
            areaTeks = new Rectangle((int)(x + uk + Tema.S(8)), 0, ukTeks.Width + 2, Height);
        }
        TextRenderer.DrawText(g, Text, Font, areaTeks, teks,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        if (Focused && ShowFocusCues)
        {
            using var p = new Pen(Tema.Alfa(Tema.Biru, 160), Tema.S(2f));
            using var path = Tema.Bulat(new RectangleF(2, 2, Width - 5, Height - 5), (Height - 5) / 2f);
            g.DrawPath(p, path);
        }
    }

    private (Color Isi, Color Garis, Color Teks) Warna()
    {
        if (!Enabled)
            return _gaya == GayaTombol.Utama
                ? (Tema.GarisHalus, Color.Empty, Tema.TeksPudar)
                : (Color.Empty, Tema.GarisHalus, Tema.TeksPudar);

        return _gaya switch
        {
            GayaTombol.Utama => (_tekan ? Tema.BiruTekan : _hover ? Tema.BiruHover : Tema.Biru, Color.Empty, Color.White),
            GayaTombol.Sekunder => (_tekan ? Tema.Hex("#D2E3FC") : _hover ? Tema.Hex("#F6FAFE") : Color.White, Tema.Garis, Tema.Biru),
            GayaTombol.Teks => (_tekan ? Tema.Hex("#D2E3FC") : _hover ? Tema.BiruMuda : Color.Empty, Color.Empty, Tema.Biru),
            GayaTombol.Pil when _terpilih => (Tema.BiruMuda, Tema.Biru, Tema.BiruTekan),
            GayaTombol.Pil => (_hover ? Tema.LatarAbu : Color.White, Tema.Garis, Tema.TeksRedup),
            _ => (Tema.Biru, Color.Empty, Color.White),
        };
    }
}
