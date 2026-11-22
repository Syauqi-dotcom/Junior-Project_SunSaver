using SunSaver.App.Core.Kalkulasi;
using SunSaver.App.Core.Model;
using SunSaver.App.Ui.Grafik;
using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App.Ui.Halaman;

/// <summary>
/// Jelajah Data (padanan "Data Explorer" Project Sunroof): perbandingan potensi surya kota-kota besar
/// di Indonesia berdasarkan data cuaca setahun penuh.
/// </summary>
public sealed class JelajahHalaman : HalamanDasar
{
    private sealed record BarisKota(string Kota, string Provinsi, double JamPuncak, double Psh, double HasilSpesifik,
        double Suhu, double Awan, double Produksi3Kwp, double Co23Kwp, double Pohon3Kwp)
    {
        public Kota? Sumber { get; init; }
    }

    private readonly PanelGulir _gulir = new();
    private readonly LabelBungkus _status = Teks.Bungkus("", Tema.Font(9.5f));
    private readonly BilahProgres _progres = new() { Warna = Tema.Biru, Margin = new Padding(0, 0, 0, Tema.S(16)) };
    private readonly GrafikKombinasi _grafik = new() { Height = Tema.S(320) };
    private readonly DataGridView _tabel = Isian.Tabel();
    private readonly List<BarisKota> _baris = [];
    private bool _dimuat, _sedangMuat;

