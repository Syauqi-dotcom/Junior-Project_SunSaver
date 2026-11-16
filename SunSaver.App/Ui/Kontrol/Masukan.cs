using System.ComponentModel;

namespace SunSaver.App.Ui.Kontrol;

/// <summary>Slider bergaya Material (seperti slider tagihan listrik di Project Sunroof).</summary>
public class Penggeser : Control
{
    private int _nilai, _min, _maks = 100, _langkah = 1;
    private bool _seret, _hover;

    public Penggeser()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        TabStop = true;
        Height = Tema.S(32);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.Slider;
    }

    public event EventHandler? NilaiBerubah;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get => _min; set { _min = value; Nilai = _nilai; Invalidate(); } }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maksimum { get => _maks; set { _maks = Math.Max(value, _min + 1); Nilai = _nilai; Invalidate(); } }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Langkah { get => _langkah; set => _langkah = Math.Max(1, value); }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Nilai
    {
        get => _nilai;
        set
        {
            var v = Math.Clamp(_min + (int)Math.Round((value - _min) / (double)_langkah) * _langkah, _min, _maks);
            if (v == _nilai) return;
            _nilai = v;
            AccessibleDescription = v.ToString(Tema.Id);
            Invalidate();
            NilaiBerubah?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Atur nilai tanpa memicu event (saat mengisi form dari data).</summary>
    public void AturDiam(int nilai)
    {
        _nilai = Math.Clamp(nilai, _min, _maks);
        Invalidate();
    }

    private float Kiri => Tema.S(10f);
    private float Kanan => Width - Tema.S(10f);
    private float XDari(int v) => Kiri + (Kanan - Kiri) * (v - _min) / (float)(_maks - _min);
    private int NilaiDari(int x) => _min + (int)Math.Round((x - Kiri) / (Kanan - Kiri) * (_maks - _min));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        _seret = true;
        Nilai = NilaiDari(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_seret) Nilai = NilaiDari(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _seret = false; }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override bool IsInputKey(Keys keyData)
        => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
           || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var besar = Math.Max(_langkah, (_maks - _min) / 10);
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Down: Nilai -= _langkah; break;
            case Keys.Right or Keys.Up: Nilai += _langkah; break;
            case Keys.PageDown: Nilai -= besar; break;
            case Keys.PageUp: Nilai += besar; break;
            case Keys.Home: Nilai = _min; break;
            case Keys.End: Nilai = _maks; break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Color.White);
        Tema.Halus(g);
        var cy = Height / 2f;
        var tebal = Tema.S(4f);
        using (var b = new SolidBrush(Tema.Hex("#C6DAFC")))
        using (var path = Tema.Bulat(new RectangleF(Kiri, cy - tebal / 2, Kanan - Kiri, tebal), tebal / 2))
            g.FillPath(b, path);
        var x = XDari(_nilai);
        using (var b = new SolidBrush(Enabled ? Tema.Biru : Tema.TeksPudar))
        using (var path = Tema.Bulat(new RectangleF(Kiri, cy - tebal / 2, Math.Max(tebal, x - Kiri), tebal), tebal / 2))
            g.FillPath(b, path);

        if (Focused || _seret || _hover)
        {
            var h = Tema.S(_seret ? 30f : 26f);
            using var halo = new SolidBrush(Tema.Alfa(Tema.Biru, Focused ? 50 : 30));
            g.FillEllipse(halo, x - h / 2, cy - h / 2, h, h);
        }
        var d = Tema.S(16f);
        using (var b = new SolidBrush(Enabled ? Tema.Biru : Tema.TeksPudar))
            g.FillEllipse(b, x - d / 2, cy - d / 2, d, d);
    }
}

