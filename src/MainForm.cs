namespace LiteWall;

public sealed class MainForm : Form
{
    readonly TrayApp app;
    readonly TextBox txtQuery = new() { Width = 220, PlaceholderText = "Cari wallpaper (kosong = populer)" };
    readonly ComboBox cmbSource = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox cmbRes = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Button btnSearch = new() { Text = "Cari", AutoSize = true };
    readonly Button btnLocal = new() { Text = "File sendiri...", AutoSize = true };
    readonly Button btnSettings = new() { Text = "Pengaturan", AutoSize = true };
    readonly Button btnMore = new() { Text = "Muat lebih banyak", AutoSize = true, Visible = false };
    readonly FlowLayoutPanel flow = new() { Dock = DockStyle.Fill, AutoScroll = true };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
    readonly ToolTip tip = new();
    readonly SemaphoreSlim thumbGate = new(4);

    CancellationTokenSource cts;
    int page = 1;

    public bool Quitting;

    public MainForm(TrayApp app)
    {
        this.app = app;
        Text = "LiteWall";
        Size = new Size(1040, 700);
        StartPosition = FormStartPosition.CenterScreen;

        cmbSource.Items.AddRange(new object[] { "Wallhaven (gambar)", "Pexels (video)", "Pixabay (video)" });
        cmbSource.SelectedIndex = 0;

        var screen = Screen.PrimaryScreen.Bounds;
        cmbRes.Items.AddRange(new object[]
        {
            $"Otomatis ({screen.Width}x{screen.Height})", "Full HD (1920x1080)", "2K (2560x1440)", "4K (3840x2160)"
        });
        cmbRes.SelectedIndex = Math.Clamp(app.Settings.ResolutionMode, 0, 3);
        cmbRes.SelectedIndexChanged += (_, _) =>
        {
            app.Settings.ResolutionMode = cmbRes.SelectedIndex;
            app.Settings.Save();
        };

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6), WrapContents = false };
        top.Controls.AddRange(new Control[] { txtQuery, cmbSource, cmbRes, btnSearch, btnLocal, btnSettings });

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
        bottom.Controls.Add(btnMore);

        Controls.Add(flow);
        Controls.Add(bottom);
        Controls.Add(status);
        Controls.Add(top);

        status.Text = "Sumber: Wallhaven, Pexels, Pixabay. Klik gambar untuk memasang sebagai wallpaper.";

        btnSearch.Click += async (_, _) => await SearchAsync(true);
        btnMore.Click += async (_, _) => await SearchAsync(false);
        txtQuery.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(true); }
        };
        btnSettings.Click += (_, _) => app.ShowSettings();
        btnLocal.Click += (_, _) => PickLocal();

        VisibleChanged += async (_, _) =>
        {
            if (Visible && flow.Controls.Count == 0) await SearchAsync(true);
        };
    }

    (int w, int h) Tier()
    {
        return cmbRes.SelectedIndex switch
        {
            1 => (1920, 1080),
            2 => (2560, 1440),
            3 => (3840, 2160),
            _ => (Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height)
        };
    }

    IProvider CurrentProvider()
    {
        var s = app.Settings;
        return cmbSource.SelectedIndex switch
        {
            1 => new PexelsProvider(s.PexelsKey),
            2 => new PixabayProvider(s.PixabayKey),
            _ => new WallhavenProvider(s.WallhavenKey)
        };
    }

    async Task SearchAsync(bool reset)
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        var ct = cts.Token;

        if (reset) { page = 1; ClearGallery(); } else page++;

        var provider = CurrentProvider();
        var s = app.Settings;
        string key = cmbSource.SelectedIndex switch { 1 => s.PexelsKey, 2 => s.PixabayKey, _ => "x" };
        if (provider.NeedsKey && string.IsNullOrWhiteSpace(key))
        {
            status.Text = $"Isi API key {provider.Name} dulu di Pengaturan.";
            btnMore.Visible = false;
            return;
        }

        var (w, h) = Tier();
        status.Text = "Mencari...";
        try
        {
            var items = await provider.SearchAsync(txtQuery.Text.Trim(), page, w, h, ct);
            foreach (var it in items) AddCard(it, ct);
            btnMore.Visible = items.Count > 0;
            status.Text = items.Count > 0
                ? $"{flow.Controls.Count} hasil dari {provider.Name}, minimal {w}x{h}"
                : $"Tidak ada hasil dari {provider.Name} untuk resolusi minimal {w}x{h}";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            status.Text = "Gagal mencari: " + ex.Message;
        }
    }

    void AddCard(WallItem it, CancellationToken ct)
    {
        var pb = new PictureBox
        {
            Width = 240, Height = 135,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(32, 32, 32),
            Margin = new Padding(6),
            Cursor = Cursors.Hand
        };
        var lb = new Label
        {
            Text = $"{it.Width}x{it.Height}" + (it.IsVideo ? "  video" : ""),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(20, 20, 20),
            Dock = DockStyle.Bottom,
            Height = 18,
            Font = new Font(Font.FontFamily, 8f),
            Cursor = Cursors.Hand
        };
        pb.Controls.Add(lb);

        string credit = string.IsNullOrEmpty(it.Credit) ? "" : $"\nOleh: {it.Credit}";
        tip.SetToolTip(pb, $"{it.Source}{credit}");

        async void OnClick(object s, EventArgs e) => await ApplyAsync(it);
        pb.Click += OnClick;
        lb.Click += OnClick;

        flow.Controls.Add(pb);
        _ = LoadThumbAsync(pb, it.ThumbUrl, ct);
    }

    async Task LoadThumbAsync(PictureBox pb, string url, CancellationToken ct)
    {
        bool acquired = false;
        try
        {
            await thumbGate.WaitAsync(ct);
            acquired = true;
            var bytes = await Http.Client.GetByteArrayAsync(url, ct);
            using var ms = new MemoryStream(bytes);
            using var tmp = Image.FromStream(ms);
            var bmp = new Bitmap(tmp);
            if (pb.IsDisposed) bmp.Dispose();
            else pb.Image = bmp;
        }
        catch
        {
            // thumbnail gagal: biarkan kotak kosong
        }
        finally
        {
            if (acquired) thumbGate.Release();
        }
    }

    async Task ApplyAsync(WallItem it)
    {
        status.Text = "Mengunduh...";
        try
        {
            var prog = new Progress<int>(p => status.Text = $"Mengunduh... {p}%");
            string path = await Downloader.DownloadAsync(it, prog, CancellationToken.None);
            app.SetWallpaper(path);
            status.Text = $"Wallpaper terpasang ({it.Source}, {it.Width}x{it.Height})";
        }
        catch (Exception ex)
        {
            status.Text = "Gagal: " + ex.Message;
        }
    }

    void PickLocal()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Pilih video atau gambar",
            Filter = "Video dan gambar|*.mp4;*.mkv;*.webm;*.mov;*.avi;*.gif;*.jpg;*.jpeg;*.png;*.bmp;*.webp|Semua file|*.*"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            app.SetWallpaper(dlg.FileName);
            status.Text = "Wallpaper terpasang: " + Path.GetFileName(dlg.FileName);
        }
    }

    /// <summary>Bebaskan thumbnail dari memori. Dipanggil saat jendela disembunyikan.</summary>
    void ClearGallery()
    {
        flow.SuspendLayout();
        foreach (Control c in flow.Controls.Cast<Control>().ToList())
        {
            if (c is PictureBox pb) pb.Image?.Dispose();
            flow.Controls.Remove(c);
            c.Dispose();
        }
        flow.ResumeLayout();
        btnMore.Visible = false;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!Quitting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            cts?.Cancel();
            ClearGallery();
            Hide();
            GC.Collect();
            return;
        }
        base.OnFormClosing(e);
    }
}
