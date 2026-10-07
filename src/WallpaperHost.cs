namespace LiteWall;

/// <summary>
/// Jendela tanpa bingkai yang ditempelkan di belakang ikon desktop (trik Progman/WorkerW).
/// mpv merender ke jendela ini.
/// </summary>
public sealed class WallpaperHost : Form
{
    IntPtr parent = IntPtr.Zero;

    public WallpaperHost()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(100, 100);
        BackColor = Color.Black;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080;   // WS_EX_TOOLWINDOW
            cp.ExStyle |= 0x08000000;   // WS_EX_NOACTIVATE
            return cp;
        }
    }

    public void Attach()
    {
        IntPtr progman = Native.FindWindow("Progman", null);
        Native.SendMessageTimeout(progman, 0x052C, UIntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

        IntPtr workerw = IntPtr.Zero;
        Native.EnumWindows((top, _) =>
        {
            if (Native.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                workerw = Native.FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            return true;
        }, IntPtr.Zero);

        // Windows 11 24H2: WorkerW menjadi anak Progman
        if (workerw == IntPtr.Zero)
            workerw = Native.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);

        parent = workerw != IntPtr.Zero ? workerw : progman;

        int style = Native.GetWindowLong(Handle, Native.GWL_STYLE);
        style = (style & ~Native.WS_POPUP) | Native.WS_CHILD;
        Native.SetWindowLong(Handle, Native.GWL_STYLE, style);

        Native.SetParent(Handle, parent);
        Reposition();
    }

    /// <summary>Pasang ulang ukuran dan posisi ke monitor utama (dipanggil saat resolusi berubah).</summary>
    public void Reposition()
    {
        if (parent == IntPtr.Zero) return;
        var b = Screen.PrimaryScreen.Bounds;
        var pt = new Native.POINT { X = b.X, Y = b.Y };
        Native.ScreenToClient(parent, ref pt);
        Native.SetWindowPos(Handle, IntPtr.Zero, pt.X, pt.Y, b.Width, b.Height,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }
}
