using System.Diagnostics;

namespace LiteWall;

public sealed class SettingsForm : Form
{
    readonly AppSettings s;
    readonly Action onSaved;

    readonly TrackBar tbFps = new()
    {
        Minimum = AppSettings.MinFps, Maximum = AppSettings.MaxFpsLimit,
        TickFrequency = 12, SmallChange = 1, LargeChange = 6, Width = 380
    };
    readonly Label lblFps = new() { AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };

    readonly TextBox txtWallhaven = new() { Width = 380 };
    readonly TextBox txtPexels = new() { Width = 380 };
    readonly TextBox txtPixabay = new() { Width = 380 };

    readonly CheckBox chkFull = new() { Text = "Jeda saat ada aplikasi layar penuh (game, video)", AutoSize = true };
    readonly CheckBox chkMax = new() { Text = "Jeda juga saat ada jendela maksimal (hemat daya maksimal)", AutoSize = true };
    readonly CheckBox chkBattery = new() { Text = "Jeda saat laptop memakai baterai", AutoSize = true };
    readonly CheckBox chkLock = new() { Text = "Jeda saat layar terkunci", AutoSize = true };
    readonly CheckBox chkStartup = new() { Text = "Jalankan LiteWall saat Windows menyala", AutoSize = true };

    public SettingsForm(AppSettings settings, Action onSaved)
    {
        s = settings;
        this.onSaved = onSaved;

        Text = "Pengaturan LiteWall";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 640);

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12)
        };
        Controls.Add(panel);

        panel.Controls.Add(Heading("Batas FPS"));
        panel.Controls.Add(tbFps);
        panel.Controls.Add(lblFps);
        panel.Controls.Add(Hint("Pilih 24 sampai 120. FPS lebih rendah berarti CPU dan GPU lebih hemat. " +
                                "Batas ini tidak menaikkan FPS video: video 30 FPS tetap berjalan 30 FPS."));

        panel.Controls.Add(Heading("Jeda otomatis"));
        panel.Controls.AddRange(new Control[] { chkFull, chkMax, chkBattery, chkLock });

        panel.Controls.Add(Heading("API key (gratis)"));
        panel.Controls.Add(Caption("Wallhaven (opsional)"));
        panel.Controls.Add(txtWallhaven);
        panel.Controls.Add(Link("Ambil key Wallhaven", "https://wallhaven.cc/settings/account"));
        panel.Controls.Add(Caption("Pexels (wajib untuk video Pexels)"));
        panel.Controls.Add(txtPexels);
        panel.Controls.Add(Link("Ambil key Pexels", "https://www.pexels.com/api/"));
        panel.Controls.Add(Caption("Pixabay (wajib untuk video Pixabay)"));
        panel.Controls.Add(txtPixabay);
        panel.Controls.Add(Link("Ambil key Pixabay", "https://pixabay.com/api/docs/"));

        panel.Controls.Add(Heading("Umum"));
        panel.Controls.Add(chkStartup);

        var btnSave = new Button { Text = "Simpan", AutoSize = true, Margin = new Padding(3, 14, 3, 3) };
        btnSave.Click += (_, _) => Save();
        panel.Controls.Add(btnSave);

        tbFps.Value = Math.Clamp(s.MaxFps, AppSettings.MinFps, AppSettings.MaxFpsLimit);
        tbFps.ValueChanged += (_, _) => UpdateFpsLabel();
        UpdateFpsLabel();

        txtWallhaven.Text = s.WallhavenKey;
        txtPexels.Text = s.PexelsKey;
        txtPixabay.Text = s.PixabayKey;
        chkFull.Checked = s.PauseOnFullscreen;
        chkMax.Checked = s.PauseOnMaximized;
        chkBattery.Checked = s.PauseOnBattery;
        chkLock.Checked = s.PauseOnLock;
        chkStartup.Checked = s.StartWithWindows;
    }

    void UpdateFpsLabel() => lblFps.Text = $"{tbFps.Value} FPS";

    void Save()
    {
        s.MaxFps = tbFps.Value;
        s.WallhavenKey = txtWallhaven.Text.Trim();
        s.PexelsKey = txtPexels.Text.Trim();
        s.PixabayKey = txtPixabay.Text.Trim();
        s.PauseOnFullscreen = chkFull.Checked;
        s.PauseOnMaximized = chkMax.Checked;
        s.PauseOnBattery = chkBattery.Checked;
        s.PauseOnLock = chkLock.Checked;
        s.StartWithWindows = chkStartup.Checked;
        onSaved();
        Close();
    }

    static Label Heading(string text) => new()
    {
        Text = text, AutoSize = true,
        Font = new Font("Segoe UI", 10f, FontStyle.Bold),
        Margin = new Padding(3, 12, 3, 4)
    };

    static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 6, 3, 0) };

    static Label Hint(string text) => new()
    {
        Text = text, MaximumSize = new Size(390, 0), AutoSize = true,
        ForeColor = Color.DimGray
    };

    static LinkLabel Link(string text, string url)
    {
        var l = new LinkLabel { Text = text, AutoSize = true };
        l.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return l;
    }
}
