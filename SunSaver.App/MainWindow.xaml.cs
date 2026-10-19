using System.Windows;
using SunSaver.App.Data;
using SunSaver.App.Views;

namespace SunSaver.App;

public partial class MainWindow : Window
{
    // Satu objek instalasi dipakai bersama kedua halaman,
    // jadi perubahan di form langsung terlihat di dashboard.
    private readonly Instalasi _instalasi = new();
    private readonly DashboardView _dashboard;
    private readonly InstalasiView _form;

    public MainWindow()
    {
        InitializeComponent();

        _dashboard = new DashboardView(_instalasi);
        _dashboard.UbahDataDiminta += (_, _) => NavInstalasi.IsChecked = true;

        _form = new InstalasiView(_instalasi);
        _form.Disimpan += (_, _) => { _dashboard.Muat(); NavDashboard.IsChecked = true; };
        _form.Dibatalkan += (_, _) => NavDashboard.IsChecked = true;

        Konten.Content = _dashboard;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        // Checked bisa terpicu saat InitializeComponent, sebelum field terisi.
        if (_dashboard is null) return;

        if (sender == NavDashboard) Konten.Content = _dashboard;
        else
        {
            _form.Isi();
            Konten.Content = _form;
        }
    }
}
