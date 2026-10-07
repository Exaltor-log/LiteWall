using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace LiteWall;

/// <summary>
/// Palet warna gelap, font, dan helper gaya. Semua font dibuat sekali lalu dipakai bersama
/// supaya tidak ada objek GDI baru per kartu atau per repaint.
/// </summary>
static class Theme
{
    public static readonly Color Bg = Color.FromArgb(17, 17, 21);
    public static readonly Color Surface = Color.FromArgb(26, 26, 32);
    public static readonly Color Input = Color.FromArgb(36, 36, 44);
    public static readonly Color InputHover = Color.FromArgb(44, 44, 54);
    public static readonly Color Border = Color.FromArgb(52, 52, 64);
    public static readonly Color Text = Color.FromArgb(236, 236, 241);
    public static readonly Color Muted = Color.FromArgb(150, 150, 166);
    public static readonly Color Accent = Color.FromArgb(108, 124, 255);
    public static readonly Color AccentHover = Color.FromArgb(128, 142, 255);
    public static readonly Color AccentPressed = Color.FromArgb(90, 104, 230);
    public static readonly Color Danger = Color.FromArgb(255, 120, 120);

    public static readonly Font Body = new("Segoe UI", 9.5f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font SmallBold = new("Segoe UI Semibold", 8.5f);
    public static readonly Font Heading = new("Segoe UI Semibold", 10.5f);
    public static readonly Font Title = new("Segoe UI Semibold", 13f);
    public static readonly Font Value = new("Segoe UI Semibold", 15f);
    public static readonly Font Icon = new("Segoe MDL2 Assets", 10f);

    /// <summary>Warna dasar jendela dan judul jendela gelap (Windows 10 1809 ke atas).</summary>
    public static void ApplyWindow(Form f)
    {
        f.BackColor = Bg;
        f.ForeColor = Text;
        f.Font = Body;
        f.HandleCreated += (_, _) => DarkTitleBar(f.Handle);
    }

    /// <summary>Scrollbar gelap untuk kontrol yang punya scrollbar bawaan Windows.</summary>
    public static void DarkScrollBars(Control c)
    {
        c.HandleCreated += (_, _) =>
        {
            try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { }
        };
    }

    static void DarkTitleBar(IntPtr hwnd)
    {
        try
        {
            int on = 1;
            // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE, 19 untuk build Windows 10 lama
            if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
        }
        catch
        {
            // Windows lama: judul jendela tetap terang
        }
    }

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Isi kotak membulat beserta garis tepi opsional, sudut luar diisi warna induk.</summary>
    public static void FillRound(Graphics g, Rectangle bounds, Color outside, Color fill, Color? border, float radius)
    {
        g.Clear(outside);
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(bounds.X + 0.5f, bounds.Y + 0.5f, bounds.Width - 1f, bounds.Height - 1f);
        using (var path = Round(r, radius))
        {
            using (var br = new SolidBrush(fill)) g.FillPath(br, path);
            if (border.HasValue)
                using (var pen = new Pen(border.Value)) g.DrawPath(pen, path);
        }
        g.SmoothingMode = old;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);
}
