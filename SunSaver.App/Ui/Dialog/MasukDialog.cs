using System.Net.Mail;
using SunSaver.App.Data;
using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App.Ui.Dialog;

/// <summary>Masuk / daftar akun SunSaver (Supabase Auth, email + kata sandi).</summary>
public sealed class MasukDialog : DialogDasar
{
    private readonly LayananAkun _akun;
    private readonly TombolBulat _tabMasuk = new("Masuk", GayaTombol.Pil);
    private readonly TombolBulat _tabDaftar = new("Daftar akun baru", GayaTombol.Pil);
    private readonly TextBox _nama = Isian.Teks("Nama Anda", 80);
    private readonly TextBox _email = Isian.Teks("nama@email.com", 254);
    private readonly TextBox _sandi = Isian.Teks("Minimal 8 karakter", 128);
    private readonly TextBox _ulangi = Isian.Teks("Ketik ulang kata sandi", 128);
    private readonly Tumpukan _bidangNama, _bidangUlangi;
    private readonly LabelBungkus _status = Teks.Bungkus("", Tema.Font(9), Tema.Merah);
    private readonly TombolBulat _kirim = new("Masuk");
    private bool _modeDaftar;

    public MasukDialog(LayananAkun akun, bool daftar = false) : base("Akun SunSaver", 460)
    {
        _akun = akun;
        _sandi.UseSystemPasswordChar = true;
        _ulangi.UseSystemPasswordChar = true;
        _email.AccessibleName = "Email";
        _sandi.AccessibleName = "Kata sandi";

        var judul = Teks.Judul("Simpan instalasi Anda di cloud", 15);
        var info = Teks.Bungkus("Akun disimpan dengan Supabase Auth. Data instalasi Anda tersimpan di database PostgreSQL " +
                                "Supabase dan hanya bisa dibaca oleh akun Anda sendiri (Row Level Security).", Tema.Font(9.5f));

        var tab = new Baris { Jarak = Tema.S(8), Margin = new Padding(0, Tema.S(6), 0, Tema.S(16)) };
        tab.Tambah(_tabMasuk.Pas()).Tambah(_tabDaftar.Pas());
        _tabMasuk.Click += (_, _) => AturMode(false);
        _tabDaftar.Click += (_, _) => AturMode(true);

        _bidangNama = Isian.Bidang("Nama", _nama);
        _bidangUlangi = Isian.Bidang("Ulangi kata sandi", _ulangi);
        Isi.Controls.AddRange([judul, info, tab, _bidangNama, Isian.Bidang("Email", _email), Isian.Bidang("Kata sandi", _sandi), _bidangUlangi, _status]);

        if (!akun.CloudDiatur)
        {
            _status.ForeColor = Tema.OranyeTua;
            _status.Text = "Supabase belum diatur. Isi URL project dan publishable/anon key di menu Pengaturan terlebih dahulu.";
            _kirim.Enabled = false;
        }

        var batal = new TombolBulat("Batal", GayaTombol.Teks);
        batal.Click += (_, _) => Close();
        _kirim.Click += async (_, _) => await KirimAsync();
        BarisTombol.Tambah(batal).Tambah(_kirim);
        AcceptButton = _kirim;
        CancelButton = batal;

        AturMode(daftar);
    }

    private void AturMode(bool daftar)
    {
        _modeDaftar = daftar;
        _tabMasuk.Terpilih = !daftar;
        _tabDaftar.Terpilih = daftar;
        _bidangNama.Visible = daftar;
        _bidangUlangi.Visible = daftar;
        _kirim.Text = daftar ? "Daftar" : "Masuk";
        _kirim.Pas(Tema.S(110));
        if (_akun.CloudDiatur) _status.Text = "";
        Isi.PerformLayout();
        BarisTombol.PerformLayout();
        if (IsHandleCreated) SesuaikanTinggi();
        (daftar ? _nama : _email).Focus();
    }

    private string? Validasi()
    {
        var email = _email.Text.Trim();
        if (!MailAddress.TryCreate(email, out _)) return "Format email tidak valid.";
        if (_sandi.Text.Length < (_modeDaftar ? 8 : 1)) return _modeDaftar ? "Kata sandi minimal 8 karakter." : "Kata sandi wajib diisi.";
        if (_modeDaftar)
        {
            if (_nama.Text.Trim().Length < 2) return "Nama minimal 2 karakter.";
            if (_sandi.Text != _ulangi.Text) return "Kata sandi dan ulangan tidak sama.";
            if (!_sandi.Text.Any(char.IsLetter) || !_sandi.Text.Any(char.IsDigit)) return "Gunakan campuran huruf dan angka.";
        }
        return null;
    }

    private async Task KirimAsync()
    {
        _status.ForeColor = Tema.Merah;
        if (Validasi() is { } galat)
        {
            _status.Text = galat;
            SesuaikanTinggi();
            return;
        }

        _kirim.Enabled = false;
        UseWaitCursor = true;
        try
        {
            if (_modeDaftar)
            {
                var hasil = await _akun.DaftarAsync(_email.Text.Trim(), _sandi.Text, _nama.Text.Trim());
                if (hasil.PerluKonfirmasiEmail)
                {
                    _status.ForeColor = Tema.Hijau;
                    _status.Text = "Pendaftaran berhasil. Buka tautan konfirmasi yang dikirim ke email Anda, lalu masuk.";
                    AturMode(false);
                    _status.ForeColor = Tema.Hijau;
                    _status.Text = "Pendaftaran berhasil. Buka tautan konfirmasi yang dikirim ke email Anda, lalu masuk.";
                    SesuaikanTinggi();
                    return;
                }
            }
            else
            {
                await _akun.MasukAsync(_email.Text.Trim(), _sandi.Text);
            }
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _status.Text = KonteksAplikasi.PesanGalat(e);
            SesuaikanTinggi();
        }
        finally
        {
            _kirim.Enabled = _akun.CloudDiatur;
            UseWaitCursor = false;
            _sandi.Clear();
            _ulangi.Clear();
        }
    }
}
