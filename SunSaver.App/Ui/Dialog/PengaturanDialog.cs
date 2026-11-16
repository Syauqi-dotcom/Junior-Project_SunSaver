using System.Diagnostics;
using SunSaver.App.Core;
using SunSaver.App.Data;
using SunSaver.App.Data.Supabase;
using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App.Ui.Dialog;

/// <summary>Pengaturan koneksi Supabase (URL project + publishable/anon key) dan info folder data.</summary>
public sealed class PengaturanDialog : DialogDasar
{
    private readonly LayananAkun _akun;
    private readonly TextBox _url = Isian.Teks("https://xxxxxxxx.supabase.co", 200);
    private readonly TextBox _kunci = Isian.Teks("sb_publishable_… atau anon key (eyJ…)", 1000);
    private readonly LabelBungkus _status = Teks.Bungkus("", Tema.Font(9));

    public PengaturanDialog(LayananAkun akun) : base("Pengaturan SunSaver", 560)
    {
        _akun = akun;
        _url.Text = akun.Pengaturan.Url;
        _kunci.Text = akun.Pengaturan.AnonKey;
        _kunci.UseSystemPasswordChar = true;

        var tampilKunci = new CheckBox { Text = "Tampilkan kunci", AutoSize = true, Font = Tema.Font(9), Margin = new Padding(0, Tema.S(2), 0, Tema.S(12)) };
        tampilKunci.CheckedChanged += (_, _) => _kunci.UseSystemPasswordChar = !tampilKunci.Checked;

        var uji = new TombolBulat("Uji koneksi", GayaTombol.Sekunder) { Margin = new Padding(0, 0, 0, Tema.S(8)) };
        uji.Click += async (_, _) => await UjiAsync(uji);

        var bukaFolder = new LinkLabel { Text = FolderData.Akar, AutoSize = true, Font = Tema.Font(9), LinkColor = Tema.Biru, Margin = new Padding(0, 0, 0, Tema.S(12)) };
        bukaFolder.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{FolderData.Akar}\"") { UseShellExecute = true });

        Isi.Controls.AddRange(
        [
            Teks.Judul("Koneksi Supabase", 15),
            Teks.Bungkus("SunSaver memakai Supabase (PostgreSQL + Auth + REST API) sebagai backend cloud. Buat project gratis di " +
                         "supabase.com, jalankan skrip supabase/migrations/*.sql di SQL Editor, lalu salin Project URL dan " +
                         "publishable key (Project Settings → API Keys). Kunci ini aman disimpan di aplikasi karena akses data dibatasi Row Level Security. " +
                         "Jangan pernah memakai secret/service_role key di sini.", Tema.Font(9.5f)),
            Isian.Bidang("Project URL", _url),
            Isian.Bidang("Publishable / anon key", _kunci),
            tampilKunci,
            uji,
            _status,
            new Pemisah(),
            Teks.Subjudul("Folder data lokal"),
            Teks.Bungkus("Cache cuaca, cache peta, data mode lokal, dan sesi login (terenkripsi DPAPI) disimpan di:", Tema.Font(9.5f)),
            bukaFolder,
        ]);

        var batal = new TombolBulat("Batal", GayaTombol.Teks);
        batal.Click += (_, _) => Close();
        var simpan = new TombolBulat("Simpan");
        simpan.Click += (_, _) => Simpan();
        BarisTombol.Tambah(batal).Tambah(simpan.Pas(Tema.S(110)));
        CancelButton = batal;
    }

    private PengaturanSupabase Baca() => new PengaturanSupabase(_url.Text, _kunci.Text).Normalisasi();

    private string? Validasi(PengaturanSupabase p)
    {
        if (p.Url.Length == 0 && p.AnonKey.Length == 0) return null; // kosong = mode lokal
        if (!Uri.TryCreate(p.Url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
            return "URL harus diawali https:// (contoh: https://abcd1234.supabase.co).";
        if (p.AnonKey.Length < 20) return "Kunci terlalu pendek. Salin publishable/anon key dari dashboard Supabase.";
        if (p.AnonKey.StartsWith("sb_secret_", StringComparison.Ordinal) || TampakServiceRole(p.AnonKey))
            return "Itu secret/service_role key. Kunci ini melewati RLS dan TIDAK boleh dipakai di aplikasi klien. Gunakan publishable/anon key.";
        return null;
    }

    /// <summary>Deteksi JWT legacy dengan klaim role=service_role.</summary>
    private static bool TampakServiceRole(string kunci)
    {
        var bagian = kunci.Split('.');
        if (bagian.Length != 3) return false;
        try
        {
            var isi = bagian[1].Replace('-', '+').Replace('_', '/');
            isi = isi.PadRight(isi.Length + (4 - isi.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(isi));
            return json.Contains("\"service_role\"", StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task UjiAsync(TombolBulat tombol)
    {
        var p = Baca();
        if (Validasi(p) is { } g || !p.Lengkap)
        {
            Status(Validasi(p) ?? "Isi URL dan kunci terlebih dahulu.", Tema.Merah);
            return;
        }
        tombol.Enabled = false;
        Status("Menghubungi Supabase…", Tema.TeksRedup);
        try
        {
            var n = await new SupabaseKlien(p).UjiKoneksiAsync();
            Status(n > 0
                ? $"Terhubung. Tabel tarif_listrik berisi {n} golongan tarif — skema database sudah siap."
                : "Terhubung, tetapi tabel tarif_listrik kosong. Jalankan ulang skrip migrasi.", n > 0 ? Tema.Hijau : Tema.OranyeTua);
        }
        catch (Exception e) when (e is SupabaseException or LayananLuarException)
        {
            Status(e.Message, Tema.Merah);
        }
        finally
        {
            tombol.Enabled = true;
        }
    }

    private void Status(string pesan, Color warna)
    {
        _status.ForeColor = warna;
        _status.Text = pesan;
        SesuaikanTinggi();
    }

    private void Simpan()
    {
        var p = Baca();
        if (Validasi(p) is { } galat)
        {
            Status(galat, Tema.Merah);
            return;
        }
        _akun.TerapkanPengaturan(p);
        DialogResult = DialogResult.OK;
        Close();
    }
}
