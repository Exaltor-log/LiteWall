using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LiteWall;

// Kontrol ringan yang digambar sendiri untuk tema gelap. Semuanya hanya menggambar ulang
// saat ada perubahan (hover, fokus, nilai), tanpa timer atau animasi yang memakan CPU.

/// <summary>Tombol membulat. Primary memakai warna aksen.</summary>
sealed class ModernButton : Button
{
    bool primary, hover, down;

    public ModernButton(string text, bool primary = false, int height = 34)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        this.primary = primary;
        Text = text;
        Font = Theme.Body;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        int w = TextRenderer.MeasureText(text, Font).Width;
        Size = new Size(w + LogicalToDeviceUnits(height < 32 ? 20 : 28), LogicalToDeviceUnits(height));
        Margin = new Padding(LogicalToDeviceUnits(4), 0, LogicalToDeviceUnits(4), 0);
    }

    public bool Primary
    {
        get => primary;
        set { if (primary != value) { primary = value; Invalidate(); } }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Color fill = !Enabled ? Theme.Input
            : primary ? (down ? Theme.AccentPressed : hover ? Theme.AccentHover : Theme.Accent)
            : (down ? Theme.Border : hover ? Theme.InputHover : Theme.Input);
        Color? border = primary ? null : Theme.Border;
        if (Focused && ShowFocusCues) border = primary ? Theme.Text : Theme.Accent;

        Theme.FillRound(e.Graphics, ClientRectangle, Parent?.BackColor ?? Theme.Bg, fill, border, LogicalToDeviceUnits(6));
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
            !Enabled ? Theme.Muted : primary ? Color.White : Theme.Text, fill,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { down = true; Invalidate(); }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        down = false;
        Invalidate();
        base.OnMouseUp(e);
    }
}

