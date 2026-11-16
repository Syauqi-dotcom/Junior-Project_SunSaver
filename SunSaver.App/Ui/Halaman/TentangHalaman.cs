using System.Diagnostics;
using SunSaver.App.Core.Kalkulasi;
using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App.Ui.Halaman;

/// <summary>Tentang aplikasi: metodologi perhitungan, asumsi, sumber data, referensi, dan tim.</summary>
public sealed class TentangHalaman : HalamanDasar
{
    public TentangHalaman(KonteksAplikasi k) : base(k)
    {
        var gulir = new PanelGulir { LebarMaks = Tema.S(940) };

        gulir.Controls.AddRange(
        [
            Teks.Judul("Tentang SunSaver", 22),
            Teks.Bungkus("SunSaver menghitung potensi energi surya dan efisiensi jejak karbon berdasarkan data radiasi matahari " +
                         "di lokasi pengguna. Terinspirasi Google Project Sunroof, SunSaver berfokus pada parameter cuaca " +
                         "(weather-based variables) sehingga tetap bisa dipakai di daerah yang belum terjangkau citra satelit resolusi tinggi.",
                Tema.Font(11)),
            Bagian("1. Data cuaca (Open-Meteo, tanpa API key)",
                "• Potensi tahunan: data harian satu tahun kalender penuh dari Open-Meteo Historical Weather API (reanalisis ERA5): " +
                "shortwave_radiation_sum, sunshine_duration, temperature_2m_mean, cloud_cover_mean.\n" +
                "• Dashboard real-time: Open-Meteo Forecast API, kondisi saat ini (interval 15 menit), per jam, dan 7 hari ke depan.\n" +
                "• Hasil historis disimpan di cache lokal sehingga tidak diunduh berulang."),
            Bagian("2. Kalkulator energi",
                "PSH (jam matahari puncak) = radiasi harian [MJ/m²] ÷ 3,6\n" +
                $"E_harian [kWh] = P [kWp] × PSH × PR_sistem ({Tema.N(KalkulatorEnergi.PrSistem, 2)}) × f_suhu\n" +
                "f_suhu = 1 − 0,4% × (T_sel − 25 °C), dengan T_sel ≈ T_udara + 20 °C (harian)\n" +
                "Luas panel = Wp ÷ (1000 W/m² × efisiensi); panel maksimum = luas atap × 75% ÷ luas panel.\n" +
                "Ukuran rekomendasi = konsumsi tahunan ÷ hasil spesifik lokasi (kWh/kWp/tahun), dibatasi luas atap."),
            Bagian("3. Kalkulator karbon & gamifikasi",
                $"CO₂ dihindari [kg] = energi [kWh] × faktor emisi grid ({Tema.N(KalkulatorKarbon.FaktorEmisiDefault, 3)} kg CO₂e/kWh, grid Jawa-Madura-Bali; dapat diubah).\n" +
                $"Setara pohon = CO₂ tahunan ÷ {Tema.N(KalkulatorKarbon.SerapanPohonKgPerTahun, 2)} kg CO₂ per pohon per tahun.\n" +
                "Lencana: Benih → Tunas Surya (1) → Kebun Hijau (10) → Taman Kota (25) → Hutan Kecil (50) → Penjaga Hutan (100 pohon)."),
            Bagian("4. Kalkulator finansial",
                "Hemat bulanan = min(produksi, konsumsi) × tarif — tanpa kompensasi ekspor ke PLN (Permen ESDM No. 2/2024).\n" +
                "Proyeksi 20 tahun memperhitungkan kenaikan tarif 3%/tahun, degradasi panel 0,5%/tahun, perawatan 1%/tahun dari harga sistem, " +
                "dan biaya sistem Rp 15 juta/kWp. Cicilan memakai rumus anuitas. Semua asumsi dapat diubah di halaman Estimasi.\n" +
                "Angka bersifat indikatif untuk edukasi, bukan penawaran pemasangan."),
            Bagian("5. Backend cloud & keamanan data",
                "• Database: PostgreSQL di Supabase (tabel profil, tarif_listrik, instalasi, riwayat_estimasi).\n" +
                "• Autentikasi: Supabase Auth (email + kata sandi). REST API: PostgREST (/rest/v1).\n" +
                "• Row Level Security: setiap pengguna hanya bisa membaca/mengubah datanya sendiri.\n" +
                "• Aplikasi hanya memakai publishable/anon key; refresh token disimpan terenkripsi DPAPI di komputer pengguna.\n" +
                "• Tanpa akun, aplikasi tetap berjalan dalam mode lokal (berkas JSON)."),
            Bagian("6. Sumber data & atribusi",
                "Cuaca: Open-Meteo.com (CC BY 4.0) · Citra satelit: Esri World Imagery (Esri, Maxar, Earthstar Geographics, GIS User Community) · " +
                "Peta jalan & geocoding: © kontributor OpenStreetMap (ODbL), layanan Nominatim."),
        ]);

        var referensi = new Kartu();
        referensi.Controls.Add(Teks.Subjudul("Referensi literatur (serapan karbon pohon)"));
        foreach (var (teks, doi) in Referensi)
        {
            referensi.Controls.Add(Teks.Bungkus(teks, Tema.Font(9.5f), Tema.Teks));
            var link = new LinkLabel { Text = doi, AutoSize = true, Font = Tema.Font(9), LinkColor = Tema.Biru, Margin = new Padding(0, 0, 0, Tema.S(10)) };
            link.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(doi) { UseShellExecute = true });
            referensi.Controls.Add(link);
        }
        gulir.Controls.Add(referensi);

