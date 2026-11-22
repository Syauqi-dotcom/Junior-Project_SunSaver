using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using SunSaver.App.Core;
using SunSaver.App.Core.Model;
using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App.Ui.Peta;

public enum LapisanPeta { Satelit, Jalan }

/// <summary>
/// Peta "slippy map" berbasis tile (Web Mercator) dengan citra satelit Esri World Imagery atau
/// OpenStreetMap. Mendukung geser (drag), zoom roda mouse/keyboard, penanda lokasi, dan mode
/// <b>gambar atap</b>: klik sudut-sudut atap untuk membuat poligon, luasnya dihitung otomatis
/// dan diwarnai seperti heatmap potensi surya Project Sunroof.
/// </summary>
public sealed class PetaSatelit : Control
{
    private const int UkuranTile = 256;
    private const int MaksCacheMemori = 450;
    private const double LatMaks = 85.05112878;

    private readonly Dictionary<string, Image> _cache = [];
    private readonly LinkedList<string> _urutanLru = new();
    private readonly HashSet<string> _diambil = [];
    private readonly HashSet<string> _tidakAda = [];
    private readonly Dictionary<string, DateTime> _gagal = [];
    private readonly SemaphoreSlim _batasUnduh = new(6, 6);
    private readonly ContextMenuStrip _menu;

    private double _cx, _cy; // pusat dalam koordinat piksel dunia pada zoom saat ini
    private int _zoom = 5;
    private LapisanPeta _lapisan = LapisanPeta.Satelit;
    private TitikGeo? _penanda;

    private bool _seret, _sudahDigeser;
    private Point _titikSeret;
    private (double X, double Y) _pusatSeret;
    private Point? _kursor;
    private Point _titikMenu;

    private readonly List<TitikGeo> _poligon = [];
    private bool _modeGambar;

    public PetaSatelit()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "Peta satelit lokasi rumah";
        Pusatkan(new TitikGeo(-2.5, 118), 5);