/// <summary>Kotak teks membulat dengan ikon opsional (huruf dari Segoe MDL2 Assets).</summary>
sealed class InputBox : Panel
{
    public readonly TextBox Box = new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Input,
        ForeColor = Theme.Text,
        Font = Theme.Body
    };

    readonly string glyph;

    public InputBox(int width, string glyph = null)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        this.glyph = glyph;
        BackColor = Theme.Input;
        Size = new Size(width, LogicalToDeviceUnits(34));
        Margin = new Padding(LogicalToDeviceUnits(4), 0, LogicalToDeviceUnits(4), 0);
        Cursor = Cursors.IBeam;
        Controls.Add(Box);
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
    }

    int PadLeft => LogicalToDeviceUnits(glyph != null ? 34 : 11);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int padR = LogicalToDeviceUnits(11);
        Box.SetBounds(PadLeft, (Height - Box.Height) / 2, Math.Max(10, Width - PadLeft - padR), Box.Height);
    }

    protected override void OnClick(EventArgs e)
    {
        Box.Focus();
        base.OnClick(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Theme.FillRound(e.Graphics, ClientRectangle, Parent?.BackColor ?? Theme.Bg, Theme.Input,
            Box.Focused ? Theme.Accent : Theme.Border, LogicalToDeviceUnits(6));
        if (glyph != null)
            TextRenderer.DrawText(e.Graphics, glyph, Theme.Icon, new Rectangle(0, 0, PadLeft, Height), Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    protected override void OnPaint(PaintEventArgs e) { }
}

/// <summary>ComboBox gelap. Bagian tertutup digambar sendiri, daftar memakai owner draw.</summary>
sealed class DarkComboBox : ComboBox
{
    const int WM_PAINT = 0x000F;
    bool hover;

    public DarkComboBox(int width)
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        BackColor = Theme.Input;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        ItemHeight = LogicalToDeviceUnits(28);
        Width = width;
        Cursor = Cursors.Hand;
        Margin = new Padding(LogicalToDeviceUnits(4), 0, LogicalToDeviceUnits(4), 0);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
        bool sel = !edit && (e.State & DrawItemState.Selected) != 0;
        Color bg = sel ? Theme.Accent : Theme.Input;
        using (var br = new SolidBrush(bg)) e.Graphics.FillRectangle(br, e.Bounds);
        var r = new Rectangle(e.Bounds.X + LogicalToDeviceUnits(10), e.Bounds.Y,
            e.Bounds.Width - LogicalToDeviceUnits(14), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, r, sel ? Color.White : Theme.Text, bg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_PAINT)
        {
            var ps = new PAINTSTRUCT();
            IntPtr hdc = BeginPaint(Handle, ref ps);
            try
            {
                using var g = Graphics.FromHdc(hdc);
                PaintFace(g);
            }
            finally
            {
                EndPaint(Handle, ref ps);
            }
            return;
        }
        base.WndProc(ref m);
    }

    void PaintFace(Graphics target)
    {
        var r = ClientRectangle;
        if (r.Width <= 0 || r.Height <= 0) return;
        using var buf = BufferedGraphicsManager.Current.Allocate(target, r);
        var g = buf.Graphics;
        bool active = Focused || DroppedDown;
        Color fill = hover || DroppedDown ? Theme.InputHover : Theme.Input;
        Theme.FillRound(g, r, Parent?.BackColor ?? Theme.Bg, fill, active ? Theme.Accent : Theme.Border, LogicalToDeviceUnits(6));

        int arrowW = LogicalToDeviceUnits(30);
        string text = SelectedIndex >= 0 ? GetItemText(SelectedItem) : "";
        var tr = new Rectangle(LogicalToDeviceUnits(11), 0, r.Width - arrowW - LogicalToDeviceUnits(11), r.Height);
        TextRenderer.DrawText(g, text, Font, tr, Enabled ? Theme.Text : Theme.Muted, fill,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        float cx = r.Width - arrowW / 2f, cy = r.Height / 2f, s = LogicalToDeviceUnits(4);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Theme.Muted, Math.Max(1.5f, LogicalToDeviceUnits(2) * 0.75f));
        g.DrawLines(pen, new[] { new PointF(cx - s, cy - s / 2), new PointF(cx, cy + s / 2), new PointF(cx + s, cy - s / 2) });
        buf.Render(target);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnDropDown(EventArgs e) { Invalidate(); base.OnDropDown(e); }
    protected override void OnDropDownClosed(EventArgs e) { Invalidate(); base.OnDropDownClosed(e); }
    protected override void OnSelectedIndexChanged(EventArgs e) { Invalidate(); base.OnSelectedIndexChanged(e); }

    [StructLayout(LayoutKind.Sequential)]
    struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public int fErase;
        public Native.RECT rcPaint;
        public int fRestore;
        public int fIncUpdate;
        // rgbReserved[32], ditulis sebagai field biasa supaya struct tetap blittable
        public long r0, r1, r2, r3;
    }

    [DllImport("user32.dll")]
    static extern IntPtr BeginPaint(IntPtr hwnd, ref PAINTSTRUCT ps);

    [DllImport("user32.dll")]
    static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT ps);
}

/// <summary>Sakelar on/off. Turunan CheckBox supaya keyboard dan aksesibilitas tetap jalan.</summary>
sealed class ToggleSwitch : CheckBox
{
    const int SwitchW = 40, SwitchH = 22, Gap = 16;
    bool hover;

    public ToggleSwitch(string text, int width)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = false;
        Font = Theme.Body;
        Text = text;
        Cursor = Cursors.Hand;
        Width = width;
        Height = Math.Max(LogicalToDeviceUnits(34), TextSize().Height + LogicalToDeviceUnits(10));
    }

    Size TextSize() => TextRenderer.MeasureText(Text, Font,
        new Size(Width - LogicalToDeviceUnits(SwitchW + Gap), int.MaxValue),
        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        int sw = LogicalToDeviceUnits(SwitchW), sh = LogicalToDeviceUnits(SwitchH);
        var ts = TextSize();
        var tr = new Rectangle(0, (Height - ts.Height) / 2, Width - sw - LogicalToDeviceUnits(Gap), ts.Height);
        TextRenderer.DrawText(g, Text, Font, tr, Enabled ? Theme.Text : Theme.Muted, BackColor,
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        var track = new RectangleF(Width - sw - 1, (Height - sh) / 2f, sw, sh);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Theme.Round(track, sh / 2f))
        {
            Color fill = Checked ? (hover ? Theme.AccentHover : Theme.Accent) : (hover ? Theme.InputHover : Theme.Input);
            using (var br = new SolidBrush(fill)) g.FillPath(br, path);
            if (!Checked)
                using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, path);
            if (Focused && ShowFocusCues)
                using (var pen = new Pen(Theme.Text, 1.5f)) g.DrawPath(pen, path);
        }

        float k = sh - LogicalToDeviceUnits(8);
        float kx = Checked ? track.Right - k - LogicalToDeviceUnits(4) : track.X + LogicalToDeviceUnits(4);
        using (var br = new SolidBrush(Checked ? Color.White : Theme.Muted))
            g.FillEllipse(br, kx, track.Y + (sh - k) / 2f, k, k);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
}

