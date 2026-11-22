-- =====================================================================================
-- SunSaver · skema database Supabase (PostgreSQL)
--
-- Jalankan sekali di Supabase Dashboard → SQL Editor, atau dengan Supabase CLI:
--     supabase link --project-ref <ref>  &&  supabase db push
--
-- Aplikasi desktop memakai publishable/anon key + JWT pengguna (Supabase Auth).
-- Seluruh tabel memakai Row Level Security: setiap pengguna hanya bisa membaca/mengubah
-- datanya sendiri; tabel referensi tarif bisa dibaca publik.
-- =====================================================================================

-- ------------------------------------------------------------------ profil pengguna
create table if not exists public.profil (
    id          uuid primary key references auth.users (id) on delete cascade,
    nama        text not null default '' check (char_length(nama) <= 80),
    dibuat_pada timestamptz not null default now()
);
comment on table public.profil is 'Profil publik pengguna SunSaver (1:1 dengan auth.users).';

-- Buat profil otomatis saat akun didaftarkan lewat Supabase Auth (nama dari user_metadata).
create or replace function public.buat_profil_pengguna_baru()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    insert into public.profil (id, nama)
    values (new.id, left(coalesce(new.raw_user_meta_data ->> 'nama', split_part(new.email, '@', 1), ''), 80))
    on conflict (id) do nothing;
    return new;
end;
$$;

drop trigger if exists saat_pengguna_dibuat on auth.users;
create trigger saat_pengguna_dibuat
    after insert on auth.users
    for each row execute function public.buat_profil_pengguna_baru();

-- ------------------------------------------------------------------ tarif listrik (referensi)
create table if not exists public.tarif_listrik (
    kode          text primary key check (char_length(kode) <= 20),
    nama          text not null,
    daya_va       integer not null check (daya_va > 0),
    tarif_per_kwh numeric(10, 2) not null check (tarif_per_kwh > 0),
    subsidi       boolean not null default false,
    urutan        smallint not null default 0
);
comment on table public.tarif_listrik is 'Golongan tarif PLN rumah tangga (Rp/kWh).';

insert into public.tarif_listrik (kode, nama, daya_va, tarif_per_kwh, subsidi, urutan) values
    ('R1-450S', 'R-1 / 450 VA (subsidi)', 450,  415.00,  true,  1),
    ('R1-900S', 'R-1 / 900 VA (subsidi)', 900,  605.00,  true,  2),
    ('R1-900',  'R-1 / 900 VA',           900,  1352.00, false, 3),
    ('R1-1300', 'R-1 / 1.300 VA',         1300, 1444.70, false, 4),
    ('R1-2200', 'R-1 / 2.200 VA',         2200, 1444.70, false, 5),
    ('R2',      'R-2 / 3.500–5.500 VA',   5500, 1699.53, false, 6),
    ('R3',      'R-3 / ≥6.600 VA',        6600, 1699.53, false, 7)
on conflict (kode) do update
    set nama = excluded.nama, daya_va = excluded.daya_va, tarif_per_kwh = excluded.tarif_per_kwh,
        subsidi = excluded.subsidi, urutan = excluded.urutan;

