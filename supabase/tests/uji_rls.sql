-- Uji Row Level Security: dua pengguna, masing-masing hanya boleh melihat/mengubah datanya sendiri.
-- Dijalankan setelah stub_auth_lokal.sql + migrasi. Gagal → RAISE EXCEPTION (psql -v ON_ERROR_STOP=1).
\set ON_ERROR_STOP 1

insert into auth.users (id, email, raw_user_meta_data) values
    ('11111111-1111-1111-1111-111111111111', 'ani@contoh.id',  '{"nama":"Ani"}'),
    ('22222222-2222-2222-2222-222222222222', 'budi@contoh.id', '{}');

do $$ begin
    assert (select nama from public.profil where id = '11111111-1111-1111-1111-111111111111') = 'Ani',
        'trigger profil: nama dari metadata';
    assert (select nama from public.profil where id = '22222222-2222-2222-2222-222222222222') = 'budi',
        'trigger profil: fallback dari email';
end $$;

-- ---------- sebagai Ani
set role authenticated;
select set_config('request.jwt.claims', '{"sub":"11111111-1111-1111-1111-111111111111","role":"authenticated"}', false);

insert into public.instalasi (nama, latitude, longitude, kapasitas_kwp, jumlah_panel, kode_tarif, tarif_per_kwh, tagihan_bulanan_rp, poligon_atap)
values ('Rumah Ani', -7.7956, 110.3695, 3.2, 8, 'R1-1300', 1444.70, 500000, '[{"lat":-7.79,"lon":110.36}]');

insert into public.riwayat_estimasi (instalasi_id, psh_rata_harian, produksi_tahunan_kwh, hemat_tahunan_rp, co2_tahunan_kg, setara_pohon)
select id, 4.9, 4600, 6000000, 3650, 167 from public.instalasi where nama = 'Rumah Ani';

do $$ begin
    assert (select count(*) from public.instalasi) = 1, 'Ani melihat 1 instalasi';
    assert (select pengguna_id from public.instalasi limit 1) = '11111111-1111-1111-1111-111111111111', 'pengguna_id default auth.uid()';
    assert (select count(*) from public.tarif_listrik) = 7, 'tarif terbaca';
    assert (select jumlah_instalasi from public.ringkasan_dampak_saya()) = 1, 'RPC ringkasan';
end $$;

-- Ani tidak boleh memindahkan instalasi ke pengguna lain
do $$ begin
    begin
        update public.instalasi set pengguna_id = '22222222-2222-2222-2222-222222222222';
        raise exception 'GAGAL: pemilik bisa dipindah';
    exception when insufficient_privilege then null;
    end;
end $$;

-- ---------- sebagai Budi
select set_config('request.jwt.claims', '{"sub":"22222222-2222-2222-2222-222222222222","role":"authenticated"}', false);

do $$
declare n int;
begin
    assert (select count(*) from public.instalasi) = 0, 'Budi tidak melihat instalasi Ani';
    assert (select count(*) from public.riwayat_estimasi) = 0, 'Budi tidak melihat riwayat Ani';

    update public.instalasi set nama = 'diretas';
    get diagnostics n = row_count;
    assert n = 0, 'Budi tidak bisa mengubah instalasi Ani';

    delete from public.instalasi;
    get diagnostics n = row_count;
    assert n = 0, 'Budi tidak bisa menghapus instalasi Ani';

    -- menyisipkan riwayat ke instalasi milik Ani harus ditolak RLS
    begin
        insert into public.riwayat_estimasi (instalasi_id, psh_rata_harian, produksi_tahunan_kwh, hemat_tahunan_rp, co2_tahunan_kg, setara_pohon)
        select 1, 1, 1, 1, 1, 1;
        raise exception 'GAGAL: riwayat ke instalasi orang lain diterima';
    exception when insufficient_privilege then null;
    end;

    -- menyisipkan instalasi atas nama Ani harus ditolak RLS
    begin
        insert into public.instalasi (pengguna_id, nama, latitude, longitude, kapasitas_kwp, jumlah_panel, tarif_per_kwh)
        values ('11111111-1111-1111-1111-111111111111', 'palsu', 0, 0, 1, 1, 1000);
        raise exception 'GAGAL: insert atas nama orang lain diterima';
    exception when insufficient_privilege then null;
    end;
end $$;

-- ---------- sebagai anon (tanpa login)
reset role;
set role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', false);
do $$ begin
    assert (select count(*) from public.tarif_listrik) = 7, 'anon boleh membaca tarif';
    begin
        perform count(*) from public.instalasi;
        raise exception 'GAGAL: anon bisa membaca instalasi';
    exception when insufficient_privilege then null;
    end;
end $$;

-- ---------- validasi CHECK constraint
reset role;
set role authenticated;
select set_config('request.jwt.claims', '{"sub":"11111111-1111-1111-1111-111111111111","role":"authenticated"}', false);
do $$ begin
    begin
        insert into public.instalasi (nama, latitude, longitude, kapasitas_kwp, jumlah_panel, tarif_per_kwh)
        values ('salah', 200, 0, 1, 1, 1000);
        raise exception 'GAGAL: latitude 200 diterima';
    exception when check_violation then null;
    end;
end $$;

reset role;
select 'SEMUA UJI RLS LULUS' as hasil;