        _menu = new ContextMenuStrip { Font = Tema.Font(9.5f), ShowImageMargin = false };
        _menu.Opening += (_, _) => SusunMenu();
    }

    // ------------------------------------------------------------------ properti

    public event EventHandler? PoligonBerubah;
    public event EventHandler? ModeGambarBerubah;
    public event EventHandler<TitikGeo>? LokasiDipilih;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ZoomMin { get; set; } = 3;

    public int ZoomMaks => _lapisan == LapisanPeta.Satelit ? 21 : 19;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Zoom
    {
        get => _zoom;
        set => AturZoom(value, new Point(Width / 2, Height / 2));
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TitikGeo Pusat
    {
        get => new(YKeLat(_cy, _zoom), XKeLon(_cx, _zoom));
        set => Pusatkan(value, _zoom);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LapisanPeta Lapisan
    {
        get => _lapisan;
        set
        {
            if (_lapisan == value) return;
            var pusat = Pusat;
            _lapisan = value;
            Pusatkan(pusat, Math.Min(_zoom, ZoomMaks));
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TitikGeo? Penanda { get => _penanda; set { _penanda = value; Invalidate(); } }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? Petunjuk { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ModeGambar
    {
        get => _modeGambar;
        set
        {
            if (_modeGambar == value) return;
            _modeGambar = value;
            Cursor = value ? Cursors.Cross : Cursors.Hand;
            if (!value && _poligon.Count is > 0 and < 3) { _poligon.Clear(); PoligonBerubah?.Invoke(this, EventArgs.Empty); }
            ModeGambarBerubah?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    public IReadOnlyList<TitikGeo> Poligon => _poligon;

    public bool PoligonLengkap => _poligon.Count >= 3 && !_modeGambar;

    /// <summary>Luas poligon atap (m²) — 0 bila belum ada poligon lengkap.</summary>
    public double LuasPoligonM2 => _poligon.Count >= 3 ? LuasM2(_poligon) : 0;

    public void Pusatkan(TitikGeo t, int zoom)
    {
        _zoom = Math.Clamp(zoom, ZoomMin, ZoomMaks);
        _cx = LonKeX(t.Lon, _zoom);
        _cy = LatKeY(t.Lat, _zoom);
        Invalidate();
    }

    public void MulaiGambar()
    {
        _poligon.Clear();
        PoligonBerubah?.Invoke(this, EventArgs.Empty);
        ModeGambar = true;
        Focus();
    }

    public void SelesaiGambar()
    {
        if (_poligon.Count >= 3) ModeGambar = false;
    }

    public void HapusPoligon()
    {
        _poligon.Clear();
        ModeGambar = false;
        PoligonBerubah?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    public void AturPoligon(IEnumerable<TitikGeo>? titik)
    {
        _poligon.Clear();
        if (titik is not null) _poligon.AddRange(titik);
        _modeGambar = false;
        Cursor = Cursors.Hand;
        PoligonBerubah?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    // ------------------------------------------------------------------ proyeksi Web Mercator

    private static double Dunia(int z) => UkuranTile * Math.Pow(2, z);
    public static double LonKeX(double lon, int z) => (lon + 180) / 360 * Dunia(z);

    public static double LatKeY(double lat, int z)
    {
        var r = Math.Clamp(lat, -LatMaks, LatMaks) * Math.PI / 180;
        return (1 - Math.Log(Math.Tan(r) + 1 / Math.Cos(r)) / Math.PI) / 2 * Dunia(z);
    }

    public static double XKeLon(double x, int z) => x / Dunia(z) * 360 - 180;
    public static double YKeLat(double y, int z) => Math.Atan(Math.Sinh(Math.PI - 2 * Math.PI * y / Dunia(z))) * 180 / Math.PI;

    private TitikGeo LayarKeGeo(Point p)
    {
        var x = _cx - Width / 2.0 + p.X;
        var y = _cy - Height / 2.0 + p.Y;
        return new TitikGeo(YKeLat(y, _zoom), XKeLon(x, _zoom));
    }

    private PointF GeoKeLayar(TitikGeo t)
        => new((float)(LonKeX(t.Lon, _zoom) - _cx + Width / 2.0), (float)(LatKeY(t.Lat, _zoom) - _cy + Height / 2.0));

    /// <summary>Luas poligon geodesik kecil (proyeksi equirectangular lokal + rumus shoelace).</summary>
    public static double LuasM2(IReadOnlyList<TitikGeo> p)
    {
        if (p.Count < 3) return 0;
        const double R = 6_378_137;
        var lat0 = p.Average(t => t.Lat) * Math.PI / 180;
        double jumlah = 0;
        for (var i = 0; i < p.Count; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Count];
            double x1 = R * a.Lon * Math.PI / 180 * Math.Cos(lat0), y1 = R * a.Lat * Math.PI / 180;
            double x2 = R * b.Lon * Math.PI / 180 * Math.Cos(lat0), y2 = R * b.Lat * Math.PI / 180;
            jumlah += x1 * y2 - x2 * y1;
        }
        return Math.Abs(jumlah) / 2;
    }

    private double MeterPerPiksel => 156_543.03392 * Math.Cos(Pusat.Lat * Math.PI / 180) / Math.Pow(2, _zoom);

    // ------------------------------------------------------------------ interaksi

    private void AturZoom(int zoomBaru, Point jangkar)
    {
        zoomBaru = Math.Clamp(zoomBaru, ZoomMin, ZoomMaks);
        if (zoomBaru == _zoom) return;
        var geo = LayarKeGeo(jangkar);
        _zoom = zoomBaru;
        _cx = LonKeX(geo.Lon, _zoom) - jangkar.X + Width / 2.0;
        _cy = LatKeY(geo.Lat, _zoom) - jangkar.Y + Height / 2.0;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Right) { _titikMenu = e.Location; return; }
        if (e.Button != MouseButtons.Left) return;

        if (_modeGambar)
        {
            if (_poligon.Count >= 3 && Jarak(GeoKeLayar(_poligon[0]), e.Location) < Tema.S(12))
            {
                SelesaiGambar();
                return;
            }
            if (_poligon.Count > 0 && Jarak(GeoKeLayar(_poligon[^1]), e.Location) < Tema.S(4)) return; // klik ganda
            _poligon.Add(LayarKeGeo(e.Location));
            PoligonBerubah?.Invoke(this, EventArgs.Empty);
            Invalidate();
            return;
        }

        _seret = true;
        _sudahDigeser = false;
        _titikSeret = e.Location;
        _pusatSeret = (_cx, _cy);
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_seret)
        {
            var dx = e.X - _titikSeret.X;
            var dy = e.Y - _titikSeret.Y;
            if (Math.Abs(dx) + Math.Abs(dy) > 3) _sudahDigeser = true;
            _cx = _pusatSeret.X - dx;
            _cy = Math.Clamp(_pusatSeret.Y - dy, 0, Dunia(_zoom));
            Invalidate();
        }
        else if (_modeGambar)
        {
            _kursor = e.Location;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_seret) Cursor = _modeGambar ? Cursors.Cross : Cursors.Hand;
        _seret = false;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _kursor = null;
        Invalidate();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left) return;
        if (_modeGambar) SelesaiGambar();
        else if (!_sudahDigeser) AturZoom(_zoom + 1, e.Location);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        AturZoom(_zoom + Math.Sign(e.Delta), e.Location);
    }

    protected override bool IsInputKey(Keys keyData)
        => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Escape or Keys.Enter or Keys.Back
           || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var langkah = Tema.S(80);
        var tengah = new Point(Width / 2, Height / 2);
        switch (e.KeyCode)
        {
            case Keys.Left: _cx -= langkah; break;
            case Keys.Right: _cx += langkah; break;
            case Keys.Up: _cy -= langkah; break;
            case Keys.Down: _cy += langkah; break;
            case Keys.Add or Keys.Oemplus: AturZoom(_zoom + 1, tengah); break;
            case Keys.Subtract or Keys.OemMinus: AturZoom(_zoom - 1, tengah); break;
            case Keys.Escape when _modeGambar: HapusPoligon(); break;
            case Keys.Enter when _modeGambar: SelesaiGambar(); break;
            case Keys.Back when _modeGambar && _poligon.Count > 0:
                _poligon.RemoveAt(_poligon.Count - 1);
                PoligonBerubah?.Invoke(this, EventArgs.Empty);
                break;
            case Keys.Space when !_modeGambar && Focused:
                _titikMenu = tengah;
                _menu.Show(this, tengah);
                break;
            default: return;
        }
        e.Handled = true;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Right) _menu.Show(this, e.Location);
    }

    private void SusunMenu()
    {
        _menu.Items.Clear();
        var geo = LayarKeGeo(_titikMenu);
        _menu.Items.Add("Pilih titik ini sebagai lokasi rumah", null, (_, _) => LokasiDipilih?.Invoke(this, geo));
        _menu.Items.Add("Pusatkan peta di sini", null, (_, _) => { Pusatkan(geo, _zoom); });
        _menu.Items.Add(new ToolStripSeparator());
        if (_modeGambar)
        {
            _menu.Items.Add("Selesai menggambar (Enter)", null, (_, _) => SelesaiGambar()).Enabled = _poligon.Count >= 3;
            _menu.Items.Add("Batalkan titik terakhir (Backspace)", null, (_, _) =>
            {
                if (_poligon.Count == 0) return;
                _poligon.RemoveAt(_poligon.Count - 1);
                PoligonBerubah?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }).Enabled = _poligon.Count > 0;
        }
        else
        {
            _menu.Items.Add("Gambar atap", null, (_, _) => MulaiGambar());
        }
        _menu.Items.Add("Hapus gambar atap", null, (_, _) => HapusPoligon()).Enabled = _poligon.Count > 0;
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_lapisan == LapisanPeta.Satelit ? "Tampilkan peta jalan" : "Tampilkan citra satelit", null,
            (_, _) => Lapisan = _lapisan == LapisanPeta.Satelit ? LapisanPeta.Jalan : LapisanPeta.Satelit);
    }

    private static double Jarak(PointF a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    // ------------------------------------------------------------------ menggambar

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Tema.Hex("#E8EAED"));
        g.InterpolationMode = InterpolationMode.Bilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        var n = 1 << _zoom;
        var kiri = _cx - Width / 2.0;
        var atas = _cy - Height / 2.0;
        int tx0 = (int)Math.Floor(kiri / UkuranTile), tx1 = (int)Math.Floor((kiri + Width) / UkuranTile);
        int ty0 = (int)Math.Floor(atas / UkuranTile), ty1 = (int)Math.Floor((atas + Height) / UkuranTile);

        for (var ty = ty0; ty <= ty1; ty++)
        {
            if (ty < 0 || ty >= n) continue;
            for (var tx = tx0; tx <= tx1; tx++)
            {
                var x = ((tx % n) + n) % n;
                var tujuan = new Rectangle((int)Math.Round(tx * UkuranTile - kiri), (int)Math.Round(ty * UkuranTile - atas), UkuranTile, UkuranTile);
                GambarTile(g, _zoom, x, ty, tujuan);
            }
        }

        Tema.Halus(g);
        GambarPoligon(g);
        GambarPenanda(g);
        GambarSkala(g);
        GambarAtribusi(g);
        GambarPetunjuk(g);

        if (Focused && ShowFocusCues)
        {
            using var p = new Pen(Tema.Biru, Tema.S(2f));
            g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
        }
    }

    private void GambarTile(Graphics g, int z, int x, int y, Rectangle tujuan)
    {
        var kunci = Kunci(_lapisan, z, x, y);
        if (_cache.TryGetValue(kunci, out var img))
        {
            g.DrawImage(img, tujuan);
            Sentuh(kunci);
            return;
        }

        if (!_tidakAda.Contains(kunci)) MintaTile(_lapisan, z, x, y);

        // Sementara (atau bila tile tidak tersedia di zoom ini): perbesar tile induk yang sudah ada.
        for (var d = 1; d <= 6 && z - d >= 0; d++)
        {
            var pk = Kunci(_lapisan, z - d, x >> d, y >> d);
            if (!_cache.TryGetValue(pk, out var induk))
            {
                if (_tidakAda.Contains(kunci) && !_tidakAda.Contains(pk)) MintaTile(_lapisan, z - d, x >> d, y >> d);
                continue;
            }
            var sub = UkuranTile >> d;
            var sumber = new Rectangle((x - ((x >> d) << d)) * sub, (y - ((y >> d) << d)) * sub, sub, sub);
            g.DrawImage(induk, tujuan, sumber, GraphicsUnit.Pixel);
            return;
        }
    }

    private void GambarPoligon(Graphics g)
    {
        if (_poligon.Count == 0) return;
        var titik = _poligon.Select(GeoKeLayar).ToList();

        if (titik.Count >= 3)
        {
            using var path = new GraphicsPath();
            path.AddPolygon(titik.ToArray());
            var batas = path.GetBounds();
            if (batas.Width > 1 && batas.Height > 1)
            {
                // Heatmap gaya Sunroof: kuning (paling terpapar) → oranye → merah-oranye.
                using var lg = new LinearGradientBrush(batas, Color.Empty, Color.Empty, 35f)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Colors = [Color.FromArgb(170, 255, 235, 59), Color.FromArgb(170, 255, 193, 7), Color.FromArgb(170, 251, 140, 0), Color.FromArgb(170, 244, 81, 30)],
                        Positions = [0f, 0.35f, 0.7f, 1f],
                    },
                };
                g.FillPath(lg, path);
            }
            using var pPutih = new Pen(Color.White, Tema.S(3f)) { LineJoin = LineJoin.Round };
            using var pOranye = new Pen(Tema.OranyeTua, Tema.S(1.5f)) { LineJoin = LineJoin.Round };
            if (_modeGambar)
            {
                g.DrawLines(pPutih, titik.ToArray());
                g.DrawLines(pOranye, titik.ToArray());
            }
            else
            {
                g.DrawPath(pPutih, path);
                g.DrawPath(pOranye, path);
            }
        }
        else if (titik.Count == 2)
        {
            using var p = new Pen(Color.White, Tema.S(2.5f));
            g.DrawLine(p, titik[0], titik[1]);
        }

        if (_modeGambar && _kursor is { } k && titik.Count > 0)
        {
            using var p = new Pen(Color.White, Tema.S(1.5f)) { DashStyle = DashStyle.Dash };
            g.DrawLine(p, titik[^1], k);
            if (titik.Count >= 2) g.DrawLine(p, k, titik[0]);
        }

        var d = Tema.S(9f);
        for (var i = 0; i < titik.Count; i++)
        {
            var t = titik[i];
            var awal = i == 0 && _modeGambar && titik.Count >= 3;
            var dd = awal ? d * 1.5f : d;
            g.FillEllipse(Brushes.White, t.X - dd / 2, t.Y - dd / 2, dd, dd);
            using var p = new Pen(awal ? Tema.Hijau : Tema.OranyeTua, Tema.S(2f));
            g.DrawEllipse(p, t.X - dd / 2, t.Y - dd / 2, dd, dd);
        }

        if (_poligon.Count >= 3)
        {
            var cx = titik.Average(t => t.X);
            var cy = titik.Average(t => t.Y);
            var teks = $"{Tema.N(LuasPoligonM2, 0)} m²";
            var f = Tema.Font(10, semibold: true);
            var uk = TextRenderer.MeasureText(teks, f);
            var r = new RectangleF(cx - uk.Width / 2f - Tema.S(8), cy - uk.Height / 2f - Tema.S(3), uk.Width + Tema.S(16), uk.Height + Tema.S(6));
            using (var path = Tema.Bulat(r, r.Height / 2))
            using (var b = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                g.FillPath(b, path);
            TextRenderer.DrawText(g, teks, f, Rectangle.Round(r), Tema.Teks, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private void GambarPenanda(Graphics g)
    {
        if (_penanda is not { } p) return;
        var t = GeoKeLayar(p);
        var w = Tema.S(30f);
        var r = new RectangleF(t.X - w / 2, t.Y - w * 1.25f, w, w * 1.25f);
        using (var bayang = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            g.FillEllipse(bayang, t.X - w * 0.22f, t.Y - w * 0.08f, w * 0.44f, w * 0.16f);
        Ikon.Pin(g, r, Tema.Merah);
    }

    private void GambarSkala(Graphics g)
    {
        var mpp = MeterPerPiksel;
        var target = Tema.S(100) * mpp;
        var pangkat = Math.Pow(10, Math.Floor(Math.Log10(target)));
        var meter = new[] { 5, 2, 1 }.Select(m => m * pangkat).First(m => m <= target);
        var px = (float)(meter / mpp);
        var teks = meter >= 1000 ? $"{meter / 1000:0.#} km" : $"{meter:0} m";
        var x = Tema.S(12f);
        var y = Height - Tema.S(22f);
        using (var bg = Tema.Bulat(new RectangleF(x - Tema.S(6), y - Tema.S(16), px + Tema.S(12), Tema.S(26)), Tema.S(6)))
        using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
            g.FillPath(b, bg);
        using var pen = new Pen(Tema.Teks, Tema.S(2f));
        g.DrawLines(pen, new[] { new PointF(x, y - Tema.S(4)), new PointF(x, y + Tema.S(3)), new PointF(x + px, y + Tema.S(3)), new PointF(x + px, y - Tema.S(4)) });
        TextRenderer.DrawText(g, teks, Tema.Font(8), new Point((int)x, (int)(y - Tema.S(16))), Tema.Teks);
    }

    private void GambarAtribusi(Graphics g)
    {
        var teks = _lapisan == LapisanPeta.Satelit
            ? "Citra © Esri, Maxar, Earthstar Geographics, GIS User Community"
            : "© kontributor OpenStreetMap";
        var f = Tema.Font(7.5f);
        var uk = TextRenderer.MeasureText(teks, f);
        var r = new Rectangle(Width - uk.Width - Tema.S(8), Height - uk.Height - Tema.S(4), uk.Width + Tema.S(8), uk.Height + Tema.S(4));
        using (var b = new SolidBrush(Color.FromArgb(190, 255, 255, 255))) g.FillRectangle(b, r);
        TextRenderer.DrawText(g, teks, f, new Point(r.X + Tema.S(4), r.Y + Tema.S(2)), Tema.TeksRedup);
    }

    private void GambarPetunjuk(Graphics g)
    {
        var teks = _modeGambar
            ? _poligon.Count < 3
                ? "Klik sudut-sudut atap Anda satu per satu"
                : "Klik titik hijau, tekan Enter, atau klik dua kali untuk selesai · Backspace: batalkan titik"
            : Petunjuk;
        if (string.IsNullOrEmpty(teks)) return;
        var f = Tema.Font(9.5f, semibold: true);
        var uk = TextRenderer.MeasureText(teks, f);
        var r = new RectangleF((Width - uk.Width) / 2f - Tema.S(14), Tema.S(16), uk.Width + Tema.S(28), uk.Height + Tema.S(14));
        using (var path = Tema.Bulat(r, r.Height / 2))
        using (var b = new SolidBrush(Color.FromArgb(225, 32, 33, 36)))
            g.FillPath(b, path);
        TextRenderer.DrawText(g, teks, f, Rectangle.Round(r), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    // ------------------------------------------------------------------ tile: memori → disk → jaringan

    private static string Kunci(LapisanPeta l, int z, int x, int y) => string.Create(CultureInfo.InvariantCulture, $"{(int)l}/{z}/{x}/{y}");

    private static string Url(LapisanPeta l, int z, int x, int y) => l == LapisanPeta.Satelit
        // blankTile=false → 404 untuk area tanpa data (bukan gambar "Map data not yet available")
        ? $"https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}?blankTile=false"
        : $"https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    private static string BerkasDisk(LapisanPeta l, int z, int x, int y)
        => Path.Combine(FolderData.Tile, l.ToString().ToLowerInvariant(), z.ToString(CultureInfo.InvariantCulture),
            $"{x}_{y}.{(l == LapisanPeta.Satelit ? "jpg" : "png")}");

    private void Sentuh(string kunci)
    {
        var node = _urutanLru.Find(kunci);
        if (node is null || node == _urutanLru.Last) return;
        _urutanLru.Remove(node);
        _urutanLru.AddLast(node);
    }

    private void Simpan(string kunci, Image img)
    {
        if (_cache.Remove(kunci, out var lama)) { lama.Dispose(); _urutanLru.Remove(kunci); }
        _cache[kunci] = img;
        _urutanLru.AddLast(kunci);
        while (_cache.Count > MaksCacheMemori && _urutanLru.First is { } tertua)
        {
            _urutanLru.RemoveFirst();
            if (_cache.Remove(tertua.Value, out var buang)) buang.Dispose();
        }
    }

    private void MintaTile(LapisanPeta l, int z, int x, int y)
    {
        var kunci = Kunci(l, z, x, y);
        if (_diambil.Contains(kunci) || _cache.ContainsKey(kunci) || _tidakAda.Contains(kunci)) return;
        if (_gagal.TryGetValue(kunci, out var waktu) && DateTime.UtcNow - waktu < TimeSpan.FromSeconds(30)) return;
        _diambil.Add(kunci);
        _ = AmbilTileAsync(l, z, x, y, kunci);
    }

    private async Task AmbilTileAsync(LapisanPeta l, int z, int x, int y, string kunci)
    {
        Image? img = null;
        var tidakAda = false;
        try
        {
            var berkas = BerkasDisk(l, z, x, y);
            byte[]? data = null;
            if (File.Exists(berkas) && DateTime.UtcNow - File.GetLastWriteTimeUtc(berkas) < TimeSpan.FromDays(30))
                data = await File.ReadAllBytesAsync(berkas).ConfigureAwait(false);

            if (data is null)
            {
                await _batasUnduh.WaitAsync().ConfigureAwait(false);
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, Url(l, z, x, y));
                    req.Headers.Accept.ParseAdd("image/*");
                    using var resp = await Http.Klien.SendAsync(req).ConfigureAwait(false);
                    if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest) tidakAda = true;
                    else if (resp.IsSuccessStatusCode)
                    {
                        data = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        Directory.CreateDirectory(Path.GetDirectoryName(berkas)!);
                        await File.WriteAllBytesAsync(berkas, data).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _batasUnduh.Release();
                }
            }

            if (data is { Length: > 0 })
            {
                using var ms = new MemoryStream(data);
                using var sementara = Image.FromStream(ms);
                img = new Bitmap(sementara);
            }
        }
        catch (Exception e) when (e is HttpRequestException or IOException or ArgumentException or TaskCanceledException
                                      or UnauthorizedAccessException or ExternalException)
        {
            // jaringan/berkas bermasalah → dicoba lagi nanti
        }

        if (IsDisposed || !IsHandleCreated)
        {
            img?.Dispose();
            return;
        }
        try
        {
            BeginInvoke(() =>
            {
                _diambil.Remove(kunci);
                if (img is not null) Simpan(kunci, img);
                else if (tidakAda) _tidakAda.Add(kunci);
                else _gagal[kunci] = DateTime.UtcNow;
                Invalidate();
            });
        }
        catch (InvalidOperationException)
        {
            img?.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var img in _cache.Values) img.Dispose();
            _cache.Clear();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