-- ------------------------------------------------------------------ instalasi
create table if not exists public.instalasi (
    id                 bigint generated always as identity primary key,
    pengguna_id        uuid not null default auth.uid() references auth.users (id) on delete cascade,
    nama               text not null check (char_length(nama) between 1 and 80),
    alamat             text not null default '' check (char_length(alamat) <= 300),
    latitude           double precision not null check (latitude between -90 and 90),
    longitude          double precision not null check (longitude between -180 and 180),
    kapasitas_kwp      double precision not null check (kapasitas_kwp > 0 and kapasitas_kwp <= 100),
    jumlah_panel       integer not null check (jumlah_panel between 1 and 300),
    daya_panel_wp      double precision not null default 400 check (daya_panel_wp between 100 and 1000),
    efisiensi_persen   double precision not null default 20 check (efisiensi_persen between 5 and 30),
    luas_atap_m2       double precision check (luas_atap_m2 is null or luas_atap_m2 between 1 and 5000),
    poligon_atap       jsonb check (poligon_atap is null or jsonb_typeof(poligon_atap) = 'array'),
    kode_tarif         text references public.tarif_listrik (kode) on delete set null,
    tarif_per_kwh      numeric(10, 2) not null check (tarif_per_kwh between 100 and 10000),
    tagihan_bulanan_rp numeric(14, 2) not null default 0 check (tagihan_bulanan_rp between 0 and 100000000),
    dibuat_pada        timestamptz not null default now(),
    diperbarui_pada    timestamptz not null default now()
);
comment on table public.instalasi is 'Instalasi panel surya milik pengguna (halaman "Instalasi Saya").';
create index if not exists instalasi_pengguna_idx on public.instalasi (pengguna_id, diperbarui_pada desc);

-- ------------------------------------------------------------------ riwayat estimasi
create table if not exists public.riwayat_estimasi (
    id                   bigint generated always as identity primary key,
    instalasi_id         bigint not null references public.instalasi (id) on delete cascade,
    pengguna_id          uuid not null default auth.uid() references auth.users (id) on delete cascade,
    dibuat_pada          timestamptz not null default now(),
    psh_rata_harian      double precision not null check (psh_rata_harian >= 0),
    produksi_tahunan_kwh double precision not null check (produksi_tahunan_kwh >= 0),
    hemat_tahunan_rp     double precision not null check (hemat_tahunan_rp >= 0),
    co2_tahunan_kg       double precision not null check (co2_tahunan_kg >= 0),
    setara_pohon         double precision not null check (setara_pohon >= 0),
    produksi_7_hari_kwh  double precision not null default 0 check (produksi_7_hari_kwh >= 0),
    hemat_7_hari_rp      double precision not null default 0 check (hemat_7_hari_rp >= 0)
);
comment on table public.riwayat_estimasi is 'Snapshot hasil estimasi per instalasi untuk melihat tren dari waktu ke waktu.';
create index if not exists riwayat_instalasi_idx on public.riwayat_estimasi (instalasi_id, dibuat_pada desc);
create index if not exists riwayat_pengguna_idx on public.riwayat_estimasi (pengguna_id);

-- ------------------------------------------------------------------ trigger
create or replace function public.atur_diperbarui_pada()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
    new.diperbarui_pada := now();
    return new;
end;
$$;

drop trigger if exists instalasi_diperbarui on public.instalasi;
create trigger instalasi_diperbarui
    before update on public.instalasi
    for each row execute function public.atur_diperbarui_pada();

-- Pemilik baris tidak boleh dipindah ke pengguna lain lewat UPDATE.
create or replace function public.kunci_pemilik()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
    if new.pengguna_id is distinct from old.pengguna_id then
        raise exception 'pengguna_id tidak boleh diubah' using errcode = '42501';
    end if;
    return new;
end;
$$;

drop trigger if exists instalasi_kunci_pemilik on public.instalasi;
create trigger instalasi_kunci_pemilik
    before update on public.instalasi
    for each row execute function public.kunci_pemilik();

-- ------------------------------------------------------------------ ringkasan dampak (RPC)
-- Dipanggil lewat POST /rest/v1/rpc/ringkasan_dampak_saya. security invoker → tetap tunduk RLS.
create or replace function public.ringkasan_dampak_saya()
returns table (jumlah_instalasi bigint, total_kwp double precision, total_co2_tahunan_kg double precision, total_setara_pohon double precision)
language sql
stable
security invoker
set search_path = ''
as $$
    -- lateral join menghasilkan ≤ 1 baris riwayat per instalasi, jadi agregat tidak terduplikasi
    select count(i.id),
           coalesce(sum(i.kapasitas_kwp), 0),
           coalesce(sum(r.co2_tahunan_kg), 0),
           coalesce(sum(r.setara_pohon), 0)
    from public.instalasi i
    left join lateral (
        select co2_tahunan_kg, setara_pohon
        from public.riwayat_estimasi re
        where re.instalasi_id = i.id
        order by re.dibuat_pada desc
        limit 1
    ) r on true
    where i.pengguna_id = (select auth.uid());