    public JelajahHalaman(KonteksAplikasi k) : base(k)
    {
        var kGrafik = new Kartu();
        kGrafik.Controls.AddRange(
        [
            Teks.Subjudul("Hasil energi spesifik per kota"),
            Teks.Bungkus("kWh listrik per kWp panel per tahun (setelah rugi sistem & suhu), diurutkan dari yang tertinggi.", Tema.Font(9.5f)),
            _grafik,
        ]);
        _grafik.FormatKiri = v => Tema.N(v, 0);
        _grafik.FormatKanan = v => Tema.N(v, 1);
        _grafik.JudulSumbuKiri = "kWh/kWp";
        _grafik.JudulSumbuKanan = "jam/hari";

        _tabel.Height = Tema.S(440);
        _tabel.Columns.AddRange(
            Isian.Kolom("Kota", nameof(BarisKota.Kota), kanan: false, bobot: 110),
            Isian.Kolom("Provinsi", nameof(BarisKota.Provinsi), kanan: false, bobot: 140),
            Isian.Kolom("Jam puncak/th", nameof(BarisKota.JamPuncak), "N0"),
            Isian.Kolom("PSH/hari", nameof(BarisKota.Psh), "N2", bobot: 80),
            Isian.Kolom("kWh/kWp/th", nameof(BarisKota.HasilSpesifik), "N0"),
            Isian.Kolom("Suhu (°C)", nameof(BarisKota.Suhu), "N1", bobot: 80),
            Isian.Kolom("Awan (%)", nameof(BarisKota.Awan), "N0", bobot: 80),
            Isian.Kolom("Produksi 3 kWp (kWh)", nameof(BarisKota.Produksi3Kwp), "N0"),
            Isian.Kolom("CO₂ 3 kWp (kg)", nameof(BarisKota.Co23Kwp), "N0"),
            Isian.Kolom("Setara pohon", nameof(BarisKota.Pohon3Kwp), "N0", bobot: 80));
        _tabel.CellDoubleClick += (_, e) => BukaKota(e.RowIndex);
        _tabel.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter || _tabel.CurrentRow is null) return;
            e.SuppressKeyPress = true;
            BukaKota(_tabel.CurrentRow.Index);
        };

        var kTabel = new Kartu();
        kTabel.Controls.AddRange(
        [
            Teks.Subjudul("Tabel potensi surya"),
            Teks.Bungkus("Klik dua kali (atau Enter) pada baris untuk membuka kota tersebut di peta dan menghitung estimasi atap.", Tema.Font(9.5f)),
            _tabel,
        ]);

        var muatUlang = new TombolBulat("Muat ulang data", GayaTombol.Sekunder);
        muatUlang.Click += async (_, _) => { _dimuat = false; await MuatAsync(); };
        var header = new Baris();
        header.Tambah(Teks.Judul("Jelajah Data Potensi Surya Indonesia", 22), 1).Tambah(muatUlang.Pas());

        _gulir.Controls.AddRange(
        [
            header,
            Teks.Bungkus($"Perbandingan {DataReferensi.Kota.Count} kota berdasarkan data cuaca harian setahun penuh (Open-Meteo Archive, model ERA5). " +
                         "Produksi dihitung untuk sistem acuan 3 kWp dengan faktor emisi grid " +
                         $"{Tema.N(KalkulatorKarbon.FaktorEmisiDefault, 3)} kg CO₂/kWh.", Tema.Font(10.5f)),
            _status, _progres, kGrafik, kTabel,
        ]);
        Controls.Add(_gulir);
    }

    public override string Judul => "Jelajah Data";

    public override async void Tampil(object? argumen)
    {
        if (!_dimuat) await MuatAsync();
    }

    private async Task MuatAsync()
    {
        if (_sedangMuat) return;
        _sedangMuat = true;
        _baris.Clear();
        _progres.Visible = true;
        var gagal = new List<string>();
        try
        {
            for (var i = 0; i < DataReferensi.Kota.Count; i++)
            {
                var kota = DataReferensi.Kota[i];
                _status.Text = $"Memuat data iklim {kota.Nama} ({i + 1}/{DataReferensi.Kota.Count})…";
                _progres.Nilai = i * 100.0 / DataReferensi.Kota.Count;
                try
                {
                    var r = await K.Iklim.RingkasanTahunanAsync(kota.Lat, kota.Lon);
                    var hs = KalkulatorEnergi.HasilSpesifikTahunan(r);
                    var prod = hs * 3;
                    var co2 = KalkulatorKarbon.Co2Kg(prod, KalkulatorKarbon.FaktorEmisiDefault);
                    _baris.Add(new BarisKota(kota.Nama, kota.Provinsi, r.JamMatahariPuncakTahunan, r.PshRataHarian, hs, r.SuhuRata,
                        r.TutupanAwanRata, prod, co2, KalkulatorKarbon.SetaraPohon(co2)) { Sumber = kota });
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    gagal.Add(kota.Nama);
                }
                Tampilkan();
            }
            _dimuat = gagal.Count == 0;
            _status.Text = gagal.Count == 0
                ? $"Data {_baris.Count} kota dimuat. Potensi tertinggi: {_baris.MaxBy(b => b.HasilSpesifik)?.Kota}; terendah: {_baris.MinBy(b => b.HasilSpesifik)?.Kota}."
                : $"Sebagian data gagal dimuat ({string.Join(", ", gagal)}). Periksa koneksi lalu klik \"Muat ulang data\".";
        }
        finally
        {
            _progres.Visible = false;
            _sedangMuat = false;
        }
    }

    private void Tampilkan()
    {
        var urut = _baris.OrderByDescending(b => b.HasilSpesifik).ToList();
        _grafik.Atur(urut.Select(b => b.Kota).ToArray(),
            new Seri("kWh/kWp per tahun", urut.Select(b => b.HasilSpesifik).ToArray(), Tema.Kuning),
            new Seri("Jam matahari puncak/hari", urut.Select(b => b.Psh).ToArray(), Tema.OranyeTua, JenisSeri.Garis, SumbuKanan: true));
        _tabel.DataSource = urut;
    }

    private void BukaKota(int baris)
    {
        if (baris < 0 || _tabel.DataSource is not List<BarisKota> data || baris >= data.Count || data[baris].Sumber is not { } kota) return;
        K.Shell.Ke(HalamanId.HasilAtap, new HasilLokasi($"{kota.Nama}, {kota.Provinsi}, Indonesia", kota.Lat, kota.Lon, "kota"));
    }
}
