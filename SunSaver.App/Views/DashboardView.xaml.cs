using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SunSaver.App.Data;

namespace SunSaver.App.Views;

public partial class DashboardView : UserControl
{
    private readonly Instalasi _instalasi;
    private IReadOnlyList<BarisHarian> _rincian = [];

    public event EventHandler? UbahDataDiminta;

    public DashboardView(Instalasi instalasi)
    {
        InitializeComponent();
        _instalasi = instalasi;
        Muat();
    }

    /// <summary>Hitung ulang semua angka dari instalasi aktif.</summary>
    public void Muat()
    {
        _rincian = DataContoh.RincianMingguan(_instalasi);

        var totalKwh = _rincian.Sum(r => r.ProduksiKwh);
        var totalRp = _rincian.Sum(r => r.HematRp);
        var totalCo2 = totalKwh * DataContoh.FaktorEmisiJamali;

        TeksProduksi.Text = $"{totalKwh:N1} kWh";
        TeksHemat.Text = $"Rp {totalRp:N0}";
        TeksTarif.Text = $"tarif {_instalasi.GolonganTarif} · Rp {_instalasi.TarifPerKwh:N0}/kWh";
        TeksCo2.Text = $"{totalCo2:N1} kg";
        TeksPohon.Text = $"{DataContoh.SetaraPohon(totalCo2, _rincian.Count):N1} pohon";

        TeksNama.Text = _instalasi.Nama;
        TeksKapasitas.Text = $"{_instalasi.KapasitasKwp:N1} kWp";
        TeksEfisiensi.Text = $"{_instalasi.EfisiensiPersen:N0}%";
        TeksJumlahPanel.Text = $"{_instalasi.JumlahPanel} unit";
        TeksKoordinat.Text = $"{_instalasi.Latitude:N4}; {_instalasi.Longitude:N4}";

        TabelRincian.ItemsSource = _rincian;
        GambarGrafik();
    }

    private void KanvasGrafik_SizeChanged(object sender, SizeChangedEventArgs e) => GambarGrafik();

    private void UbahData_Click(object sender, RoutedEventArgs e) => UbahDataDiminta?.Invoke(this, EventArgs.Empty);

    // ---------------------------------------------------------------------------------
    // Grafik kombinasi: batang = produksi (sumbu kiri), garis = radiasi (sumbu kanan).
    // Digambar manual supaya tidak butuh paket grafik pihak ketiga untuk tahap UI ini.
    // ---------------------------------------------------------------------------------
    private void GambarGrafik()
    {
        var c = KanvasGrafik;
        c.Children.Clear();
        double w = c.ActualWidth, h = c.ActualHeight;
        if (w < 100 || h < 100 || _rincian.Count == 0) return;

        const double kiri = 48, kanan = 52, atas = 8, bawah = 26;
        double lebarPlot = w - kiri - kanan, tinggiPlot = h - atas - bawah;

        var maksKwh = BulatkanKeAtas(_rincian.Max(r => r.ProduksiKwh) * 1.15);
        var maksRad = BulatkanKeAtas(_rincian.Max(r => r.RadiasiWm2) * 1.15);
        var redup = (Brush)FindResource("TeksRedup");
        var aksen = (Brush)FindResource("Aksen");
        var garis = (Brush)FindResource("Garis");

        // Garis bantu horizontal + label kedua sumbu
        const int jumlahGaris = 4;
        for (int i = 0; i <= jumlahGaris; i++)
        {
            var y = atas + tinggiPlot - tinggiPlot * i / jumlahGaris;
            c.Children.Add(new Line { X1 = kiri, X2 = kiri + lebarPlot, Y1 = y, Y2 = y, Stroke = garis, StrokeThickness = 1 });
            Tulis(c, (maksKwh * i / jumlahGaris).ToString("N1"), 0, y - 8, kiri - 8, TextAlignment.Right, redup);
            Tulis(c, (maksRad * i / jumlahGaris).ToString("N0"), kiri + lebarPlot + 8, y - 8, kanan - 8, TextAlignment.Left, redup);
        }

        var slot = lebarPlot / _rincian.Count;
        var lebarBatang = Math.Min(slot * 0.5, 56);
        var titikRadiasi = new PointCollection();

        for (int i = 0; i < _rincian.Count; i++)
        {
            var r = _rincian[i];
            var tengah = kiri + slot * i + slot / 2;

            var tinggi = tinggiPlot * r.ProduksiKwh / maksKwh;
            var batang = new Rectangle
            {
                Width = lebarBatang, Height = tinggi, Fill = aksen, RadiusX = 3, RadiusY = 3,
                ToolTip = $"{r.Hari}: {r.ProduksiKwh:N2} kWh"
            };
            Canvas.SetLeft(batang, tengah - lebarBatang / 2);
            Canvas.SetTop(batang, atas + tinggiPlot - tinggi);
            c.Children.Add(batang);

            Tulis(c, r.Hari, tengah - 30, atas + tinggiPlot + 6, 60, TextAlignment.Center, redup);
            titikRadiasi.Add(new Point(tengah, atas + tinggiPlot - tinggiPlot * r.RadiasiWm2 / maksRad));
        }

        var warnaGaris = new SolidColorBrush(Color.FromRgb(0x52, 0x52, 0x5B));
        c.Children.Add(new Polyline { Points = titikRadiasi, Stroke = warnaGaris, StrokeThickness = 2 });
        foreach (var p in titikRadiasi)
        {
            var titik = new Ellipse { Width = 7, Height = 7, Fill = Brushes.White, Stroke = warnaGaris, StrokeThickness = 2 };
            Canvas.SetLeft(titik, p.X - 3.5);
            Canvas.SetTop(titik, p.Y - 3.5);
            c.Children.Add(titik);
        }
    }

    private static void Tulis(Canvas c, string teks, double x, double y, double lebar, TextAlignment rata, Brush warna)
    {
        var tb = new TextBlock
        {
            Text = teks, Width = lebar, TextAlignment = rata, Foreground = warna, FontSize = 11,
            FontFamily = new FontFamily("Consolas")
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        c.Children.Add(tb);
    }

    /// <summary>Bulatkan ke 1/2/5 × 10^n terdekat di atasnya, agar label sumbu rapi.</summary>
    private static double BulatkanKeAtas(double nilai)
    {
        if (nilai <= 0) return 1;
        var pangkat = Math.Pow(10, Math.Floor(Math.Log10(nilai)));
        foreach (var m in new[] { 1, 2, 2.5, 5, 10 })
            if (m * pangkat >= nilai) return m * pangkat;
        return 10 * pangkat;
    }
}
