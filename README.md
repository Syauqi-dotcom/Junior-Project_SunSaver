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

### Koneksi langsung ke PostgreSQL (Modul 8)

`Data/Postgres/` — `RepositoriBasis` (abstract: validasi + template method) → `RepositoriPostgres` (Npgsql).
Isi `SUNSAVER_PG_CONNECTION` (atau `Postgres:ConnectionString` di `appsettings.json`), mis.
`Host=localhost;Port=5432;Database=sunsaver;Username=postgres;Password=...`, lalu jalankan migrasi
`supabase/migrations/*.sql` (untuk PostgreSQL biasa, jalankan `supabase/tests/stub_auth_lokal.sql` lebih dulu).
Setelah masuk akun, "Instalasi Saya" & riwayat estimasi disimpan langsung ke database tersebut.

### Deploy backend ke Supabase

1. Buat project di [supabase.com](https://supabase.com).
2. Buka **SQL Editor**, tempel dan jalankan `supabase/migrations/20261022000000_skema_awal.sql`
   (atau `supabase link --project-ref <ref>` lalu `supabase db push`).
3. Salin **Project URL** dan **publishable key** (Project Settings → API Keys).
4. Di aplikasi: **Pengaturan** → isi URL + key → *Uji koneksi* → *Simpan*, lalu **Masuk / Daftar**.
   Alternatif: isi `SunSaver.App/appsettings.json` atau env `SUNSAVER_SUPABASE_URL` / `SUNSAVER_SUPABASE_KEY`.

Jangan pernah memakai *secret/service_role key* di aplikasi (dialog Pengaturan menolaknya).
Jika *Confirm email* aktif di Supabase Auth, pengguna baru harus klik tautan di email sebelum bisa masuk.

### Mekanisme demo

Durasi ±10 menit. Aplikasi butuh internet untuk Open-Meteo, citra satelit, dan pencarian alamat.

#### 1. Persiapan (H-1 / sebelum presentasi)

| Langkah | Tujuan |
|---|---|
| Jalankan `SunSaver.exe` hasil publish (atau `dotnet run --project SunSaver.App`) | Pastikan aplikasi terbuka tanpa galat |
| Cari dan buka lokasi demo (mis. *Jl. Kaliurang, Sleman*), zoom-in di peta, catat koordinat di bawah alamat | Data iklim dan tile satelit tersimpan di cache `data/cache`; koordinat dipakai bila internet putus |
| Buka halaman **Jelajah Data** sampai 12 kota selesai dimuat | Saat demo tabel langsung tampil |
| Tutup aplikasi, lalu hapus `data/instalasi-lokal.json` | Mulai demo dengan daftar instalasi kosong |
| (Opsional, demo cloud) Siapkan project Supabase, jalankan migrasi, matikan *Confirm email*, isi **Pengaturan** | Login bisa langsung tanpa verifikasi email |
| Siapkan dua akun uji, mis. `demo1@contoh.id` dan `demo2@contoh.id` | Untuk menunjukkan Row Level Security |

Folder `data` berada di samping `.exe`. Hapus `data/sesi.bin` bila ingin keluar akun secara paksa.

#### 2. Skenario demo

| No | Halaman | Yang dilakukan | Yang dijelaskan (fitur W1) |
|---|---|---|---|
| 1 | **Beranda** | Ketik alamat atau klik contoh *Jl. Kaliurang, Sleman* → **Cek atap saya** | Alur mirip Project Sunroof; geocoding OpenStreetMap Nominatim; bisa juga input koordinat `-7.7956, 110.3695` |
| 2 | **Atap Anda** | Tunjukkan panel kanan: jam sinar matahari efektif per tahun | Data radiasi *shortwave*, durasi penyinaran, tutupan awan setahun dari Open-Meteo, tanpa API key |
| 3 | **Atap Anda** | Klik **Gambar atap** → klik sudut-sudut atap → klik titik hijau / Enter | Luas atap (m²) dihitung otomatis, atap diwarnai seperti heatmap Sunroof, jumlah panel maksimum menyesuaikan |
| 4 | **Atap Anda** | Geser slider tagihan (mis. Rp 500.000), ganti golongan tarif | Estimasi dihitung ulang langsung (kalkulator energi) |
| 5 | **Estimasi penghematan** | Klik **Lihat penghematan saya** → geser *Sesuaikan jumlah panel* → **Pakai rekomendasi** | Rekomendasi kWp dari konsumsi ÷ hasil spesifik lokasi; cakupan kebutuhan listrik |
| 6 | **Estimasi penghematan** | Pindah **Beli tunai** ↔ **Cicilan**, arahkan kursor ke grafik | Proyeksi arus kas 20 tahun, balik modal, tagihan bulanan tanpa vs dengan panel (tanpa net-metering, Permen ESDM 2/2024) |
| 7 | **Estimasi penghematan** | Gulir ke *Dampak lingkungan* dan *Asumsi perhitungan*, ubah faktor emisi | Kalkulator karbon: CO₂ = kWh × faktor emisi grid; setara pohon; lencana gamifikasi |
| 8 | **Estimasi penghematan** | Klik **Simpan ke Instalasi Saya** | Data tersimpan (lokal atau Supabase, lihat label di header) |
| 9 | **Dashboard Dampak** | Pilih instalasi di dropdown → tunjukkan kondisi saat ini, grafik 7 hari, kurva per jam, komparasi biaya bulanan | Dashboard dampak lingkungan + data *real-time* (disegarkan tiap 10 menit) |
| 10 | **Dashboard Dampak** | Gulir ke *Hutan virtual* → klik **Simpan snapshot** | Gamifikasi "setara pohon", level lencana, riwayat estimasi tersimpan di tabel `riwayat_estimasi` |
| 11 | **Instalasi Saya** | Klik **+ Instalasi baru**, kosongkan nama lalu **Simpan** → isi data → **Cari koordinat dari alamat** → simpan, ubah, lalu **Hapus** | CRUD + validasi input + pratinjau estimasi otomatis |
| 12 | **Jelajah Data** | Tunjukkan peringkat kota → klik dua kali satu kota | Perbandingan potensi surya 12 kota Indonesia; membuka kota di peta |
| 13 | **Tentang** | Tunjukkan rumus dan referensi | Metodologi perhitungan dan sumber literatur W1 |

Angka contoh (Jl. Kaliurang, tagihan Rp 500.000, R-1/1.300 VA, atap ±216 m²): sekitar 7 panel / 2,8 kWp,
cakupan ±97%, balik modal ±7 tahun. Angka bisa sedikit berbeda karena memakai data iklim tahun terakhir.

Bila demo dilakukan malam hari, kartu *Kondisi saat ini* menunjukkan radiasi 0 W/m² dan daya 0 kW. Itu memang benar;
arahkan penjelasan ke prakiraan 7 hari dan kurva per jam.

#### 3. Demo backend cloud (Supabase)

1. **Pengaturan** → *Uji koneksi* → muncul pesan bahwa tabel `tarif_listrik` berisi 7 golongan tarif.
2. **Masuk** → tab **Daftar akun baru** → daftar sebagai `demo1@contoh.id`. Label header berubah menjadi *Cloud · Supabase*.
3. Simpan satu instalasi, lalu buka Supabase **Table Editor** → tabel `instalasi` dan `riwayat_estimasi`: data baru muncul dengan `pengguna_id` milik akun tersebut.
4. Keluar (menu nama akun → **Keluar**), masuk sebagai `demo2@contoh.id` → **Instalasi Saya** kosong. Ini bukti Row Level Security: pengguna hanya melihat datanya sendiri.
5. (Opsional) Tempel *secret key* di **Pengaturan** → aplikasi menolak karena kunci itu melewati RLS.

Uji RLS yang sama bisa ditunjukkan tanpa Supabase lewat `supabase/tests/uji_rls.sql` di PostgreSQL lokal.

#### 4. Pembagian peran saat presentasi

| Anggota | Bagian demo |
|---|---|
| Garjita Adicandra (Software Architect & Systems) | Arsitektur (Core/Data/Ui), skema database Supabase, RLS (skenario cloud) |
| Muhammad Syauqi Fittuqo (Backend Developer & Data Logic) | Integrasi Open-Meteo, kalkulator energi/karbon/finansial (langkah 2, 4–7, 12) |
| Naufal Dzaky (Frontend Developer & UI/UX) | Alur UI mirip Sunroof, peta & gambar atap, dashboard dan grafik (langkah 1, 3, 8–11) |

#### 5. Rencana cadangan

| Masalah | Solusi |
|---|---|
| Alamat tidak ditemukan | Tulis lebih umum (jalan + kota) atau masukkan koordinat `lat, lon` |
| Peta lambat / kosong di zoom tinggi | Zoom-out satu tingkat atau klik **Peta jalan** |
| Internet putus | Ketik koordinat yang dicatat saat persiapan di kotak cari Beranda (input koordinat tidak butuh internet). Halaman Atap Anda, Estimasi, dan Instalasi Saya tetap jalan dari cache. Pencarian nama alamat dan Dashboard (prakiraan real-time) tetap butuh internet |
| Supabase gagal / lupa kata sandi | Demo tetap jalan di mode lokal; tunjukkan uji RLS lokal |
| Aplikasi galat | Lihat `data/galat.log`, buka ulang aplikasi |

### Catatan progres

- [x] Core: Open-Meteo, geocoding, kalkulator energi/karbon/finansial (diuji terhadap API asli)
- [x] Backend Supabase: skema, RLS, trigger, RPC — diuji di PostgreSQL 17 lokal (`supabase/tests/uji_rls.sql`)
- [x] UI Windows Forms: 7 halaman + dialog Masuk & Pengaturan (diperiksa lewat tangkapan layar)
- [x] Build rilis self-contained
- [ ] Hubungkan ke project Supabase milik kelompok (butuh akun; langkah di atas)
- [ ] Uji login/daftar & sinkron cloud end-to-end setelah project Supabase dibuat