$$;

-- ------------------------------------------------------------------ Row Level Security
alter table public.profil           enable row level security;
alter table public.tarif_listrik    enable row level security;
alter table public.instalasi        enable row level security;
alter table public.riwayat_estimasi enable row level security;

drop policy if exists "profil: baca milik sendiri" on public.profil;
create policy "profil: baca milik sendiri" on public.profil
    for select to authenticated using (id = (select auth.uid()));

drop policy if exists "profil: ubah milik sendiri" on public.profil;
create policy "profil: ubah milik sendiri" on public.profil
    for update to authenticated using (id = (select auth.uid())) with check (id = (select auth.uid()));

drop policy if exists "tarif: baca publik" on public.tarif_listrik;
create policy "tarif: baca publik" on public.tarif_listrik
    for select to anon, authenticated using (true);

drop policy if exists "instalasi: baca milik sendiri" on public.instalasi;
create policy "instalasi: baca milik sendiri" on public.instalasi
    for select to authenticated using (pengguna_id = (select auth.uid()));

drop policy if exists "instalasi: tambah milik sendiri" on public.instalasi;
create policy "instalasi: tambah milik sendiri" on public.instalasi
    for insert to authenticated with check (pengguna_id = (select auth.uid()));

drop policy if exists "instalasi: ubah milik sendiri" on public.instalasi;
create policy "instalasi: ubah milik sendiri" on public.instalasi
    for update to authenticated
    using (pengguna_id = (select auth.uid()))
    with check (pengguna_id = (select auth.uid()));

drop policy if exists "instalasi: hapus milik sendiri" on public.instalasi;
create policy "instalasi: hapus milik sendiri" on public.instalasi
    for delete to authenticated using (pengguna_id = (select auth.uid()));

drop policy if exists "riwayat: baca milik sendiri" on public.riwayat_estimasi;
create policy "riwayat: baca milik sendiri" on public.riwayat_estimasi
    for select to authenticated using (pengguna_id = (select auth.uid()));

-- Riwayat hanya boleh ditambahkan ke instalasi milik sendiri.
drop policy if exists "riwayat: tambah untuk instalasi sendiri" on public.riwayat_estimasi;
create policy "riwayat: tambah untuk instalasi sendiri" on public.riwayat_estimasi
    for insert to authenticated
    with check (
        pengguna_id = (select auth.uid())
        and exists (select 1 from public.instalasi i where i.id = instalasi_id and i.pengguna_id = (select auth.uid()))
    );

drop policy if exists "riwayat: hapus milik sendiri" on public.riwayat_estimasi;
create policy "riwayat: hapus milik sendiri" on public.riwayat_estimasi
    for delete to authenticated using (pengguna_id = (select auth.uid()));

-- ------------------------------------------------------------------ hak akses Data API
-- Eksplisit agar tetap benar walau default privileges project diubah.
revoke all on public.profil, public.tarif_listrik, public.instalasi, public.riwayat_estimasi from anon;
grant select on public.tarif_listrik to anon, authenticated;
grant select, update (nama) on public.profil to authenticated;
grant select, insert, update, delete on public.instalasi to authenticated;
grant select, insert, delete on public.riwayat_estimasi to authenticated;
grant execute on function public.ringkasan_dampak_saya() to authenticated;
revoke execute on function public.ringkasan_dampak_saya() from anon, public;
revoke execute on function public.buat_profil_pengguna_baru() from anon, authenticated, public;
