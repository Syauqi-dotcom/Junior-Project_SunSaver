using System.ComponentModel;
using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App.Ui.Grafik;

/// <summary>
/// Visualisasi gamifikasi "setara pohon": satu ikon per pohon, pohon terakhir yang belum utuh
/// digambar transparan sebagian. Bila lebih dari kapasitas, ditampilkan "+N".
/// </summary>
public class HutanVirtual : Control
{
    private double _pohon;

    public HutanVirtual()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint, true);
        Height = Tema.S(120);
        AccessibleRole = AccessibleRole.Graphic;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double JumlahPohon
    {
        get => _pohon;
        set
        {
            _pohon = Math.Max(0, value);
            AccessibleName = $"Setara {Tema.N(_pohon, 1)} pohon";
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Color.White);
        Tema.Halus(g);

        var uk = Tema.S(26f);
        var jarak = Tema.S(6f);
        var kolom = Math.Max(1, (int)((Width + jarak) / (uk + jarak)));
        var baris = Math.Max(1, (int)((Height + jarak) / (uk + jarak)));
        var kapasitas = kolom * baris;

        var utuh = (int)Math.Floor(_pohon);
        var sisa = _pohon - utuh;
        var tampil = Math.Min(kapasitas, (int)Math.Ceiling(_pohon));
        var lebih = (int)Math.Ceiling(_pohon) - tampil;
        if (lebih > 0) tampil = kapasitas - 1;

        for (var i = 0; i < kapasitas; i++)
        {
            var r = new RectangleF(i % kolom * (uk + jarak), i / kolom * (uk + jarak), uk, uk);
            if (i < tampil)
            {
                var sebagian = i == utuh && sisa > 0 && lebih == 0;
                var warna = sebagian ? Tema.Alfa(Tema.HijauTerang, (int)(60 + 140 * sisa)) : (i % 3 == 1 ? Tema.Hijau : Tema.HijauTerang);
                Ikon.Pohon(g, r, warna);
            }
            else if (i == tampil && lebih > 0)
            {
                TextRenderer.DrawText(g, "+" + Tema.N(lebih + 1), Tema.Font(9, semibold: true), Rectangle.Round(r), Tema.Hijau,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoClipping);
            }
            else
            {
                using var p = new Pen(Tema.GarisHalus, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
                g.DrawEllipse(p, r.X + uk * 0.25f, r.Y + uk * 0.25f, uk * 0.5f, uk * 0.5f);
            }
        }
    }
}
