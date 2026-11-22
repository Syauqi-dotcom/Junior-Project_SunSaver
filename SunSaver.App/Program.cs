using System.Globalization;
using SunSaver.App.Ui;

namespace SunSaver.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Format angka Indonesia di seluruh aplikasi: "42,8 kWh", "Rp 62.300".
        var id = new CultureInfo("id-ID");
        CultureInfo.DefaultThreadCurrentCulture = id;
        CultureInfo.DefaultThreadCurrentUICulture = id;
        Thread.CurrentThread.CurrentCulture = id;
        Thread.CurrentThread.CurrentUICulture = id;

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => TampilkanGalat(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) TampilkanGalat(ex); };

        using (var g = Graphics.FromHwnd(IntPtr.Zero))
            Tema.Inisialisasi(g.DpiX);

        Application.Run(new MainForm());
    }

    private static void TampilkanGalat(Exception e)
    {
        try
        {
            File.AppendAllText(Core.FolderData.Berkas("galat.log"), $"[{DateTime.Now:O}] {e}\n\n");
        }
        catch (IOException)
        {
            // abaikan
        }
        MessageBox.Show(KonteksAplikasi.PesanGalat(e), "SunSaver", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
