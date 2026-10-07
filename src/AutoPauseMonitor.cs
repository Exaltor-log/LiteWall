using System.Text;
using Microsoft.Win32;

namespace LiteWall;

/// <summary>
/// Mengecek tiap detik apakah wallpaper perlu dijeda: aplikasi layar penuh, jendela maksimal,
/// layar terkunci, atau laptop memakai baterai. Hanya satu timer ringan, tanpa hook global.
/// </summary>
public sealed class AutoPauseMonitor : IDisposable
{
    readonly AppSettings settings;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    bool locked;
    bool current;

    public event Action Changed;
    public bool ShouldPause => current;

    public AutoPauseMonitor(AppSettings settings)
    {
        this.settings = settings;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        timer.Tick += (_, _) => Evaluate();
        timer.Start();
    }

    void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock) locked = true;
        else if (e.Reason == SessionSwitchReason.SessionUnlock) locked = false;
        Evaluate();
    }

    public void Evaluate()
    {
        bool pause = Compute();
        if (pause != current)
        {
            current = pause;
            Changed?.Invoke();
        }
    }

    bool Compute()
    {
        if (settings.PauseOnLock && locked) return true;

        if (settings.PauseOnBattery &&
            SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline)
            return true;

        if (!settings.PauseOnFullscreen && !settings.PauseOnMaximized) return false;
        return ForegroundCoversScreen();
    }

    bool ForegroundCoversScreen()
    {
        IntPtr h = Native.GetForegroundWindow();
        if (h == IntPtr.Zero || Native.IsIconic(h)) return false;

        Native.GetWindowThreadProcessId(h, out uint pid);
        if (pid == Environment.ProcessId) return false;

        var sb = new StringBuilder(64);
        Native.GetClassName(h, sb, sb.Capacity);
        string cls = sb.ToString();
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "SHELLDLL_DefView")
            return false;

        var screen = Screen.FromHandle(h);
        if (!screen.Primary) return false;

        Native.GetWindowRect(h, out var r);
        var b = screen.Bounds;
        bool coversAll = r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        bool noCaption = (Native.GetWindowLong(h, Native.GWL_STYLE) & Native.WS_CAPTION) == 0;

        if (settings.PauseOnFullscreen && coversAll && noCaption) return true;
        if (settings.PauseOnMaximized && Native.IsZoomed(h)) return true;
        return false;
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        timer.Stop();
        timer.Dispose();
    }
}