/// <summary>Slider datar dengan dukungan mouse dan keyboard (panah, PageUp/PageDown, Home/End).</summary>
sealed class FlatSlider : Control
{
    int min, max = 100, val, largeChange = 6;
    bool dragging, hover;

    public event EventHandler ValueChanged;

    public FlatSlider(int width)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Size = new Size(width, LogicalToDeviceUnits(30));
        AccessibleRole = AccessibleRole.Slider;
    }

    public int Minimum { get => min; set { min = value; Value = val; Invalidate(); } }
    public int Maximum { get => max; set { max = value; Value = val; Invalidate(); } }
    public int LargeChange { get => largeChange; set => largeChange = Math.Max(1, value); }

    public int Value
    {
        get => val;
        set
        {
            int v = Math.Clamp(value, min, Math.Max(min, max));
            if (v == val) return;
            val = v;
            AccessibleDescription = v.ToString();
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    float Radius => LogicalToDeviceUnits(9);

    float XOf(int v)
    {
        float r = Radius;
        float t = max > min ? (v - min) / (float)(max - min) : 0;
        return r + 1 + t * (Width - 2 * r - 2);
    }

    void SetFromX(int x)
    {
        float r = Radius;
        float t = (x - r - 1) / Math.Max(1f, Width - 2 * r - 2);
        Value = min + (int)Math.Round(Math.Clamp(t, 0f, 1f) * (max - min));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float cy = Height / 2f, x = XOf(val), r = Radius;
        float th = LogicalToDeviceUnits(4);
        using (var pen = new Pen(Theme.Border, th) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(pen, r + 1, cy, Width - r - 1, cy);
        using (var pen = new Pen(Theme.Accent, th) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(pen, r + 1, cy, x, cy);

        if ((Focused && ShowFocusCues) || hover || dragging)
            using (var halo = new SolidBrush(Color.FromArgb(60, Theme.Accent)))
                g.FillEllipse(halo, x - r - 4, cy - r - 4, 2 * r + 8, 2 * r + 8);
        using (var br = new SolidBrush(Color.White)) g.FillEllipse(br, x - r, cy - r, 2 * r, 2 * r);
        using (var pen = new Pen(Theme.Accent, 2f)) g.DrawEllipse(pen, x - r + 1, cy - r + 1, 2 * r - 2, 2 * r - 2);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        dragging = true;
        SetFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging) SetFromX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragging = false;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left: case Keys.Down: Value--; break;
            case Keys.Right: case Keys.Up: Value++; break;
            case Keys.PageDown: Value -= largeChange; break;
            case Keys.PageUp: Value += largeChange; break;
            case Keys.Home: Value = min; break;
            case Keys.End: Value = max; break;
            default: return;
        }
        e.Handled = true;
    }
}

/// <summary>Kartu panel membulat untuk halaman pengaturan. Anak kontrol disusun dari atas ke bawah.</summary>
sealed class Section : Panel
{
    readonly int pad;
    int y;

    public Section(int width)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Width = width;
        pad = LogicalToDeviceUnits(18);
        y = pad;
        Height = pad * 2;
        Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(12));
    }

    public int Inner => Width - pad * 2;

    /// <summary>Tambah satu baris. Kontrol kanan (opsional) diratakan ke kanan dan ke tengah baris.</summary>
    public void Add(Control left, Control right = null, int gapTop = 0)
    {
        y += LogicalToDeviceUnits(gapTop);
        int h = Math.Max(left.Height, right?.Height ?? 0);
        left.Location = new Point(pad, y + (h - left.Height) / 2);
        Controls.Add(left);
        if (right != null)
        {
            right.Location = new Point(Width - pad - right.Width, y + (h - right.Height) / 2);
            Controls.Add(right);
        }
        y += h;
        Height = y + pad;
    }

    /// <summary>Tambah beberapa kontrol berjajar mendatar dalam satu baris.</summary>
    public void AddInline(int gapTop, params Control[] items)
    {
        y += LogicalToDeviceUnits(gapTop);
        int x = pad, h = items.Max(c => c.Height), gap = LogicalToDeviceUnits(8);
        foreach (var c in items)
        {
            c.Margin = Padding.Empty;
            c.Location = new Point(x, y + (h - c.Height) / 2);
            Controls.Add(c);
            x += c.Width + gap;
        }
        y += h;
        Height = y + pad;
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        Theme.FillRound(e.Graphics, ClientRectangle, Parent?.BackColor ?? Theme.Bg, Theme.Surface, Theme.Border,
            LogicalToDeviceUnits(10));

    protected override void OnPaint(PaintEventArgs e) { }
}

