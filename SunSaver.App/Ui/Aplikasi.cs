using SunSaver.App.Core;
using SunSaver.App.Core.Cuaca;
using SunSaver.App.Core.Estimasi;
using SunSaver.App.Core.Lokasi;
using SunSaver.App.Core.Model;
using SunSaver.App.Data;
using SunSaver.App.Data.Supabase;

namespace SunSaver.App.Ui;

public enum HalamanId { Beranda, HasilAtap, Estimasi, Dashboard, Instalasi, Jelajah, Tentang }

public interface IShell
{
    void Ke(HalamanId id, object? argumen = null);
    void Notifikasi(string pesan, bool galat = false);
    Task<bool> PastikanMasukAsync(string alasan);
    IWin32Window Jendela { get; }
}

/// <summary>
/// Layanan bersama + status "sesi analisis" ala Project Sunroof
/// (alamat terpilih â†’ gambar atap â†’ sesuaikan tagihan â†’ lihat penghematan).
/// </summary>
public sealed class KonteksAplikasi
{
    public KonteksAplikasi(IShell shell)
    {
        Shell = shell;
        Iklim = new LayananIklim(new OpenMeteoClient());
        Estimasi = new LayananEstimasi(Iklim);
        Geocoder = new NominatimGeocoder();
        Akun = new LayananAkun();
    }

    public IShell Shell { get; }
    public LayananIklim Iklim { get; }
    public LayananEstimasi Estimasi { get; }
    public NominatimGeocoder Geocoder { get; }
    public LayananAkun Akun { get; }

    /// <summary>Lokasi rumah yang sedang dianalisis.</summary>
    public HasilLokasi? Lokasi { get; set; }

    /// <summary>Masukan kalkulator terakhir (tagihan, tarif, luas atap, jumlah panel, asumsi).</summary>
    public EstimasiMasukan Masukan { get; set; } = new();

    public List<TitikGeo>? PoligonAtap { get; set; }

    public HasilEstimasi? HasilTerakhir { get; set; }

    /// <summary>Dipicu bila daftar instalasi berubah (disimpan/dihapus) agar halaman lain memuat ulang.</summary>
    public event EventHandler? InstalasiBerubah;
    public void UmumkanInstalasiBerubah() => InstalasiBerubah?.Invoke(this, EventArgs.Empty);

    /// <summary>Buat objek Instalasi dari sesi analisis saat ini (untuk disimpan ke "Instalasi Saya").</summary>
    public Instalasi InstalasiDariSesi()
    {
        var m = Masukan;
        var h = HasilTerakhir;
        var nama = Lokasi?.Nama.Split(',')[0].Trim() is { Length: > 0 } n ? n : "Rumah Saya";
        return new Instalasi
        {
            Nama = nama.Length > 80 ? nama[..80] : nama,
            Alamat = Lokasi?.Nama is { } a ? (a.Length > 300 ? a[..300] : a) : "",
            Latitude = m.Latitude,
            Longitude = m.Longitude,
            KapasitasKwp = Math.Round(h?.Sistem.KapasitasKwp ?? 3.2, 2),
            JumlahPanel = h?.Sistem.JumlahPanel ?? 8,
            DayaPanelWp = m.DayaPanelWp,
            EfisiensiPersen = m.EfisiensiPersen,
            LuasAtapM2 = m.LuasAtapM2 is { } l ? Math.Round(Math.Clamp(l, 1, 5000), 1) : null,
            PoligonAtap = PoligonAtap?.ToList(),
            KodeTarif = m.KodeTarif,
            TarifPerKwh = m.TarifPerKwh,
            TagihanBulananRp = m.TagihanBulananRp,
        };
    }

    public static EstimasiMasukan MasukanDari(Instalasi i, EstimasiMasukan? dasar = null) => (dasar ?? new EstimasiMasukan()) with
    {
        Latitude = i.Latitude,
        Longitude = i.Longitude,
        TagihanBulananRp = i.TagihanBulananRp,
        TarifPerKwh = i.TarifPerKwh,
        KodeTarif = i.KodeTarif,
        LuasAtapM2 = i.LuasAtapM2,
        JumlahPanel = Math.Max(1, i.JumlahPanel),
        DayaPanelWp = i.KapasitasKwp > 0 && i.JumlahPanel > 0 ? Math.Round(i.KapasitasKwp * 1000 / i.JumlahPanel, 1) : i.DayaPanelWp,
        EfisiensiPersen = i.EfisiensiPersen,
    };

    /// <summary>Pesan galat yang ramah untuk pengguna.</summary>
    public static string PesanGalat(Exception e) => e switch
    {
        SupabaseException s => s.Message,
        LayananLuarException l => l.Message,
        HttpRequestException => "Gagal terhubung ke internet.",
        TaskCanceledException => "Permintaan melebihi batas waktu. Coba lagi.",
        IOException io => "Gagal membaca/menulis berkas: " + io.Message,
        _ => "Terjadi kesalahan: " + e.Message,
    };
}

/// <summary>Kelas dasar halaman (UserControl) yang ditampilkan di area konten jendela utama.</summary>
public abstract class HalamanDasar : UserControl
{
    protected HalamanDasar(KonteksAplikasi konteks)
    {
        K = konteks;
        BackColor = Tema.Latar;
        Dock = DockStyle.Fill;
        Font = Tema.FontIsi;
        DoubleBuffered = true;
    }

    protected KonteksAplikasi K { get; }

    public abstract string Judul { get; }

    /// <summary>Dipanggil setiap kali halaman ditampilkan (argumen opsional dari navigasi).</summary>
    public virtual void Tampil(object? argumen) { }

    /// <summary>Jalankan aksi async dengan penanganan galat seragam (notifikasi snackbar).</summary>
    protected async Task Aman(Func<Task> aksi, string? pesanSukses = null)
    {
        try
        {
            await aksi();
            if (pesanSukses is not null) K.Shell.Notifikasi(pesanSukses);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            K.Shell.Notifikasi(KonteksAplikasi.PesanGalat(e), galat: true);
        }
    }
}
