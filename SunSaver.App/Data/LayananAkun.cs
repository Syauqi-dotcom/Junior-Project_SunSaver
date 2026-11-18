using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SunSaver.App.Core;
using SunSaver.App.Data.Postgres;
using SunSaver.App.Data.Supabase;

namespace SunSaver.App.Data;

/// <summary>
/// Status akun & sumber data aplikasi.
/// <list type="bullet">
/// <item>Supabase diatur + sudah masuk → data instalasi disimpan di cloud (PostgreSQL Supabase, dilindungi RLS).</item>
/// <item>Selain itu → mode lokal (berkas JSON di folder data).</item>
/// </list>
/// Refresh token disimpan terenkripsi DPAPI (hanya bisa dibuka akun Windows yang sama).
/// </summary>
public sealed class LayananAkun
{
    private static readonly byte[] Entropi = "SunSaver.Sesi.v1"u8.ToArray();
    private readonly string _berkasSesi = FolderData.Berkas("sesi.bin");
    private readonly RepositoriLokal _lokal = new();
    private RepositoriSupabase? _cloud;
    private readonly RepositoriPostgres? _postgres;

    public LayananAkun()
    {
        // Worksheet 8: bila connection string PostgreSQL diatur, data disimpan langsung ke database tsb.
        var pg = PengaturanPostgres.Muat();
        if (pg.Length > 0) _postgres = new RepositoriPostgres(pg, () => Sesi?.PenggunaId);
        TerapkanPengaturan(PenyimpanPengaturan.Muat(), simpan: false);
    }

    public PengaturanSupabase Pengaturan { get; private set; } = PengaturanSupabase.Kosong;
    public SupabaseKlien? Supabase { get; private set; }
    public Sesi? Sesi => Supabase?.Sesi;

    public bool CloudDiatur => Supabase is not null;
    public bool SudahMasuk => Sesi is not null;

    // Polimorfisme: pemanggil hanya memakai IRepositoriInstalasi; implementasi dipilih saat runtime.
    public IRepositoriInstalasi Repositori =>
        SudahMasuk && _postgres is not null ? _postgres
        : SudahMasuk && _cloud is not null ? _cloud
        : _lokal;

    public string LabelMode => SudahMasuk ? (_postgres is not null ? "Database · PostgreSQL" : "Cloud · Supabase") : CloudDiatur ? "Lokal · belum masuk" : "Mode lokal";

    /// <summary>Dipicu saat login/logout/pengaturan berubah (dipanggil di thread pemanggil).</summary>
    public event EventHandler? Berubah;

    public void TerapkanPengaturan(PengaturanSupabase p, bool simpan = true)
    {
        p = p.Normalisasi();
        if (simpan) PenyimpanPengaturan.Simpan(p);

        if (Supabase is not null) Supabase.SesiBerubah -= SimpanSesi;
        var berganti = Pengaturan != p;
        Pengaturan = p;

        if (p.Lengkap)
        {
            Supabase = new SupabaseKlien(p);
            Supabase.SesiBerubah += SimpanSesi;
            _cloud = new RepositoriSupabase(Supabase);
        }
        else
        {
            Supabase = null;
            _cloud = null;
        }

        if (berganti && simpan) HapusSesiTersimpan();
        Berubah?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Coba pulihkan sesi dari refresh token tersimpan. Diam-diam gagal → tetap mode lokal.</summary>
    public async Task PulihkanSesiAsync()
    {
        if (Supabase is null || BacaSesiTersimpan() is not { } s || s.Url != Pengaturan.Url) return;
        try
        {
            await Supabase.SegarkanAsync(s.RefreshToken);
        }
        catch (SupabaseException)
        {
            HapusSesiTersimpan();
        }
        catch (LayananLuarException)
        {
            // offline: biarkan berkas sesi untuk dicoba lagi saat aplikasi dibuka berikutnya
        }
        Berubah?.Invoke(this, EventArgs.Empty);
    }

    public async Task<HasilDaftar> DaftarAsync(string email, string sandi, string nama)
    {
        var h = await Klien().DaftarAsync(email, sandi, nama);
        Berubah?.Invoke(this, EventArgs.Empty);
        return h;
    }

    public async Task MasukAsync(string email, string sandi)
    {
        await Klien().MasukAsync(email, sandi);
        Berubah?.Invoke(this, EventArgs.Empty);
    }

    public async Task KeluarAsync()
    {
        if (Supabase is not null) await Supabase.KeluarAsync();
        HapusSesiTersimpan();
        Berubah?.Invoke(this, EventArgs.Empty);
    }

    private SupabaseKlien Klien() => Supabase
        ?? throw new SupabaseException("Supabase belum diatur. Isi URL project dan publishable/anon key di menu Pengaturan.",
            System.Net.HttpStatusCode.PreconditionFailed);

    // ---------------------------- penyimpanan sesi (DPAPI) ----------------------------

    private sealed record SesiTersimpan(string Url, string RefreshToken, string Email);

    private void SimpanSesi(Sesi? s)
    {
        try
        {
            if (s is null)
            {
                HapusSesiTersimpan();
                return;
            }
            var json = JsonSerializer.SerializeToUtf8Bytes(new SesiTersimpan(Pengaturan.Url, s.RefreshToken, s.Email));
            FolderData.TulisAtomik(_berkasSesi, ProtectedData.Protect(json, Entropi, DataProtectionScope.CurrentUser));
        }
        catch (Exception e) when (e is IOException or CryptographicException or UnauthorizedAccessException)
        {
            // gagal menyimpan sesi hanya berarti pengguna perlu login lagi lain kali
        }
    }

    private SesiTersimpan? BacaSesiTersimpan()
    {
        try
        {
            if (!File.Exists(_berkasSesi)) return null;
            var json = ProtectedData.Unprotect(File.ReadAllBytes(_berkasSesi), Entropi, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<SesiTersimpan>(Encoding.UTF8.GetString(json));
        }
        catch (Exception e) when (e is IOException or CryptographicException or JsonException)
        {
            HapusSesiTersimpan();
            return null;
        }
    }

    private void HapusSesiTersimpan()
    {
        try
        {
            if (File.Exists(_berkasSesi)) File.Delete(_berkasSesi);
        }
        catch (IOException)
        {
            // abaikan
        }
    }
}