/// <summary>Logo bulat bergradasi plus nama aplikasi, senada dengan ikon tray.</summary>
sealed class LogoLabel : Control
{
    public LogoLabel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Text = "LiteWall";
        Font = Theme.Title;
        var t = TextRenderer.MeasureText(Text, Font);
        Size = new Size(t.Width + LogicalToDeviceUnits(30), Math.Max(t.Height, LogicalToDeviceUnits(24)));
        Margin = new Padding(0, 0, LogicalToDeviceUnits(12), 0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int d = LogicalToDeviceUnits(20);
        var c = new Rectangle(0, (Height - d) / 2, d, d);
        using (var br = new LinearGradientBrush(c, Color.FromArgb(80, 160, 255), Color.FromArgb(150, 80, 255), 45f))
            g.FillEllipse(br, c);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(d + LogicalToDeviceUnits(8), 0, Width, Height), Theme.Text, BackColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Bilah status bawah dengan garis progres tipis saat mengunduh.</summary>
sealed class StatusBar : Control
{
    int progress = -1;
    bool error;

    public StatusBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Surface;
        Font = Theme.Small;
        Height = LogicalToDeviceUnits(30);
    }

    /// <summary>0 sampai 100, atau -1 untuk menyembunyikan garis progres.</summary>
    public int Progress
    {
        get => progress;
        set { if (progress != value) { progress = value; Invalidate(); } }
    }

    public void SetStatus(string text, bool isError = false, int progressValue = -1)
    {
        error = isError;
        progress = progressValue;
        Text = text;
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, 0, Width, 0);
        if (progress >= 0)
            using (var br = new SolidBrush(Theme.Accent))
                g.FillRectangle(br, 0, 0, Width * Math.Clamp(progress, 0, 100) / 100, LogicalToDeviceUnits(2));
        int pad = LogicalToDeviceUnits(16);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(pad, 0, Width - pad * 2, Height),
            error ? Theme.Danger : Theme.Muted, BackColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

/// <summary>
/// Kartu thumbnail. Gambar disimpan sudah diperkecil seukuran kartu, jadi memori per kartu kecil
/// (sekitar 150 KB pada skala 100%) walau sumbernya poster video Full HD.
/// </summary>
sealed class ThumbCard : Control
{
    public WallItem Item { get; }
    readonly string caption;
    Bitmap thumb;
    bool failed, hover;

    public ThumbCard(WallItem item, Size size, Padding margin)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Item = item;
        caption = $"{item.Width}x{item.Height}";
        Size = size;
        Margin = margin;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = $"{item.Source} {caption}" + (item.IsVideo ? " video" : "");
    }

    public void SetThumb(Bitmap bmp)
    {
        thumb?.Dispose();
        thumb = bmp;
        failed = false;
        Invalidate();
    }

    public void SetFailed()
    {
        failed = true;
        Invalidate();
    }

    /// <summary>Decode lalu potong-tengah (cover) ke ukuran kartu. Aman dipanggil di thread latar.</summary>
    public static Bitmap Render(byte[] data, Size size)
    {
        int w = Math.Max(1, size.Width), h = Math.Max(1, size.Height);
        using var ms = new MemoryStream(data);
        using var src = Image.FromStream(ms, false, false);
        float scale = Math.Max((float)w / src.Width, (float)h / src.Height);
        float sw = w / scale, sh = h / scale;

        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        try
        {
            using var g = Graphics.FromImage(bmp);
            using var attr = new ImageAttributes();
            attr.SetWrapMode(WrapMode.TileFlipXY);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, new Rectangle(0, 0, w, h), (src.Width - sw) / 2f, (src.Height - sh) / 2f, sw, sh,
                GraphicsUnit.Pixel, attr);
            return bmp;
        }
        catch
        {
            bmp.Dispose();
            throw;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var r = ClientRectangle;
        int pad = LogicalToDeviceUnits(10);

        if (thumb != null)
        {
            if (thumb.Width == r.Width && thumb.Height == r.Height) g.DrawImageUnscaled(thumb, 0, 0);
            else
            {
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.DrawImage(thumb, r);
            }
        }
        else
        {
            g.Clear(Theme.Surface);
            TextRenderer.DrawText(g, failed ? "Gagal memuat" : "Memuat...", Theme.Small, r, Theme.Muted, Theme.Surface,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        // Pita bawah bergradasi supaya teks tetap terbaca di atas gambar terang.
        int bh = LogicalToDeviceUnits(32);
        var band = new Rectangle(0, r.Height - bh, r.Width, bh);
        using (var lg = new LinearGradientBrush(new Rectangle(band.X, band.Y - 1, band.Width, band.Height + 2),
                   Color.FromArgb(0, 0, 0, 0), Color.FromArgb(200, 0, 0, 0), 90f))
            g.FillRectangle(lg, band);
        var textRect = new Rectangle(pad, band.Y, r.Width - pad * 2, bh);
        TextRenderer.DrawText(g, caption, Theme.SmallBold, textRect, Color.White,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, Item.Source, Theme.Small, textRect, Color.FromArgb(200, 200, 210),
            TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Item.IsVideo)
        {
            const string badge = "VIDEO";
            var ts = TextRenderer.MeasureText(badge, Theme.SmallBold);
            var br = new Rectangle(pad - LogicalToDeviceUnits(2), pad - LogicalToDeviceUnits(2),
                ts.Width + LogicalToDeviceUnits(6), ts.Height + LogicalToDeviceUnits(2));
            using (var path = Theme.Round(br, br.Height / 2f))
            using (var fill = new SolidBrush(Color.FromArgb(220, Theme.Accent)))
                g.FillPath(fill, path);
            TextRenderer.DrawText(g, badge, Theme.SmallBold, br, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        // Sudut membulat: tutup keempat sudut dengan warna latar induk.
        float rad = LogicalToDeviceUnits(8);
        using (var corners = new GraphicsPath { FillMode = FillMode.Alternate })
        {
            corners.AddRectangle(new Rectangle(-1, -1, r.Width + 2, r.Height + 2));
            using (var round = Theme.Round(new RectangleF(0, 0, r.Width, r.Height), rad)) corners.AddPath(round, false);
            using var bg = new SolidBrush(Parent?.BackColor ?? Theme.Bg);
            g.FillPath(bg, corners);
        }

        if (hover || Focused)
        {
            float w = LogicalToDeviceUnits(2);
            using var path = Theme.Round(new RectangleF(w / 2, w / 2, r.Width - w, r.Height - w), rad - w / 2);
            using var pen = new Pen(Theme.Accent, w);
            g.DrawPath(pen, path);
        }
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            thumb?.Dispose();
            thumb = null;
        }
        base.Dispose(disposing);
    }
}
