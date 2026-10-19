#SunSaver <br>
sun-kan kepalamu <br>
Nama Kelompok 27 <br>
Ketua Kelompok : Garjita Adicandra (24/535330/TK/59377) <br>
Anggota 1 :  Muhammad Syauqi Fittuqo (24/543713/TK/60433) <br>
Anggota 2 :  Naufal Dzaky (24/543697/TK/60431) <br>

Modul 3 (SunSaver Class Diagram) 
<img width="1600" height="795" alt="Class Diagram Sunsaver" src="https://github.com/user-attachments/assets/1a44c672-2aa0-4670-903c-e12784f7462f" />

Modul 4 (Tampilan User Interface)

Dua tampilan WPF (C#, .NET 10) di `SunSaver.App/`:
1. **Dashboard Estimasi** — kartu ringkasan (produksi, penghematan, reduksi CO₂, setara pohon), grafik produksi harian vs radiasi, tabel rincian harian, dan info instalasi aktif.
2. **Instalasi Saya** — form lokasi, spesifikasi panel, dan tarif listrik dengan validasi inline serta pratinjau estimasi yang diperbarui otomatis.

Data masih data contoh (`SunSaver.App/Data/DataContoh.cs`); nanti diganti hasil Open-Meteo + kalkulator di `SunSaver.Core`.

Menjalankan (Windows):
```bash
dotnet run --project SunSaver.App
```
Di Linux/macOS hanya bisa `dotnet build` (WPF butuh Windows).