        var tim = new GridKartu { Kolom = 3, LebarMinKolom = Tema.S(240) };
        tim.Controls.AddRange(
        [
            KartuTim("Garjita Adicandra", "Software Architect & Systems", "Arsitektur perangkat lunak, diagram alir, struktur basis data, skema integrasi API RESTful."),
            KartuTim("Muhammad Syauqi Fittuqo", "Backend Developer & Data Logic", "Pemrosesan JSON Open-Meteo, penyimpanan data pengguna, algoritma estimasi daya & ekuivalensi karbon."),
            KartuTim("Naufal Dzaky", "Frontend Developer & UI/UX", "Antarmuka interaktif C#, visualisasi grafik analisis dan dashboard."),
        ]);
        gulir.Controls.Add(Teks.Judul("Tim Kelompok 27 · Junior Project", 15));
        gulir.Controls.Add(tim);
        Controls.Add(gulir);
    }

    public override string Judul => "Tentang";

    private static readonly (string Teks, string Doi)[] Referensi =
    [
        ("Mohsen, A.-Z. A., & Abdulkareem, A. K. (2026). Structural Attributes of Urban Trees Controlling Carbon Dioxide Sequestration: " +
         "An Applied Study in Al-Umma Park, Baghdad. Journal of Agrometeorology.", "https://doi.org/10.54386/jam.v28i2.3403"),
        ("Tacloy, J. G., & Paran, L. C. (2014). Carbon Stocked and Carbon Dioxide Sequestered by Eight-Year Alnus (Alnus japonica). " +
         "Mountain Journal of Science and Interdisciplinary Research.", "https://doi.org/10.70884/mjsir.v72i0.26"),
        ("Köhl, M., Neupane, P. R., & Lotfiomran, N. (2017). The impact of tree age on biomass growth and carbon accumulation capacity. PLoS ONE, 12.",
         "https://doi.org/10.1371/journal.pone.0181187"),
    ];

    private static Kartu Bagian(string judul, string isi)
    {
        var k = new Kartu();
        k.Controls.Add(Teks.Subjudul(judul));
        k.Controls.Add(Teks.Bungkus(isi, Tema.Font(10), Tema.Teks));
        return k;
    }

    private static Kartu KartuTim(string nama, string peran, string tugas)
    {
        var k = new Kartu { Margin = Padding.Empty };
        k.Controls.Add(Teks.Buat(nama, Tema.Font(11, semibold: true), Tema.Teks));
        k.Controls.Add(Teks.Buat(peran, Tema.Font(9.5f, semibold: true), Tema.Biru));
        k.Controls.Add(Teks.Bungkus(tugas, Tema.Font(9.5f)));
        return k;
    }
}
