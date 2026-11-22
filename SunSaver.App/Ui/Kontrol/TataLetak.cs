using System.ComponentModel;

namespace SunSaver.App.Ui.Kontrol;

/// <summary>Kontainer yang bisa menghitung tingginya sendiri untuk lebar tertentu.</summary>
public interface ITataLetak
{
    int TinggiPreferensi(int lebar);
}

/// <summary>Utilitas pengukuran anak kontainer.</summary>
internal static class Ukur
{
    /// <summary>Apakah anak mempertahankan lebarnya sendiri (tidak direntangkan)?</summary>
    public static bool LebarTetap(Control c) => c is ButtonBase or IkonBulat or CheckBox or RadioButton
                                                || c is Label { AutoSize: true }
                                                || c.Tag is TandaLebarTetap;

    /// <summary>
    /// Lebar anak. Kontrol lebar-tetap selalu memakai lebar alaminya sendiri (tidak pernah dipersempit),
    /// karena tata letak awal bisa terjadi saat kontainer masih selebar 0 px.
    /// </summary>
    public static int Lebar(Control c, int tersedia)
    {
        if (c is Label { AutoSize: true }) return c.PreferredSize.Width;
        if (LebarTetap(c)) return c.Width;
        tersedia = Math.Max(1, tersedia);
        return c.MaximumSize.Width > 0 ? Math.Min(c.MaximumSize.Width, tersedia) : tersedia;
    }

