using System.Drawing.Drawing2D;
using SunSaver.App.Core.Model;
using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App.Ui.Halaman;

/// <summary>
/// Beranda bergaya hero Project Sunroof: judul besar, kotak pencarian alamat di tengah,
/// ilustrasi atap bertenaga surya, lalu penjelasan "cara kerja".
/// </summary>
public sealed class BerandaHalaman : HalamanDasar
{
    private readonly PanelGulir _gulir = new() { LebarMaks = int.MaxValue, Padding = Padding.Empty };
    private readonly Hero _hero;

    public BerandaHalaman(KonteksAplikasi k) : base(k)
    {
        _hero = new Hero();
        _hero.Cari.Cari += async (_, q) => await CariAsync(q);
        _hero.Cari.HasilDipilih += (_, h) => BukaLokasi(h);
        foreach (var (teks, kueri) in Contoh)
        {
            var link = new LinkLabel
            {
                Text = teks, AutoSize = true, Font = Tema.Font(9.5f), LinkColor = Tema.Biru, ActiveLinkColor = Tema.BiruTekan,
                LinkBehavior = LinkBehavior.HoverUnderline, BackColor = Color.Transparent,
            };
            link.LinkClicked += async (_, _) => { _hero.Cari.Teks = kueri; await CariAsync(kueri); };
            _hero.TambahContoh(link);
        }

        var isi = new Tumpukan { Padding = Tema.P(32, 40, 32, 48), LebarIsiMaks = Tema.S(1100) };
        isi.Controls.Add(Teks.Judul("Cara kerja SunSaver", 20));
        isi.Controls.Add(Teks.Bungkus("Tiga langkah untuk mengetahui apakah panel surya cocok untuk rumah Anda, " +
                                      "berdasarkan data cuaca nyata di lokasi Anda.", Tema.Font(11)));

        var langkah = new GridKartu { Kolom = 3, LebarMinKolom = Tema.S(260), Margin = new Padding(0, Tema.S(16), 0, Tema.S(36)) };
        langkah.Controls.Add(KartuLangkah(Ikon.Pin, Tema.Merah, Tema.MerahMuda, "1. Cari alamat Anda",
            "Masukkan alamat atau koordinat. SunSaver mengambil data radiasi matahari, durasi penyinaran, suhu, dan tutupan " +
            "awan dari Open-Meteo untuk titik tersebut."));
        langkah.Controls.Add(KartuLangkah(Ikon.Pensil, Tema.OranyeTua, Tema.KuningMuda, "2. Gambar atap rumah",
            "Klik sudut-sudut atap Anda di citra satelit. Luas atap dihitung otomatis untuk menentukan berapa panel yang muat."));
        langkah.Controls.Add(KartuLangkah(Ikon.Daun, Tema.Hijau, Tema.HijauMuda, "3. Lihat hemat & dampak",
            "Sesuaikan tagihan listrik bulanan untuk melihat ukuran sistem yang direkomendasikan, penghematan 20 tahun, " +
            "dan pengurangan COâ‚‚ setara jumlah pohon."));
        isi.Controls.Add(langkah);

        var fitur = new GridKartu { Kolom = 2, LebarMinKolom = Tema.S(340), Margin = new Padding(0, 0, 0, Tema.S(28)) };
        fitur.Controls.Add(KartuFitur("Data cuaca, bukan tebakan",
            "Potensi dihitung dari data harian satu tahun penuh (Open-Meteo Archive/ERA5) dan prakiraan 7 hari untuk dashboard real-time. " +
            "Tidak bergantung pada citra resolusi tinggi yang belum menjangkau semua daerah.",
            "Jelajahi data kota di Indonesia", () => K.Shell.Ke(HalamanId.Jelajah)));
        fitur.Controls.Add(KartuFitur("Simpan & pantau instalasi",
            "Simpan rencana pemasangan ke \"Instalasi Saya\". Masuk dengan akun untuk menyinkronkan data ke cloud (Supabase), " +
            "lalu pantau produksi, penghematan, dan lencana dampak lingkungan Anda.",
            "Buka Instalasi Saya", () => K.Shell.Ke(HalamanId.Instalasi)));
        isi.Controls.Add(fitur);

        isi.Controls.Add(new Pemisah());
        isi.Controls.Add(Teks.Bungkus("Data cuaca: Open-Meteo.com (CC BY 4.0) Â· Citra satelit: Esri World Imagery Â· Peta & geocoding: " +
                                      "Â© kontributor OpenStreetMap (ODbL) Â· Estimasi bersifat indikatif, bukan penawaran pemasangan.",
            Tema.Font(8.5f), Tema.TeksPudar));

        _gulir.Controls.Add(_hero);
        _gulir.Controls.Add(isi);
        Controls.Add(_gulir);
    }

