namespace SunSaver.App.Core;

/// <summary>
/// Lokasi data aplikasi (cache cuaca, cache tile peta, data mode lokal, sesi).
/// Default: folder <c>data</c> di samping .exe (portabel, ikut drive tempat aplikasi berada),
/// bisa diganti lewat env <c>SUNSAVER_DATA_DIR</c>. Jatuh ke %LOCALAPPDATA% bila tak bisa ditulisi.
/// </summary>
public static class FolderData
{
    public static string Akar { get; } = Tentukan();

    public static string Cache => Sub("cache");
    public static string Tile => Sub(Path.Combine("cache", "tiles"));

    public static string Sub(string relatif)
    {
        var p = Path.Combine(Akar, relatif);
        Directory.CreateDirectory(p);
        return p;
    }

    public static string Berkas(string nama) => Path.Combine(Akar, nama);

    private static string Tentukan()
    {
        var kandidat = new[]
        {
            Environment.GetEnvironmentVariable("SUNSAVER_DATA_DIR"),
            Path.Combine(AppContext.BaseDirectory, "data"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SunSaver"),
        };
        foreach (var k in kandidat)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            try
            {
                Directory.CreateDirectory(k);
                var uji = Path.Combine(k, ".tulis");
                File.WriteAllText(uji, "");
                File.Delete(uji);
                return k;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // coba kandidat berikutnya
            }
        }
        return Path.GetTempPath();
    }

    /// <summary>Tulis berkas secara atomik (tulis ke .tmp lalu ganti) agar tidak korup saat crash.</summary>
    public static void TulisAtomik(string path, string isi)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, isi);
        File.Move(tmp, path, overwrite: true);
    }

    public static void TulisAtomik(string path, byte[] isi)
    {
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, isi);
        File.Move(tmp, path, overwrite: true);
    }
}