    public static int Tinggi(Control c, int lebar)
    {
        lebar = Math.Max(1, lebar);
        return c switch
        {
            ITataLetak t => t.TinggiPreferensi(lebar),
            LabelBungkus l => TextRenderer.MeasureText(l.Text.Length == 0 ? " " : l.Text, l.Font, new Size(lebar - l.Padding.Horizontal, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + l.Padding.Vertical,
            Label { AutoSize: true } l => l.PreferredSize.Height,
            TextBoxBase or ComboBox when c.AutoSize => c.PreferredSize.Height,
            _ => c.Height,
        };
    }
}

/// <summary>Penanda (lewat <see cref="Control.Tag"/>) bahwa kontrol tidak boleh direntangkan.</summary>
public sealed class TandaLebarTetap
{
    public static readonly TandaLebarTetap Ya = new();
}

/// <summary>Tumpukan vertikal: anak direntangkan selebar kontainer, tinggi kontainer mengikuti isi.</summary>
public class Tumpukan : Panel, ITataLetak
{
    public Tumpukan()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        Margin = Padding.Empty;
        Padding = Padding.Empty;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool TinggiOtomatis { get; set; } = true;

    /// <summary>Lebar isi maksimum; bila kontainer lebih lebar, isi ditengahkan (0 = tanpa batas).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int LebarIsiMaks { get; set; }

    private (int Kiri, int Dalam) Isi(int lebar)
    {
        var dalam = lebar - Padding.Horizontal;
        if (LebarIsiMaks <= 0 || dalam <= LebarIsiMaks) return (Padding.Left, dalam);
        return (Padding.Left + (dalam - LebarIsiMaks) / 2, LebarIsiMaks);
    }

    public int TinggiPreferensi(int lebar)
    {
        var (_, dalam) = Isi(lebar);
        var y = Padding.Top;
        foreach (Control c in Controls)
        {
            if (!c.Visible) continue;
            var w = Ukur.Lebar(c, dalam - c.Margin.Horizontal);
            y += c.Margin.Vertical + Ukur.Tinggi(c, w);
        }
        return y + Padding.Bottom;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        var (kiri, dalam) = Isi(ClientSize.Width);
        var y = Padding.Top;
        foreach (Control c in Controls)
        {
            if (!c.Visible) continue;
            var w = Ukur.Lebar(c, dalam - c.Margin.Horizontal);
            var h = Ukur.Tinggi(c, w);
            c.SetBounds(kiri + c.Margin.Left, y + c.Margin.Top, w, h);
            y += c.Margin.Vertical + h;
        }
        var total = y + Padding.Bottom;
        if (Parent is GridKartu grid)
        {
            // Di dalam GridKartu tinggi ditentukan grid (kartu sebaris dibuat sama tinggi);
            // cukup minta grid menata ulang bila kebutuhan tinggi isi berubah.
            if (total != _tinggiTerakhir)
            {
                _tinggiTerakhir = total;
                grid.PerformLayout();
            }
        }
        else if (TinggiOtomatis && Dock is DockStyle.None or DockStyle.Top or DockStyle.Bottom && Height != total)
        {
            Height = total;
        }
    }

    private int _tinggiTerakhir = -1;

    protected override void OnControlAdded(ControlEventArgs e) { base.OnControlAdded(e); e.Control!.VisibleChanged += AnakBerubah; }
    protected override void OnControlRemoved(ControlEventArgs e) { base.OnControlRemoved(e); e.Control!.VisibleChanged -= AnakBerubah; }
    private void AnakBerubah(object? s, EventArgs e) => PerformLayout();
}

/// <summary>Baris horizontal. Anak "isi" berbagi sisa lebar sesuai bobot; anak lebar-tetap memakai lebarnya sendiri.</summary>
public class Baris : Panel, ITataLetak
{
    private readonly Dictionary<Control, float> _bobot = [];

    public Baris()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        Margin = Padding.Empty;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Jarak { get; set; } = Tema.S(12);

    /// <summary>Tengahkan anak secara vertikal (default) atau rata atas.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool RataTengah { get; set; } = true;

    /// <summary>Rata kanan untuk anak lebar-tetap bila tidak ada anak "isi".</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool RataKanan { get; set; }

    public Baris Tambah(Control c, float bobot = 0)
    {
        Controls.Add(c);
        if (bobot > 0) _bobot[c] = bobot;
        return this;
    }

    private List<(Control C, int W)> Hitung(int lebar)
    {
        var anak = Controls.Cast<Control>().Where(c => c.Visible).ToList();
        var hasil = new List<(Control, int)>(anak.Count);
        var jarakTotal = Jarak * Math.Max(0, anak.Count - 1);
        var tetap = anak.Where(c => !_bobot.ContainsKey(c)).Sum(c => Ukur.Lebar(c, lebar) + c.Margin.Horizontal);
        var sisa = Math.Max(0, lebar - Padding.Horizontal - jarakTotal - tetap);
        var totalBobot = anak.Where(_bobot.ContainsKey).Sum(c => _bobot[c]);
        foreach (var c in anak)
        {
            var w = _bobot.TryGetValue(c, out var b) && totalBobot > 0
                ? (int)(sisa * b / totalBobot) - c.Margin.Horizontal
                : Ukur.Lebar(c, lebar);
            hasil.Add((c, Math.Max(1, w)));
        }
        return hasil;
    }

    public int TinggiPreferensi(int lebar)
    {
        var maks = 0;
        foreach (var (c, w) in Hitung(lebar)) maks = Math.Max(maks, Ukur.Tinggi(c, w) + c.Margin.Vertical);
        return maks + Padding.Vertical;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        var tata = Hitung(ClientSize.Width);
        var tinggi = TinggiPreferensi(ClientSize.Width);
        if (Dock == DockStyle.None && Height != tinggi) Height = tinggi;

        var adaIsi = tata.Any(t => _bobot.ContainsKey(t.C));
        var totalLebar = tata.Sum(t => t.W + t.C.Margin.Horizontal) + Jarak * Math.Max(0, tata.Count - 1);
        var x = Padding.Left + (RataKanan && !adaIsi ? Math.Max(0, ClientSize.Width - Padding.Horizontal - totalLebar) : 0);
        var dalam = tinggi - Padding.Vertical;
        foreach (var (c, w) in tata)
        {
            var h = Ukur.Tinggi(c, w);
            var y = Padding.Top + c.Margin.Top + (RataTengah ? Math.Max(0, (dalam - c.Margin.Vertical - h) / 2) : 0);
            c.SetBounds(x + c.Margin.Left, y, w, h);
            x += c.Margin.Horizontal + w + Jarak;
        }
    }
}

/// <summary>
/// Grid kartu responsif: jumlah kolom berkurang otomatis bila lebar tak cukup
/// (<see cref="LebarMinKolom"/>), tinggi baris = anak tertinggi di baris itu.
/// </summary>
public class GridKartu : Panel, ITataLetak
{
    public GridKartu()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        Margin = new Padding(0, 0, 0, Tema.S(16));
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Kolom { get; set; } = 3;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int LebarMinKolom { get; set; } = Tema.S(240);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Jarak { get; set; } = Tema.S(16);

    /// <summary>Bobot lebar per kolom (mis. [2, 1] untuk kolom kiri 2Ã— lebih lebar). Null = sama rata.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float[]? BobotKolom { get; set; }

    private int KolomEfektif(int lebar)
    {
        var k = Math.Max(1, Kolom);
        while (k > 1 && (lebar - Jarak * (k - 1)) / k < LebarMinKolom) k--;
        return k;
    }

    private int[] LebarKolom(int lebar, int k)
    {
        var bersih = lebar - Jarak * (k - 1);
        var bobot = BobotKolom is { } b && b.Length == k ? b : Enumerable.Repeat(1f, k).ToArray();
        var total = bobot.Sum();
        return bobot.Select(x => (int)(bersih * x / total)).ToArray();
    }

    private IEnumerable<(Control C, int Kol, int Brs)> Sel(int k)
    {
        var i = 0;
        foreach (Control c in Controls)
        {
            if (!c.Visible) continue;
            yield return (c, i % k, i / k);
            i++;
        }
    }

    public int TinggiPreferensi(int lebar)
    {
        var k = KolomEfektif(lebar);
        var lk = LebarKolom(lebar, k);
        var tinggiBaris = Sel(k).GroupBy(s => s.Brs).Select(g => g.Max(s => Ukur.Tinggi(s.C, lk[s.Kol]))).ToList();
        return tinggiBaris.Sum() + Jarak * Math.Max(0, tinggiBaris.Count - 1);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        var lebar = ClientSize.Width;
        var k = KolomEfektif(lebar);
        var lk = LebarKolom(lebar, k);
        var y = 0;
        foreach (var baris in Sel(k).GroupBy(s => s.Brs))
        {
            var tinggi = baris.Max(s => Ukur.Tinggi(s.C, lk[s.Kol]));
            foreach (var (c, kol, _) in baris)
            {
                var x = lk.Take(kol).Sum() + Jarak * kol;
                c.SetBounds(x, y, lk[kol], tinggi);
            }
            y += tinggi + Jarak;
        }
        var total = Math.Max(0, y - Jarak);
        if (Dock == DockStyle.None && Height != total) Height = total;
    }
}

/// <summary>
/// Area bergulir vertikal untuk isi halaman: anak ditumpuk, direntangkan hingga
/// <see cref="LebarMaks"/> dan ditengahkan seperti halaman web.
/// </summary>
public class PanelGulir : Panel
{
    private bool _mengatur;

    public PanelGulir()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        AutoScroll = true;
        Dock = DockStyle.Fill;
        BackColor = Tema.Latar;
        Padding = Tema.P(32, 24, 32, 40);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int LebarMaks { get; set; } = Tema.S(1180);

    protected override void OnLayout(LayoutEventArgs levent)
    {
        if (_mengatur) return;
        _mengatur = true;
        try
        {
            var tersedia = ClientSize.Width - Padding.Horizontal;
            var lebar = Math.Max(Tema.S(300), Math.Min(tersedia, LebarMaks));
            var x = Padding.Left + Math.Max(0, (tersedia - lebar) / 2);
            var gulir = AutoScrollPosition; // negatif saat digulir
            var y = Padding.Top;
            foreach (Control c in Controls)
            {
                if (!c.Visible) continue;
                var w = c.Dock == DockStyle.None && !Ukur.LebarTetap(c) ? lebar - c.Margin.Horizontal : Ukur.Lebar(c, lebar);
                var h = Ukur.Tinggi(c, w);
                c.SetBounds(x + c.Margin.Left, y + c.Margin.Top + gulir.Y, w, h);
                y += c.Margin.Vertical + h;
            }
            var minimal = new Size(0, y + Padding.Bottom);
            if (AutoScrollMinSize != minimal) AutoScrollMinSize = minimal;

            // ScrollableControl.OnLayout menyesuaikan scrollbar terhadap AutoScrollMinSize & posisi anak.
            base.OnLayout(levent);
        }
        finally
        {
            _mengatur = false;
        }
    }

    /// <summary>Roda mouse menggulir walau kursor berada di atas anak yang tidak bisa difokus.</summary>
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (!ContainsFocus && FindForm()?.ActiveControl is not (TextBoxBase or ComboBox or NumericUpDown)) Focus();
    }

    protected override void OnControlAdded(ControlEventArgs e) { base.OnControlAdded(e); e.Control!.VisibleChanged += AnakBerubah; }
    protected override void OnControlRemoved(ControlEventArgs e) { base.OnControlRemoved(e); e.Control!.VisibleChanged -= AnakBerubah; }
    private void AnakBerubah(object? s, EventArgs e) => PerformLayout();

    /// <summary>Gulir ke atas (dipakai saat halaman ditampilkan ulang).</summary>
    public void KeAtas() => AutoScrollPosition = Point.Empty;
}
