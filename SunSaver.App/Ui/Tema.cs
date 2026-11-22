using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace SunSaver.App.Ui;

/// <summary>
/// Sistem desain bergaya Google Project Sunroof (Material): putih bersih, aksen biru Google,
/// kuning matahari, hijau untuk dampak lingkungan.
/// </summary>
public static class Tema
{
    public static readonly Color Biru = Hex("#1A73E8");
    public static readonly Color BiruHover = Hex("#1967D2");
    public static readonly Color BiruTekan = Hex("#185ABC");
    public static readonly Color BiruMuda = Hex("#E8F0FE");
    public static readonly Color Teks = Hex("#202124");
    public static readonly Color TeksRedup = Hex("#5F6368");
    public static readonly Color TeksPudar = Hex("#80868B");
    public static readonly Color Garis = Hex("#DADCE0");
    public static readonly Color GarisHalus = Hex("#E8EAED");
    public static readonly Color Latar = Color.White;
    public static readonly Color LatarAbu = Hex("#F8F9FA");
    public static readonly Color Kuning = Hex("#FBBC04");
    public static readonly Color KuningMuda = Hex("#FEF7E0");
    public static readonly Color Oranye = Hex("#F29900");
    public static readonly Color OranyeTua = Hex("#E37400");
    public static readonly Color Hijau = Hex("#188038");
    public static readonly Color HijauTerang = Hex("#34A853");
    public static readonly Color HijauMuda = Hex("#E6F4EA");
    public static readonly Color Merah = Hex("#D93025");
    public static readonly Color MerahMuda = Hex("#FCE8E6");
    public static readonly Color Ungu = Hex("#9334E6");

    public static readonly CultureInfo Id = new("id-ID");

    private static readonly Dictionary<(float, FontStyle, bool), Font> CacheFont = [];
    private static string? _famili;

    /// <summary>Faktor skala DPI (1,0 = 96 DPI). Diisi sekali saat aplikasi mulai.</summary>
    public static float Skala { get; private set; } = 1f;

    public static void Inisialisasi(float dpi) => Skala = Math.Max(1f, dpi / 96f);

    /// <summary>Skalakan ukuran piksel desain (96 DPI) ke DPI layar.</summary>
    public static int S(int px) => (int)Math.Round(px * Skala);
    public static float S(float px) => px * Skala;
    public static Padding P(int semua) => new(S(semua));
    public static Padding P(int horizontal, int vertikal) => new(S(horizontal), S(vertikal), S(horizontal), S(vertikal));
    public static Padding P(int kiri, int atas, int kanan, int bawah) => new(S(kiri), S(atas), S(kanan), S(bawah));

    /// <summary>"Google Sans" bila terpasang, selain itu Segoe UI (bawaan Windows).</summary>
    public static string Famili => _famili ??= TersediaFont("Google Sans") ? "Google Sans" : "Segoe UI";

    public static Font Font(float pt, FontStyle gaya = FontStyle.Regular, bool semibold = false)
    {
        var kunci = (pt, gaya, semibold);
        if (!CacheFont.TryGetValue(kunci, out var f))
        {
            var famili = semibold && Famili == "Segoe UI" ? "Segoe UI Semibold" : Famili;
            if (semibold && Famili != "Segoe UI") gaya |= FontStyle.Bold;
            f = new Font(famili, pt, gaya, GraphicsUnit.Point);
            CacheFont[kunci] = f;
        }
        return f;
    }

    public static Font FontJudulHero => Font(28);
    public static Font FontJudul => Font(20);
    public static Font FontSubjudul => Font(13, semibold: true);
    public static Font FontIsi => Font(10.5f);
    public static Font FontKecil => Font(9);
    public static Font FontAngkaBesar => Font(22, semibold: true);
    public static Font FontAngka => Font(15, semibold: true);
    public static Font FontTombol => Font(10, semibold: true);

    // ------------------------------- format angka (id-ID) -------------------------------

    public static string Rp(double v) => "Rp " + Math.Round(v).ToString("N0", Id);

    /// <summary>Rupiah ringkas: Rp 850 rb, Rp 12,4 jt, Rp 1,2 M.</summary>
    public static string RpRingkas(double v)
    {
        var a = Math.Abs(v);
        var tanda = v < 0 ? "−" : "";
        return a switch
        {
            >= 1e9 => $"{tanda}Rp {(a / 1e9).ToString("0.#", Id)} M",
            >= 1e6 => $"{tanda}Rp {(a / 1e6).ToString("0.#", Id)} jt",
            >= 1e3 => $"{tanda}Rp {(a / 1e3).ToString("0", Id)} rb",
            _ => $"{tanda}Rp {a.ToString("0", Id)}",
        };
    }

    public static string N(double v, int desimal = 0) => v.ToString("N" + desimal, Id);

    public static string Tahun(double? t) => t is { } v ? $"{v.ToString("0.#", Id)} tahun" : "> 20 tahun";

    public static readonly string[] NamaBulanPendek = ["Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Agu", "Sep", "Okt", "Nov", "Des"];

    // ------------------------------- gambar -------------------------------

    public static Color Hex(string hex) => ColorTranslator.FromHtml(hex);

    public static Color Alfa(Color c, int alfa) => Color.FromArgb(Math.Clamp(alfa, 0, 255), c);

    public static Color Campur(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public static GraphicsPath Bulat(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        radius = Math.Max(0, Math.Min(radius, Math.Min(r.Width, r.Height) / 2));
        if (radius < 0.5f)
        {
            p.AddRectangle(r);
            return p;
        }
        var d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Halus(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    private static bool TersediaFont(string nama)
    {
        using var koleksi = new InstalledFontCollection();
        return koleksi.Families.Any(f => f.Name.Equals(nama, StringComparison.OrdinalIgnoreCase));
    }
}
