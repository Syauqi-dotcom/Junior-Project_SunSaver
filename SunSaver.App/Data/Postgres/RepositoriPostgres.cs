using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using SunSaver.App.Core;
using SunSaver.App.Core.Model;

namespace SunSaver.App.Data.Postgres;

/// <summary>
/// WORKSHEET 8 — koneksi langsung aplikasi ke database PostgreSQL (Npgsql / ADO.NET),
/// memakai skema yang sama dengan <c>supabase/migrations</c> (tabel <c>instalasi</c> &amp; <c>riwayat_estimasi</c>).
/// Bisa mengarah ke PostgreSQL lokal maupun ke Postgres milik Supabase (connection string "Direct"/"Session pooler").
/// <para>Encapsulation: connection string dan <see cref="NpgsqlDataSource"/> bersifat <c>private</c>;
/// semua query berparameter (tanpa string concat) dan selalu difilter <c>pengguna_id</c> pemilik data.</para>
/// </summary>
public sealed class RepositoriPostgres : RepositoriBasis, IDisposable
{
    private const string KolomInstalasi =
        "id, nama, alamat, latitude, longitude, kapasitas_kwp, jumlah_panel, daya_panel_wp, efisiensi_persen, " +
        "luas_atap_m2, poligon_atap::text AS poligon_atap, kode_tarif, tarif_per_kwh, tagihan_bulanan_rp, dibuat_pada, diperbarui_pada";

    private const string KolomRiwayat =
        "id, instalasi_id, dibuat_pada, psh_rata_harian, produksi_tahunan_kwh, hemat_tahunan_rp, co2_tahunan_kg, " +
        "setara_pohon, produksi_7_hari_kwh, hemat_7_hari_rp";

    private readonly NpgsqlDataSource _sumber;
    private readonly Func<Guid?> _penggunaId;

    public RepositoriPostgres(string connectionString, Func<Guid?> penggunaId)
    {
        _sumber = NpgsqlDataSource.Create(connectionString);
        _penggunaId = penggunaId;
    }

    public override string Nama => "PostgreSQL";

    /// <summary>Uji koneksi (SELECT 1). Melempar <see cref="NpgsqlException"/> bila database tidak terjangkau.</summary>
    public async Task UjiKoneksiAsync(CancellationToken ct = default)
    {
        await using var cmd = _sumber.CreateCommand("SELECT 1");
        await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    public override async Task<List<Instalasi>> DaftarAsync(CancellationToken ct = default)
    {
        await using var cmd = _sumber.CreateCommand(
            $"SELECT {KolomInstalasi} FROM public.instalasi WHERE pengguna_id = $1 ORDER BY diperbarui_pada DESC");
        cmd.Parameters.AddWithValue(Pemilik());
        var hasil = new List<Instalasi>();
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) hasil.Add(BacaInstalasi(r));
        return hasil;
    }

