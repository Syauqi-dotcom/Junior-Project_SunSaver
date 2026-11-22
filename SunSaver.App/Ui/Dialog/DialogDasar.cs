using SunSaver.App.Ui.Kontrol;

namespace SunSaver.App.Ui.Dialog;

/// <summary>Dialog modal bergaya kartu: judul, isi bertumpuk, baris tombol di bawah.</summary>
public abstract class DialogDasar : Form
{
    protected DialogDasar(string judul, int lebar)
    {
        Text = judul;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.White;
        Font = Tema.FontIsi;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;

        Isi = new Tumpukan { Padding = Tema.P(28, 24, 28, 8), Dock = DockStyle.Top, TinggiOtomatis = false };
        BarisTombol = new Baris { Padding = Tema.P(28, 12, 28, 20), Dock = DockStyle.Bottom, RataKanan = true, Jarak = Tema.S(10) };
        BarisTombol.Height = Tema.S(76);
        Controls.Add(Isi);
        Controls.Add(BarisTombol);
        ClientSize = new Size(Tema.S(lebar), Tema.S(400));
    }

    protected Tumpukan Isi { get; }
    protected Baris BarisTombol { get; }

    /// <summary>Hitung ulang tinggi dialog sesuai isi.</summary>
    protected void SesuaikanTinggi()
    {
        var tinggiIsi = Isi.TinggiPreferensi(ClientSize.Width);
        Isi.Height = tinggiIsi;
        ClientSize = new Size(ClientSize.Width, tinggiIsi + BarisTombol.Height);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        SesuaikanTinggi();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) Close();
    }
}
