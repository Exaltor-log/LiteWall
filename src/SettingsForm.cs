using System.Diagnostics;

namespace LiteWall;

public sealed class SettingsForm : Form
{
    static readonly int[] FpsPresets = { 24, 30, 60, 120 };

    readonly AppSettings s;
    readonly Action onSaved;

    readonly FlatSlider tbFps;
    readonly Label lblFps;
    readonly ModernButton[] presetButtons;

    readonly InputBox txtWallhaven;
    readonly InputBox txtPexels;
    readonly InputBox txtPixabay;

    readonly ToggleSwitch chkFull;
    readonly ToggleSwitch chkMax;
    readonly ToggleSwitch chkBattery;
    readonly ToggleSwitch chkLock;
    readonly ToggleSwitch chkStartup;

    public SettingsForm(AppSettings settings, Action onSaved)
    {
        s = settings;
        this.onSaved = onSaved;

        Text = "Pengaturan LiteWall";
        Theme.ApplyWindow(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Px(500), Px(680));

        int side = Px(24);
        int sectionW = ClientSize.Width - side * 2 - SystemInformation.VerticalScrollBarWidth;

        // Judul
        var header = new Panel { Dock = DockStyle.Top, Height = Px(78), BackColor = Theme.Bg };
        var title = MakeLabel("Pengaturan", Theme.Title, Theme.Text);
        var subtitle = MakeLabel("Atur performa, jeda otomatis, dan sumber wallpaper.", Theme.Small, Theme.Muted);
        title.Location = new Point(side, Px(16));
        subtitle.Location = new Point(side, title.Bottom + Px(2));
        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        // Bagian: Batas FPS
        var perf = new Section(sectionW);
        int inner = perf.Inner;
        var fpsSize = TextRenderer.MeasureText("120 FPS", Theme.Value);
        lblFps = new Label
        {
            AutoSize = false, Size = new Size(fpsSize.Width + Px(6), fpsSize.Height),
            Font = Theme.Value, ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleRight
        };
        perf.Add(Heading("Batas FPS"), lblFps);
        tbFps = new FlatSlider(inner)
        {
            Minimum = AppSettings.MinFps, Maximum = AppSettings.MaxFpsLimit, LargeChange = 6,
            AccessibleName = "Batas FPS"
        };
        perf.Add(tbFps, null, 6);
        presetButtons = FpsPresets.Select(v =>
        {
            var b = new ModernButton($"{v} FPS", false, 28);
            b.Click += (_, _) => tbFps.Value = v;
            return b;
        }).ToArray();
        perf.AddInline(10, presetButtons);
        perf.Add(Hint("Pilih 24 sampai 120. FPS lebih rendah berarti CPU dan GPU lebih hemat. " +
                      "Batas ini tidak menaikkan FPS video: video 30 FPS tetap berjalan 30 FPS.", inner), null, 12);

        // Bagian: Jeda otomatis
        var pause = new Section(sectionW);
        pause.Add(Heading("Jeda otomatis"));
        chkFull = new ToggleSwitch("Jeda saat ada aplikasi layar penuh (game, video)", inner);
        chkMax = new ToggleSwitch("Jeda juga saat ada jendela maksimal (hemat daya maksimal)", inner);
        chkBattery = new ToggleSwitch("Jeda saat laptop memakai baterai", inner);
        chkLock = new ToggleSwitch("Jeda saat layar terkunci", inner);
        pause.Add(chkFull, null, 6);
        pause.Add(chkMax, null, 2);
        pause.Add(chkBattery, null, 2);
        pause.Add(chkLock, null, 2);

        // Bagian: API key
        var keys = new Section(sectionW);
        keys.Add(Heading("API key (gratis)"));
        txtWallhaven = new InputBox(inner);
        txtPexels = new InputBox(inner);
        txtPixabay = new InputBox(inner);
        KeyRow(keys, "Wallhaven (opsional)", "Ambil key Wallhaven", "https://wallhaven.cc/settings/account", txtWallhaven, 8);
        KeyRow(keys, "Pexels (wajib untuk video Pexels)", "Ambil key Pexels", "https://www.pexels.com/api/", txtPexels, 14);
        KeyRow(keys, "Pixabay (wajib untuk video Pixabay)", "Ambil key Pixabay", "https://pixabay.com/api/docs/", txtPixabay, 14);
        keys.Add(Hint("Key disimpan sebagai teks biasa di komputer ini.", inner), null, 12);

        // Bagian: Umum
        var general = new Section(sectionW);
        general.Add(Heading("Umum"));
        chkStartup = new ToggleSwitch("Jalankan LiteWall saat Windows menyala", inner);
        general.Add(chkStartup, null, 6);

        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Bg,
            Padding = new Padding(side, 0, 0, Px(8))
        };
        Theme.DarkScrollBars(content);
        content.Controls.AddRange(new Control[] { perf, pause, keys, general });

