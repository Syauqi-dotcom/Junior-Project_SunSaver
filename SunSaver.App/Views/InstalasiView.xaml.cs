using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SunSaver.App.Data;

namespace SunSaver.App.Views;

public partial class InstalasiView : UserControl
{
    private static readonly CultureInfo Id = new("id-ID");

    private static readonly (string Nama, double Tarif)[] GolonganTarif =
    [
        ("R-1 / 900 VA",   1352.00),
        ("R-1 / 1.300 VA", 1444.70),
        ("R-1 / 2.200 VA", 1444.70),
        ("R-2 / 3.500–5.500 VA", 1699.53),
        ("R-3 / ≥6.600 VA", 1699.53),
    ];

    private readonly Instalasi _instalasi;
    private bool _sedangMengisi;

    public event EventHandler? Disimpan;
    public event EventHandler? Dibatalkan;

    public InstalasiView(Instalasi instalasi)
    {
        InitializeComponent();
        _instalasi = instalasi;
        PilihGolongan.ItemsSource = GolonganTarif.Select(g => g.Nama).ToList();
        Isi();
    }

    /// <summary>Salin data instalasi tersimpan ke form (membuang perubahan yang belum disimpan).</summary>
    public void Isi()
    {
        _sedangMengisi = true;
        InputNama.Text = _instalasi.Nama;
        InputAlamat.Text = _instalasi.Alamat;
        InputLat.Text = _instalasi.Latitude.ToString("0.####", Id);
        InputLon.Text = _instalasi.Longitude.ToString("0.####", Id);
        InputKapasitas.Text = _instalasi.KapasitasKwp.ToString("0.##", Id);
        InputJumlah.Text = _instalasi.JumlahPanel.ToString(Id);
        SliderEfisiensi.Value = _instalasi.EfisiensiPersen;
        PilihGolongan.SelectedItem = _instalasi.GolonganTarif;
        InputTarif.Text = _instalasi.TarifPerKwh.ToString("N2", Id);
        _sedangMengisi = false;

        Validasi();
    }

    private void Input_Berubah(object sender, TextChangedEventArgs e) => Validasi();

    private void SliderEfisiensi_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => TeksEfisiensi.Text = $"{e.NewValue:N0}%";

    private void PilihGolongan_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_sedangMengisi || PilihGolongan.SelectedIndex < 0) return;
        InputTarif.Text = GolonganTarif[PilihGolongan.SelectedIndex].Tarif.ToString("N2", Id);
    }

    /// <summary>
    /// Validasi semua field, tampilkan galat di bawah field masing-masing, perbarui pratinjau.
    /// Mengembalikan data hasil parsing kalau semuanya valid.
    /// </summary>
    private (double Lat, double Lon, double Kwp, int Jumlah, double Tarif)? Validasi()
    {
        if (_sedangMengisi) return null;

        bool ok = true;
        ok &= Cek(GalatNama, string.IsNullOrWhiteSpace(InputNama.Text) ? "Nama instalasi wajib diisi" : null);

        var lat = ParseDesimal(InputLat.Text);
        ok &= Cek(GalatLat, lat is null ? "Latitude harus berupa angka"
                          : lat is < -90 or > 90 ? "Latitude harus antara -90 dan 90" : null);

        var lon = ParseDesimal(InputLon.Text);
        ok &= Cek(GalatLon, lon is null ? "Longitude harus berupa angka"
                          : lon is < -180 or > 180 ? "Longitude harus antara -180 dan 180" : null);

        var kwp = ParseDesimal(InputKapasitas.Text);
        ok &= Cek(GalatKapasitas, kwp is null or <= 0 ? "Kapasitas harus lebih dari 0"
                                : kwp > 100 ? "Kapasitas rumah tangga maksimal 100 kWp" : null);

        int? jumlah = int.TryParse(InputJumlah.Text, NumberStyles.Integer, Id, out var j) ? j : null;
        ok &= Cek(GalatJumlah, jumlah is null or <= 0 ? "Jumlah panel minimal 1" : null);

        double? tarif = double.TryParse(InputTarif.Text, NumberStyles.Number, Id, out var t) ? t : null;
        ok &= Cek(GalatTarif, tarif is null or <= 0 ? "Tarif harus lebih dari 0" : null);

        PerbaruiPratinjau(kwp is > 0 ? kwp.Value : 0, tarif is > 0 ? tarif.Value : 0);
        TombolSimpan.IsEnabled = ok;

        return ok ? (lat!.Value, lon!.Value, kwp!.Value, jumlah!.Value, tarif!.Value) : null;
    }

    private void PerbaruiPratinjau(double kwp, double tarif)
    {
        var harian = DataContoh.EstimasiHarianKwh(kwp);
        var bulanan = harian * 30;
        PratinjauHarian.Text = $"{harian:N1} kWh";
        PratinjauHemat.Text = $"Rp {bulanan * tarif:N0}";
        PratinjauCo2.Text = $"{bulanan * DataContoh.FaktorEmisiJamali:N1} kg";
    }

    private static bool Cek(TextBlock galat, string? pesan)
    {
        galat.Text = pesan ?? "";
        galat.Visibility = pesan is null ? Visibility.Collapsed : Visibility.Visible;
        return pesan is null;
    }

    /// <summary>
    /// Terima "3,5" (format Indonesia) maupun "3.5". Untuk desimal kecil seperti koordinat dan kWp,
    /// titik hampir pasti dimaksudkan sebagai pemisah desimal, bukan ribuan.
    /// </summary>
    private static double? ParseDesimal(string teks)
        => double.TryParse(teks.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : null;

    private void Simpan_Click(object sender, RoutedEventArgs e)
    {
        if (Validasi() is not { } d) return;

        _instalasi.Nama = InputNama.Text.Trim();
        _instalasi.Alamat = InputAlamat.Text.Trim();
        _instalasi.Latitude = d.Lat;
        _instalasi.Longitude = d.Lon;
        _instalasi.KapasitasKwp = d.Kwp;
        _instalasi.JumlahPanel = d.Jumlah;
        _instalasi.EfisiensiPersen = SliderEfisiensi.Value;
        _instalasi.GolonganTarif = PilihGolongan.SelectedItem as string ?? _instalasi.GolonganTarif;
        _instalasi.TarifPerKwh = d.Tarif;

        Disimpan?.Invoke(this, EventArgs.Empty);
    }

    private void Batal_Click(object sender, RoutedEventArgs e)
    {
        Isi();
        Dibatalkan?.Invoke(this, EventArgs.Empty);
    }
}