/// <summary>Pabrik kontrol isian dengan gaya seragam.</summary>
public static class Isian
{
    public static NumericUpDown Angka(decimal min, decimal maks, int desimal, decimal kenaikan, decimal nilai) => new()
    {
        Minimum = min,
        Maximum = maks,
        DecimalPlaces = desimal,
        Increment = kenaikan,
        Value = Math.Clamp(nilai, min, maks),
        ThousandsSeparator = true,
        Font = Tema.Font(10.5f),
        BorderStyle = BorderStyle.FixedSingle,
        TextAlign = HorizontalAlignment.Right,
        Height = Tema.S(30),
        Margin = new Padding(0, Tema.S(4), 0, 0),
    };

    public static TextBox Teks(string placeholder = "", int maks = 300) => new()
    {
        PlaceholderText = placeholder,
        MaxLength = maks,
        Font = Tema.Font(10.5f),
        BorderStyle = BorderStyle.FixedSingle,
        AutoSize = true,
        Margin = new Padding(0, Tema.S(4), 0, 0),
    };

    public static ComboBox Pilihan(IEnumerable<object> item)
    {
        var c = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Tema.Font(10.5f),
            FlatStyle = FlatStyle.System,
            Height = Tema.S(30),
            Margin = new Padding(0, Tema.S(4), 0, 0),
        };
        c.Items.AddRange(item.ToArray());
        return c;
    }

    /// <summary>Label + kontrol (+ satuan di kanan) + pesan galat di bawah.</summary>
    public static Tumpukan Bidang(string label, Control kontrol, string? satuan = null, Label? galat = null, string? bantuan = null)
    {
        var t = new Tumpukan { Margin = new Padding(0, 0, 0, Tema.S(14)) };
        var l = Kontrol.Teks.Buat(label, Tema.Font(9.5f, semibold: true), Tema.Teks);
        kontrol.AccessibleName ??= label;
        t.Controls.Add(l);
        if (satuan is null)
        {
            t.Controls.Add(kontrol);
        }
        else
        {
            var b = new Baris { Jarak = Tema.S(8), Margin = new Padding(0, Tema.S(4), 0, 0) };
            kontrol.Margin = Padding.Empty;
            b.Tambah(kontrol, 1).Tambah(Kontrol.Teks.Redup(satuan));
            t.Controls.Add(b);
        }
        if (bantuan is not null) t.Controls.Add(Kontrol.Teks.Bungkus(bantuan, Tema.Font(8.5f), Tema.TeksPudar));
        if (galat is not null) t.Controls.Add(galat);
        return t;
    }

    public static Label Galat() => new()
    {
        AutoSize = true,
        ForeColor = Tema.Merah,
        Font = Tema.Font(8.5f),
        Visible = false,
        Margin = new Padding(0, Tema.S(4), 0, 0),
    };

    /// <summary>Gaya DataGridView yang bersih (tanpa garis vertikal, header abu muda).</summary>
    public static DataGridView Tabel()
    {
        var t = new DataGridView
        {
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = Tema.S(36),
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            GridColor = Tema.GarisHalus,
            EnableHeadersVisualStyles = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            Font = Tema.Font(9.5f),
            StandardTab = true,
        };
        t.RowTemplate.Height = Tema.S(32);
        t.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Tema.LatarAbu,
            ForeColor = Tema.TeksRedup,
            Font = Tema.Font(9, semibold: true),
            SelectionBackColor = Tema.LatarAbu,
            SelectionForeColor = Tema.TeksRedup,
            Padding = new Padding(Tema.S(6), 0, Tema.S(6), 0),
        };
        t.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Tema.Teks,
            SelectionBackColor = Tema.BiruMuda,
            SelectionForeColor = Tema.Teks,
            Padding = new Padding(Tema.S(6), 0, Tema.S(6), 0),
        };
        return t;
    }

    public static DataGridViewTextBoxColumn Kolom(string judul, string properti, string? format = null, bool kanan = true, float bobot = 100)
        => new()
        {
            HeaderText = judul,
            DataPropertyName = properti,
            FillWeight = bobot,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Format = format ?? "",
                FormatProvider = Tema.Id,
                Alignment = kanan ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft,
            },
            HeaderCell = { Style = { Alignment = kanan ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft } },
        };
}