        // Bilah bawah: Batal dan Simpan
        var footer = new Panel { Dock = DockStyle.Bottom, Height = Px(64), BackColor = Theme.Surface };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };
        var btnSave = new ModernButton("Simpan", true);
        var btnCancel = new ModernButton("Batal");
        btnSave.Width = btnCancel.Width = Math.Max(Math.Max(btnSave.Width, btnCancel.Width), Px(96));
        footer.Controls.Add(btnSave);
        footer.Controls.Add(btnCancel);
        footer.Layout += (_, _) =>
        {
            int y = (footer.Height - btnSave.Height) / 2;
            btnSave.Location = new Point(footer.Width - side - btnSave.Width, y);
            btnCancel.Location = new Point(btnSave.Left - Px(10) - btnCancel.Width, y);
        };
        btnSave.Click += (_, _) => Save();
        btnCancel.Click += (_, _) => Close();
        CancelButton = btnCancel;

        Controls.Add(content);
        Controls.Add(footer);
        Controls.Add(header);

        tbFps.Value = Math.Clamp(s.MaxFps, AppSettings.MinFps, AppSettings.MaxFpsLimit);
        tbFps.ValueChanged += (_, _) => UpdateFpsLabel();
        UpdateFpsLabel();

        txtWallhaven.Box.Text = s.WallhavenKey;
        txtPexels.Box.Text = s.PexelsKey;
        txtPixabay.Box.Text = s.PixabayKey;
        chkFull.Checked = s.PauseOnFullscreen;
        chkMax.Checked = s.PauseOnMaximized;
        chkBattery.Checked = s.PauseOnBattery;
        chkLock.Checked = s.PauseOnLock;
        chkStartup.Checked = s.StartWithWindows;
    }

    int Px(int logical) => LogicalToDeviceUnits(logical);

    void UpdateFpsLabel()
    {
        lblFps.Text = $"{tbFps.Value} FPS";
        for (int i = 0; i < presetButtons.Length; i++)
            presetButtons[i].Primary = FpsPresets[i] == tbFps.Value;
    }

    void Save()
    {
        s.MaxFps = tbFps.Value;
        s.WallhavenKey = txtWallhaven.Box.Text.Trim();
        s.PexelsKey = txtPexels.Box.Text.Trim();
        s.PixabayKey = txtPixabay.Box.Text.Trim();
        s.PauseOnFullscreen = chkFull.Checked;
        s.PauseOnMaximized = chkMax.Checked;
        s.PauseOnBattery = chkBattery.Checked;
        s.PauseOnLock = chkLock.Checked;
        s.StartWithWindows = chkStartup.Checked;
        onSaved();
        Close();
    }

    void KeyRow(Section section, string caption, string linkText, string url, InputBox box, int gapTop)
    {
        section.Add(MakeLabel(caption, Theme.Body, Theme.Muted), Link(linkText, url), gapTop);
        section.Add(box, null, 6);
    }

    static Label MakeLabel(string text, Font font, Color color, int maxWidth = 0)
    {
        var flags = TextFormatFlags.NoPrefix | (maxWidth > 0 ? TextFormatFlags.WordBreak : 0);
        var sz = TextRenderer.MeasureText(text, font, new Size(maxWidth > 0 ? maxWidth : int.MaxValue, int.MaxValue), flags);
        return new Label
        {
            Text = text, Font = font, ForeColor = color, AutoSize = false, UseMnemonic = false,
            Padding = Padding.Empty, Margin = Padding.Empty,
            Size = new Size(maxWidth > 0 ? maxWidth : sz.Width + 2, sz.Height + 2)
        };
    }

    static Label Heading(string text) => MakeLabel(text, Theme.Heading, Theme.Text);

    static Label Hint(string text, int width) => MakeLabel(text, Theme.Small, Theme.Muted, width);

    static LinkLabel Link(string text, string url)
    {
        var sz = TextRenderer.MeasureText(text, Theme.Small);
        var l = new LinkLabel
        {
            Text = text, AutoSize = false, Font = Theme.Small, UseMnemonic = false,
            Size = new Size(sz.Width + 6, sz.Height + 2), TextAlign = ContentAlignment.MiddleRight,
            LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentHover, VisitedLinkColor = Theme.Accent,
            LinkBehavior = LinkBehavior.HoverUnderline
        };
        l.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return l;
    }
}
