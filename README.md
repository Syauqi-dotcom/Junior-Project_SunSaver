#SunSaver <br>
sun-kan kepalamu <br>
Nama Kelompok 27 <br>
Ketua Kelompok : Garjita Adicandra (24/535330/TK/59377) <br>
Anggota 1 :  Muhammad Syauqi Fittuqo (24/543713/TK/60433) <br>
Anggota 2 :  Naufal Dzaky (24/543697/TK/60431) <br>

Modul 3 (SunSaver Class Diagram)
<img width="1600" height="795" alt="Class Diagram Sunsaver" src="https://github.com/user-attachments/assets/1a44c672-2aa0-4670-903c-e12784f7462f" />

## SunSaver Desktop (Windows Forms + Supabase)

Aplikasi desktop C# (.NET 10, Windows Forms) dengan UI/UX terinspirasi Google Project Sunroof.
Fitur sesuai dokumen ide (W1):

| Fitur W1 | Implementasi |
|---|---|
| Integrasi Open-Meteo (radiasi, suhu, tutupan awan, tanpa API key) | `Core/Cuaca` — arsip harian setahun (ERA5) + prakiraan 7 hari & kondisi real-time |
| Kalkulator energi & karbon | `Core/Kalkulasi` — kWh dari spesifikasi panel, CO₂ = kWh × faktor emisi grid, setara pohon |
| Dashboard dampak lingkungan | Halaman *Dashboard Dampak*: grafik produksi vs radiasi, komparasi tagihan bulanan, hutan virtual & lencana |
| Penyimpanan data pengguna | Supabase (PostgreSQL + Auth + REST, Row Level Security); mode lokal JSON bila belum masuk |

Halaman: **Beranda** (cari alamat) → **Atap Anda** (peta satelit, gambar atap, luas otomatis) →
**Estimasi penghematan** (ukuran sistem, tunai vs cicilan, arus kas 20 tahun) · **Dashboard Dampak** ·
**Instalasi Saya** (CRUD) · **Jelajah Data** (12 kota) · **Tentang** (metodologi & referensi).

### Struktur

```
SunSaver.App/
  Core/      model, Open-Meteo, Nominatim, kalkulator energi/karbon/finansial, estimasi
  Data/      klien Supabase (Auth + PostgREST), repositori cloud & lokal, pengaturan, sesi (DPAPI)
  Ui/        tema, kontrol kustom, grafik GDI+, peta satelit, dialog, halaman
supabase/
  migrations/  skema database (jalankan di Supabase)
  tests/       uji RLS untuk PostgreSQL lokal
```

### Menjalankan

```bash
dotnet run --project SunSaver.App
```

Butuh .NET 10 SDK (Windows). Tanpa Supabase aplikasi tetap jalan dalam **mode lokal**.
Data aplikasi (cache cuaca/peta, data lokal, sesi) disimpan di folder `data` di samping `.exe`
(atau env `SUNSAVER_DATA_DIR`).

Build rilis (satu file `.exe`, tanpa perlu install .NET):

```bash
dotnet publish SunSaver.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

### Deploy backend ke Supabase

1. Buat project di [supabase.com](https://supabase.com).
2. Buka **SQL Editor**, tempel dan jalankan `supabase/migrations/20261022000000_skema_awal.sql`
   (atau `supabase link --project-ref <ref>` lalu `supabase db push`).
3. Salin **Project URL** dan **publishable key** (Project Settings → API Keys).
4. Di aplikasi: **Pengaturan** → isi URL + key → *Uji koneksi* → *Simpan*, lalu **Masuk / Daftar**.
   Alternatif: isi `SunSaver.App/appsettings.json` atau env `SUNSAVER_SUPABASE_URL` / `SUNSAVER_SUPABASE_KEY`.

Jangan pernah memakai *secret/service_role key* di aplikasi (dialog Pengaturan menolaknya).
Jika *Confirm email* aktif di Supabase Auth, pengguna baru harus klik tautan di email sebelum bisa masuk.

### Catatan progres

- [x] Core: Open-Meteo, geocoding, kalkulator energi/karbon/finansial (diuji terhadap API asli)
- [x] Backend Supabase: skema, RLS, trigger, RPC — diuji di PostgreSQL 17 lokal (`supabase/tests/uji_rls.sql`)
- [x] UI Windows Forms: 7 halaman + dialog Masuk & Pengaturan (diperiksa lewat tangkapan layar)
- [x] Build rilis self-contained
- [ ] Hubungkan ke project Supabase milik kelompok (butuh akun; langkah di atas)
- [ ] Uji login/daftar & sinkron cloud end-to-end setelah project Supabase dibuat