    public override string Judul => "Beranda";

    private static readonly (string Teks, string Kueri)[] Contoh =
    [
        ("Jl. Kaliurang, Sleman", "Jalan Kaliurang, Sleman"),
        ("Malioboro, Yogyakarta", "Malioboro, Yogyakarta"),
        ("Kota Kupang", "Kupang"),
        ("-6.2088, 106.8456", "-6.2088, 106.8456"),
    ];

    public override void Tampil(object? argumen)
    {
        _gulir.KeAtas();
        BeginInvoke(() => _hero.Cari.FokusInput());
    }

    private async Task CariAsync(string kueri)
    {
        _hero.Cari.AturSibuk(true);
        try
        {
            var hasil = await K.Geocoder.CariAsync(kueri);
            if (hasil.Count == 0)
                K.Shell.Notifikasi("Alamat tidak ditemukan. Coba tulis lebih umum (mis. nama jalan + kota) atau masukkan koordinat \"lat, lon\".", galat: true);
            else if (hasil.Count == 1)
                BukaLokasi(hasil[0]);
            else
                _hero.Cari.TampilkanHasil(hasil);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            K.Shell.Notifikasi(KonteksAplikasi.PesanGalat(e), galat: true);
        }
        finally
        {
            _hero.Cari.AturSibuk(false);
        }
    }

    private void BukaLokasi(HasilLokasi h) => K.Shell.Ke(HalamanId.HasilAtap, h);

    private static Kartu KartuLangkah(Action<Graphics, RectangleF, Color> ikon, Color warna, Color latar, string judul, string isi)
    {
        var k = new Kartu { Margin = Padding.Empty, Padding = Tema.P(24) };
        k.Controls.Add(new IkonBulat(ikon, warna, latar) { Size = new Size(Tema.S(52), Tema.S(52)), Margin = new Padding(0, 0, 0, Tema.S(16)) });
        k.Controls.Add(Teks.Subjudul(judul));
        k.Controls.Add(Teks.Bungkus(isi, Tema.Font(10)));
        return k;
    }

    private static Kartu KartuFitur(string judul, string isi, string tautan, Action aksi)
    {
        var k = new Kartu { Margin = Padding.Empty, Padding = Tema.P(24), BackColor = Tema.LatarAbu, WarnaGaris = Color.Empty };
        k.Controls.Add(Teks.Subjudul(judul));
        k.Controls.Add(Teks.Bungkus(isi, Tema.Font(10)));
        var t = new TombolBulat(tautan + "  â†’", GayaTombol.Teks) { Margin = new Padding(-Tema.S(12), Tema.S(4), 0, 0) };
        t.Click += (_, _) => aksi();
        k.Controls.Add(t.Pas());
        return k;
    }

    /// <summary>Hero: langit gradasi, matahari, deretan atap dengan panel surya, judul & kotak cari.</summary>
    private sealed class Hero : Control, ITataLetak
    {
        private readonly List<LinkLabel> _contoh = [];
        private readonly Label _labelContoh;

