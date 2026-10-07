using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace LiteWall;

public sealed class TrayApp : ApplicationContext
{
    public AppSettings Settings { get; }

    readonly WallpaperHost host;
    readonly MpvPlayer player;
    readonly AutoPauseMonitor monitor;
    readonly NotifyIcon tray;
    readonly ToolStripMenuItem pauseItem;
    MainForm main;
    bool manualPause;

    public TrayApp()
    {
        Settings = AppSettings.Load();

        host = new WallpaperHost();
        host.Show();
        host.Attach();

        player = new MpvPlayer(host.Handle);
        player.SetFpsLimit(Settings.MaxFps);

        monitor = new AutoPauseMonitor(Settings);
        monitor.Changed += UpdatePause;

        pauseItem = new ToolStripMenuItem("Jeda wallpaper", null, (_, _) => TogglePause());

        var menu = new ContextMenuStrip();
        menu.Items.Add("Buka LiteWall", null, (_, _) => ShowMain());
        menu.Items.Add(pauseItem);
        menu.Items.Add("Pengaturan", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Keluar", null, (_, _) => Quit());

        tray = new NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "LiteWall",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => ShowMain();

        SystemEvents.DisplaySettingsChanged += (_, _) => host.Reposition();

        if (File.Exists(Settings.CurrentWallpaper))
            player.Play(Settings.CurrentWallpaper);
        else
            ShowMain();
    }

    public void SetWallpaper(string path)
    {
        Settings.CurrentWallpaper = path;
        Settings.Save();
        player.Play(path);
        Downloader.Prune(path);
    }

    public void ShowMain()
    {
        if (main == null || main.IsDisposed) main = new MainForm(this);
        main.Show();
        main.WindowState = FormWindowState.Normal;
        main.Activate();
    }

    public void ShowSettings()
    {
        using var f = new SettingsForm(Settings, ApplySettings);
        f.ShowDialog();
    }

    void ApplySettings()
    {
        Settings.Save();
        player.SetFpsLimit(Settings.MaxFps);
        monitor.Evaluate();
        SetStartup(Settings.StartWithWindows);
    }

    void TogglePause()
    {
        manualPause = !manualPause;
        pauseItem.Text = manualPause ? "Lanjutkan wallpaper" : "Jeda wallpaper";
        UpdatePause();
    }

    void UpdatePause() => player.SetPause(manualPause || monitor.ShouldPause);

    void Quit()
    {
        tray.Visible = false;
        monitor.Dispose();
        player.Dispose();
        if (main != null && !main.IsDisposed) { main.Quitting = true; main.Close(); }
        host.Close();
        ExitThread();
    }

    static void SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (enable) key.SetValue("LiteWall", $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue("LiteWall", false);
        }
        catch
        {
            // abaikan
        }
    }

    static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var br = new LinearGradientBrush(new Rectangle(0, 0, 32, 32),
            Color.FromArgb(80, 160, 255), Color.FromArgb(150, 80, 255), 45f);
        g.FillEllipse(br, 2, 2, 28, 28);
        return Icon.FromHandle(bmp.GetHicon());
    }
}
