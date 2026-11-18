using SunSaver.App.Core.Model;

namespace SunSaver.App.Data.Postgres;

/// <summary>
/// WORKSHEET 8 — kelas dasar abstrak untuk repositori berbasis database.
/// <list type="bullet">
/// <item><b>Inheritance</b>: <see cref="RepositoriPostgres"/> mewarisi kelas ini.</item>
/// <item><b>Polymorphism</b>: UI hanya mengenal <see cref="IRepositoriInstalasi"/>; implementasinya
///   (Lokal / Supabase REST / PostgreSQL langsung) dipilih saat runtime.</item>
/// <item><b>Encapsulation</b>: aturan validasi dikunci di sini (<c>private</c>/<c>protected</c>);
///   subkelas hanya mengisi langkah penyimpanan lewat method abstrak <c>*Internal</c>.</item>
/// </list>
/// </summary>
public abstract class RepositoriBasis : IRepositoriInstalasi
{
    public abstract string Nama { get; }

    // ---- Template method: urutan "validasi → simpan" tetap, subkelas tidak bisa melewatinya ----

    public async Task<Instalasi> SimpanAsync(Instalasi instalasi, CancellationToken ct = default)
    {
        Validasi(instalasi);
        return await SimpanInternalAsync(Normalisasi(instalasi), ct).ConfigureAwait(false);
    }

    public async Task<RiwayatEstimasi> TambahRiwayatAsync(RiwayatEstimasi riwayat, CancellationToken ct = default)
    {
        if (riwayat.InstalasiId <= 0)
            throw new ArgumentException("Riwayat harus terkait dengan instalasi yang sudah tersimpan.", nameof(riwayat));
        return await TambahRiwayatInternalAsync(riwayat, ct).ConfigureAwait(false);
    }

    public abstract Task<List<Instalasi>> DaftarAsync(CancellationToken ct = default);
    public abstract Task HapusAsync(long id, CancellationToken ct = default);
    public abstract Task<List<RiwayatEstimasi>> RiwayatAsync(long instalasiId, CancellationToken ct = default);

    protected abstract Task<Instalasi> SimpanInternalAsync(Instalasi instalasi, CancellationToken ct);
    protected abstract Task<RiwayatEstimasi> TambahRiwayatInternalAsync(RiwayatEstimasi riwayat, CancellationToken ct);

    // ---- Aturan yang sama dengan CHECK constraint di skema SQL (supabase/migrations) ----

    private static void Validasi(Instalasi i)
    {
        if (string.IsNullOrWhiteSpace(i.Nama) || i.Nama.Trim().Length > 80)
            throw new ArgumentException("Nama instalasi wajib diisi (maksimal 80 karakter).", nameof(i));
        if (i.Latitude is < -90 or > 90 || i.Longitude is < -180 or > 180)
            throw new ArgumentException("Koordinat tidak valid.", nameof(i));
        if (i.KapasitasKwp is <= 0 or > 100)
            throw new ArgumentException("Kapasitas harus antara 0 dan 100 kWp.", nameof(i));
        if (i.JumlahPanel is < 1 or > 300)
            throw new ArgumentException("Jumlah panel harus 1–300.", nameof(i));
        if (i.TarifPerKwh is < 100 or > 10_000)
            throw new ArgumentException("Tarif per kWh harus 100–10.000.", nameof(i));
    }

    private static Instalasi Normalisasi(Instalasi i)
    {
        var s = i.Salin();
        s.Nama = i.Nama.Trim();
        s.Alamat = (i.Alamat ?? "").Trim();
        s.PoligonAtap = i.PoligonAtap is { Count: >= 3 } p ? p : null;
        s.KodeTarif = string.IsNullOrWhiteSpace(i.KodeTarif) ? null : i.KodeTarif;
        return s;
    }
}