        public Hero()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.UserPaint, true);
            BackColor = Tema.Hex("#EAF2FE");
            Cari = new KotakCari();
            Controls.Add(Cari);
            _labelContoh = new Label { Text = "Contoh:", AutoSize = true, Font = Tema.Font(9.5f), ForeColor = Tema.TeksRedup, BackColor = Color.Transparent };
            Controls.Add(_labelContoh);
            Margin = Padding.Empty;
        }

        public KotakCari Cari { get; }

        public void TambahContoh(LinkLabel l)
        {
            _contoh.Add(l);
            Controls.Add(l);
        }

        public int TinggiPreferensi(int lebar) => Tema.S(560);

        private const string JudulHero = "Petakan potensi hemat surya atap Anda";
        private const string SubHero = "Masukkan alamat untuk melihat berapa banyak listrik yang bisa dihasilkan atap Anda, " +
                                       "berapa rupiah yang bisa dihemat, dan seberapa besar dampaknya bagi bumi.";

        private int YJudul => Tema.S(84);

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (Cari is null || _labelContoh is null) return; // dipanggil sebelum konstruktor selesai
            var lebarCari = Math.Min(Tema.S(680), Width - Tema.S(48));
            var ySub = YJudul + Tema.S(60);
            var tinggiSub = TextRenderer.MeasureText(SubHero, Tema.Font(12), new Size(Math.Min(Tema.S(720), Width - Tema.S(64)), 0), TextFormatFlags.WordBreak).Height;
            Cari.SetBounds((Width - lebarCari) / 2, ySub + tinggiSub + Tema.S(28), lebarCari, Tema.S(64));

            var y = Cari.Bottom + Tema.S(12);
            var total = _labelContoh.PreferredWidth + _contoh.Sum(c => c.PreferredWidth + Tema.S(14));
            var x = (Width - total) / 2;
            _labelContoh.Location = new Point(x, y);
            x += _labelContoh.PreferredWidth + Tema.S(6);
            foreach (var c in _contoh)
            {
                c.Location = new Point(x, y);
                x += c.PreferredWidth + Tema.S(14);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Tema.Halus(g);
            using (var langit = new LinearGradientBrush(ClientRectangle, Tema.Hex("#DCE9FD"), Color.White, LinearGradientMode.Vertical))
                g.FillRectangle(langit, ClientRectangle);

            // matahari terbit di kanan atas
            var r = Tema.S(160f);
            var cx = Width * 0.86f; var cy = Tema.S(110f);
            for (var i = 3; i >= 1; i--)
            {
                using var halo = new SolidBrush(Tema.Alfa(Tema.Kuning, 18 * i));
                var rr = r * (0.5f + i * 0.28f);
                g.FillEllipse(halo, cx - rr / 2, cy - rr / 2, rr, rr);
            }
            using (var inti = new LinearGradientBrush(new RectangleF(cx - r / 4, cy - r / 4, r / 2, r / 2), Tema.Hex("#FFE57F"), Tema.Kuning, 45f))
                g.FillEllipse(inti, cx - r / 4, cy - r / 4, r / 2, r / 2);

            GambarAtap(g);

            var fJudul = Tema.Font(30);
            var rJudul = new Rectangle(Tema.S(24), YJudul, Width - Tema.S(48), Tema.S(56));
            TextRenderer.DrawText(g, JudulHero, fJudul, rJudul, Tema.Teks, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            var lebarSub = Math.Min(Tema.S(720), Width - Tema.S(64));
            var rSub = new Rectangle((Width - lebarSub) / 2, YJudul + Tema.S(60), lebarSub, Tema.S(80));
            TextRenderer.DrawText(g, SubHero, Tema.Font(12), rSub, Tema.TeksRedup, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
        }

        /// <summary>Siluet deretan rumah dengan panel surya di bagian bawah hero.</summary>
        private void GambarAtap(Graphics g)
        {
            var dasar = Height;
            var lebarRumah = Tema.S(180f);
            var n = (int)Math.Ceiling(Width / lebarRumah) + 1;
            for (var i = 0; i < n; i++)
            {
                var x = i * lebarRumah - Tema.S(40f) + (i % 2) * Tema.S(14f);
                var tinggi = Tema.S(70f + (i * 37 % 3) * 18);
                var puncak = dasar - tinggi - Tema.S(46f);
                var dinding = new RectangleF(x + Tema.S(16f), dasar - tinggi, lebarRumah - Tema.S(32f), tinggi);
                using (var bd = new SolidBrush(i % 2 == 0 ? Tema.Hex("#F1F3F4") : Tema.Hex("#E8EAED"))) g.FillRectangle(bd, dinding);

                var atap = new[] { new PointF(x, dasar - tinggi), new PointF(x + lebarRumah / 2, puncak), new PointF(x + lebarRumah, dasar - tinggi) };
                using (var ba = new SolidBrush(i % 3 == 0 ? Tema.Hex("#BF6A4A") : Tema.Hex("#9E5A44"))) g.FillPolygon(ba, atap);

                // panel surya pada sisi atap kiri (miring)
                using var bp = new SolidBrush(Tema.Hex("#1F3B73"));
                using var pg = new Pen(Tema.Hex("#7BA4E0"), 1f);
                for (var k = 0; k < 3; k++)
                {
                    var t0 = 0.18f + k * 0.24f;
                    var t1 = t0 + 0.2f;
                    PointF Lerp(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
                    var a0 = Lerp(atap[0], atap[1], t0); var a1 = Lerp(atap[0], atap[1], t1);
                    var turun = Tema.S(16f);
                    var panel = new[] { a0, a1, new PointF(a1.X + turun * 0.6f, a1.Y + turun), new PointF(a0.X + turun * 0.6f, a0.Y + turun) };
                    g.FillPolygon(bp, panel);
                    g.DrawPolygon(pg, panel);
                }
                using var jendela = new SolidBrush(Tema.Hex("#C9DDF8"));
                g.FillRectangle(jendela, dinding.X + dinding.Width * 0.2f, dinding.Y + dinding.Height * 0.3f, dinding.Width * 0.22f, dinding.Height * 0.3f);
                g.FillRectangle(jendela, dinding.X + dinding.Width * 0.58f, dinding.Y + dinding.Height * 0.3f, dinding.Width * 0.22f, dinding.Height * 0.3f);
            }
            using var tanah = new SolidBrush(Tema.Hex("#CEEAD6"));
            g.FillRectangle(tanah, 0, dasar - Tema.S(6), Width, Tema.S(6));
        }
    }
}