    protected override async Task<Instalasi> SimpanInternalAsync(Instalasi i, CancellationToken ct)
    {
        var baru = i.Id is null;
        // $1..$13 = kolom data; $14 = id (update) / pengguna_id (insert); $15 = pengguna_id (update)
        var sql = baru
            ? $"""
               INSERT INTO public.instalasi
                 (nama, alamat, latitude, longitude, kapasitas_kwp, jumlah_panel, daya_panel_wp, efisiensi_persen,
                  luas_atap_m2, poligon_atap, kode_tarif, tarif_per_kwh, tagihan_bulanan_rp, pengguna_id)
               VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)
               RETURNING {KolomInstalasi}
               """
            : $"""
               UPDATE public.instalasi SET
                 nama=$1, alamat=$2, latitude=$3, longitude=$4, kapasitas_kwp=$5, jumlah_panel=$6, daya_panel_wp=$7,
                 efisiensi_persen=$8, luas_atap_m2=$9, poligon_atap=$10, kode_tarif=$11, tarif_per_kwh=$12,
                 tagihan_bulanan_rp=$13, diperbarui_pada=now()
               WHERE id=$14 AND pengguna_id=$15
               RETURNING {KolomInstalasi}
               """;

        await using var cmd = _sumber.CreateCommand(sql);
        var p = cmd.Parameters;
        p.AddWithValue(i.Nama);
        p.AddWithValue(i.Alamat);
        p.AddWithValue(i.Latitude);
        p.AddWithValue(i.Longitude);
        p.AddWithValue(i.KapasitasKwp);
        p.AddWithValue(i.JumlahPanel);
        p.AddWithValue(i.DayaPanelWp);
        p.AddWithValue(i.EfisiensiPersen);
        p.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Double, Value = (object?)i.LuasAtapM2 ?? DBNull.Value });
        p.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Jsonb,
            Value = i.PoligonAtap is null ? DBNull.Value : JsonSerializer.Serialize(i.PoligonAtap, Json.SnakeCase),
        });
        p.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)i.KodeTarif ?? DBNull.Value });
        p.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Numeric, Value = (decimal)i.TarifPerKwh });
        p.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Numeric, Value = (decimal)i.TagihanBulananRp });
        if (baru)
        {
            p.AddWithValue(Pemilik());
        }
        else
        {
            p.AddWithValue(i.Id!.Value);
            p.AddWithValue(Pemilik());
        }

        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await r.ReadAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Instalasi tidak ditemukan atau bukan milik Anda.");
        return BacaInstalasi(r);
    }

    public override async Task HapusAsync(long id, CancellationToken ct = default)
    {
        await using var cmd = _sumber.CreateCommand("DELETE FROM public.instalasi WHERE id = $1 AND pengguna_id = $2");
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(Pemilik());
        if (await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 0)
            throw new InvalidOperationException("Instalasi tidak ditemukan atau bukan milik Anda.");
    }

    public override async Task<List<RiwayatEstimasi>> RiwayatAsync(long instalasiId, CancellationToken ct = default)
    {
        await using var cmd = _sumber.CreateCommand(
            $"SELECT {KolomRiwayat} FROM public.riwayat_estimasi WHERE instalasi_id = $1 AND pengguna_id = $2 " +
            "ORDER BY dibuat_pada DESC LIMIT 60");
        cmd.Parameters.AddWithValue(instalasiId);
        cmd.Parameters.AddWithValue(Pemilik());
        var hasil = new List<RiwayatEstimasi>();
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) hasil.Add(BacaRiwayat(r));
        return hasil;
    }

    protected override async Task<RiwayatEstimasi> TambahRiwayatInternalAsync(RiwayatEstimasi x, CancellationToken ct)
    {
        // Hanya boleh menambah riwayat ke instalasi milik sendiri (setara dengan policy RLS di Supabase).
        await using var cmd = _sumber.CreateCommand(
            $"""
             INSERT INTO public.riwayat_estimasi
               (instalasi_id, pengguna_id, psh_rata_harian, produksi_tahunan_kwh, hemat_tahunan_rp, co2_tahunan_kg,
                setara_pohon, produksi_7_hari_kwh, hemat_7_hari_rp)
             SELECT i.id, i.pengguna_id, $2,$3,$4,$5,$6,$7,$8
             FROM public.instalasi i WHERE i.id = $1 AND i.pengguna_id = $9
             RETURNING {KolomRiwayat}
             """);
        var p = cmd.Parameters;
        p.AddWithValue(x.InstalasiId);
        p.AddWithValue(Math.Round(x.PshRataHarian, 3));
        p.AddWithValue(Math.Round(x.ProduksiTahunanKwh, 1));
        p.AddWithValue(Math.Round(x.HematTahunanRp));
        p.AddWithValue(Math.Round(x.Co2TahunanKg, 1));
        p.AddWithValue(Math.Round(x.SetaraPohon, 2));
        p.AddWithValue(Math.Round(x.Produksi7HariKwh, 2));
        p.AddWithValue(Math.Round(x.Hemat7HariRp));
        p.AddWithValue(Pemilik());

        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await r.ReadAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Instalasi tidak ditemukan atau bukan milik Anda.");
        return BacaRiwayat(r);
    }

    public void Dispose() => _sumber.Dispose();

    // ---------------------------------- helper privat ----------------------------------

    private Guid Pemilik() => _penggunaId()
        ?? throw new InvalidOperationException("Masuk ke akun terlebih dahulu agar data dapat dikaitkan dengan pemilik.");

    private static Instalasi BacaInstalasi(NpgsqlDataReader r)
    {
        var poligon = r.IsDBNull(r.GetOrdinal("poligon_atap")) ? null
            : JsonSerializer.Deserialize<List<TitikGeo>>(r.GetString(r.GetOrdinal("poligon_atap")), Json.SnakeCase);
        return new Instalasi
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Nama = r.GetString(r.GetOrdinal("nama")),
            Alamat = r.GetString(r.GetOrdinal("alamat")),
            Latitude = r.GetDouble(r.GetOrdinal("latitude")),
            Longitude = r.GetDouble(r.GetOrdinal("longitude")),
            KapasitasKwp = r.GetDouble(r.GetOrdinal("kapasitas_kwp")),
            JumlahPanel = r.GetInt32(r.GetOrdinal("jumlah_panel")),
            DayaPanelWp = r.GetDouble(r.GetOrdinal("daya_panel_wp")),
            EfisiensiPersen = r.GetDouble(r.GetOrdinal("efisiensi_persen")),
            LuasAtapM2 = r.IsDBNull(r.GetOrdinal("luas_atap_m2")) ? null : r.GetDouble(r.GetOrdinal("luas_atap_m2")),
            PoligonAtap = poligon,
            KodeTarif = r.IsDBNull(r.GetOrdinal("kode_tarif")) ? null : r.GetString(r.GetOrdinal("kode_tarif")),
            TarifPerKwh = (double)r.GetDecimal(r.GetOrdinal("tarif_per_kwh")),
            TagihanBulananRp = (double)r.GetDecimal(r.GetOrdinal("tagihan_bulanan_rp")),
            DibuatPada = r.GetFieldValue<DateTime>(r.GetOrdinal("dibuat_pada")).ToLocalTime(),
            DiperbaruiPada = r.GetFieldValue<DateTime>(r.GetOrdinal("diperbarui_pada")).ToLocalTime(),
        };
    }

    private static RiwayatEstimasi BacaRiwayat(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt64(r.GetOrdinal("id")),
        InstalasiId = r.GetInt64(r.GetOrdinal("instalasi_id")),
        DibuatPada = r.GetFieldValue<DateTime>(r.GetOrdinal("dibuat_pada")).ToLocalTime(),
        PshRataHarian = r.GetDouble(r.GetOrdinal("psh_rata_harian")),
        ProduksiTahunanKwh = r.GetDouble(r.GetOrdinal("produksi_tahunan_kwh")),
        HematTahunanRp = r.GetDouble(r.GetOrdinal("hemat_tahunan_rp")),
        Co2TahunanKg = r.GetDouble(r.GetOrdinal("co2_tahunan_kg")),
        SetaraPohon = r.GetDouble(r.GetOrdinal("setara_pohon")),
        Produksi7HariKwh = r.GetDouble(r.GetOrdinal("produksi_7_hari_kwh")),
        Hemat7HariRp = r.GetDouble(r.GetOrdinal("hemat_7_hari_rp")),
    };
}
