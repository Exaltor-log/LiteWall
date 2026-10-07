namespace LiteWall;

public sealed class MainForm : Form
{
    readonly TrayApp app;
    readonly InputBox search;
    readonly TextBox txtQuery;
    readonly DarkComboBox cmbSource;
    readonly DarkComboBox cmbRes;
    readonly ModernButton btnSearch = new("Cari", true);
    readonly ModernButton btnLocal = new("File sendiri...");
    readonly ModernButton btnSettings = new("Pengaturan");
    readonly ModernButton btnMore = new("Muat lebih banyak");
    readonly Panel moreBar = new() { Dock = DockStyle.Bottom, BackColor = Theme.Bg, Visible = false };
    readonly FlowLayoutPanel flow = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
    readonly Label lblEmpty = new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Visible = false,
        BackColor = Theme.Bg, ForeColor = Theme.Muted, Font = Theme.Heading, UseMnemonic = false
    };
    readonly StatusBar status = new() { Dock = DockStyle.Bottom };
    readonly ToolTip tip = new() { OwnerDraw = true };
    readonly ToolTip headerTip = new() { OwnerDraw = true };
    readonly SemaphoreSlim thumbGate = new(4);
    readonly Size cardSize;
    readonly Padding cardMargin;

    CancellationTokenSource cts;
    int page = 1;

    public bool Quitting;

    public MainForm(TrayApp app)
    {
        this.app = app;
        Text = "LiteWall";
        Theme.ApplyWindow(this);
        Size = new Size(Px(1180), Px(760));
        MinimumSize = new Size(Px(900), Px(520));
        StartPosition = FormStartPosition.CenterScreen;

        cardSize = new Size(Px(256), Px(144));
        cardMargin = new Padding(Px(6));

        search = new InputBox(Px(240), "");
        txtQuery = search.Box;
        txtQuery.PlaceholderText = "Cari wallpaper (kosong = populer)";

        cmbSource = new DarkComboBox(Px(170));
        cmbSource.Items.AddRange(new object[] { "Wallhaven (gambar)", "Pexels (video)", "Pixabay (video)" });
        cmbSource.SelectedIndex = 0;

        var screen = Screen.PrimaryScreen.Bounds;
        cmbRes = new DarkComboBox(Px(190));
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

        // Bilah atas: logo, kotak cari (melebar), filter, dan tombol.
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = Px(64), BackColor = Theme.Surface,
            ColumnCount = 7, RowCount = 1, Margin = Padding.Empty,
            Padding = new Padding(Px(16), 0, Px(12), 0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++) header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        var logo = new LogoLabel { BackColor = Theme.Surface, Anchor = AnchorStyles.Left };
        search.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        Control[] row = { logo, search, cmbSource, cmbRes, btnSearch, btnLocal, btnSettings };
        for (int i = 0; i < row.Length; i++)
        {
            if (i != 1) row[i].Anchor = AnchorStyles.Left;
            header.Controls.Add(row[i], i, 0);
        }

        StyleTip(tip);
        StyleTip(headerTip);
        headerTip.SetToolTip(btnLocal, "Pakai video atau gambar dari komputer ini");
        headerTip.SetToolTip(btnSettings, "FPS, jeda otomatis, dan API key");

        // Grid thumbnail. Padding kiri kanan diatur supaya kolom selalu di tengah.
        Theme.DarkScrollBars(flow);
        flow.Resize += (_, _) => UpdateGridPadding();

        moreBar.Height = Px(60);
        moreBar.Controls.Add(btnMore);
        moreBar.Layout += (_, _) => btnMore.Location = new Point(
            (moreBar.Width - btnMore.Width) / 2, (moreBar.Height - btnMore.Height) / 2);

        Controls.Add(lblEmpty);
        Controls.Add(flow);
        Controls.Add(moreBar);
        Controls.Add(status);
        Controls.Add(header);

        status.SetStatus("Sumber: Wallhaven, Pexels, Pixabay. Klik gambar untuk memasang sebagai wallpaper.");

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

    int Px(int logical) => LogicalToDeviceUnits(logical);

    void StyleTip(ToolTip t)
    {
        t.Popup += (_, e) =>
        {
            var sz = TextRenderer.MeasureText(t.GetToolTip(e.AssociatedControl), Theme.Small);
            e.ToolTipSize = new Size(sz.Width + Px(18), sz.Height + Px(12));
        };
        t.Draw += (_, e) =>
        {
            var g = e.Graphics;
            using (var br = new SolidBrush(Theme.Input)) g.FillRectangle(br, e.Bounds);
            using (var pen = new Pen(Theme.Border)) g.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            var r = Rectangle.Inflate(e.Bounds, -Px(9), -Px(6));
            TextRenderer.DrawText(g, e.ToolTipText, Theme.Small, r, Theme.Text, Theme.Input, TextFormatFlags.NoPrefix);
        };
    }

    void UpdateGridPadding()
    {
        int basePad = Px(14);
        int cell = cardSize.Width + cardMargin.Horizontal;
        // Lebar scrollbar selalu disisihkan supaya jumlah kolom tidak berubah saat scrollbar muncul.
        int avail = flow.Width - SystemInformation.VerticalScrollBarWidth - basePad * 2;
        int cols = Math.Max(1, avail / cell);
        int side = basePad + Math.Max(0, (avail - cols * cell) / 2);
        var p = new Padding(side, basePad, side, basePad);
        if (flow.Padding != p) flow.Padding = p;
    }

    void SetEmpty(string message)
    {
        lblEmpty.Text = message ?? "";
        lblEmpty.Visible = message != null;
    }

    void ShowMore(bool visible) => moreBar.Visible = visible;

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
            string msg = $"Isi API key {provider.Name} dulu di Pengaturan.";
            status.SetStatus(msg, true);
            if (flow.Controls.Count == 0) SetEmpty(msg);
            ShowMore(false);
            return;
        }

        var (w, h) = Tier();
        status.SetStatus("Mencari...");
        if (flow.Controls.Count == 0) SetEmpty("Mencari...");
        btnMore.Enabled = false;
        try
        {
            var items = await provider.SearchAsync(txtQuery.Text.Trim(), page, w, h, ct);
            flow.SuspendLayout();
            foreach (var it in items) AddCard(it, ct);
            flow.ResumeLayout();
            ShowMore(items.Count > 0);
            if (items.Count > 0)
            {
                SetEmpty(null);
                status.SetStatus($"{flow.Controls.Count} hasil dari {provider.Name}, minimal {w}x{h}");
            }
            else
            {
                string msg = $"Tidak ada hasil dari {provider.Name} untuk resolusi minimal {w}x{h}";
                status.SetStatus(msg);
                if (flow.Controls.Count == 0) SetEmpty(msg);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            status.SetStatus("Gagal mencari: " + ex.Message, true);
            if (flow.Controls.Count == 0) SetEmpty("Gagal mencari. Periksa koneksi atau API key.");
        }
        finally
        {
            btnMore.Enabled = true;
        }
    }

    void AddCard(WallItem it, CancellationToken ct)
    {
        var card = new ThumbCard(it, cardSize, cardMargin);

        string credit = string.IsNullOrEmpty(it.Credit) ? "" : $"\nOleh: {it.Credit}";
        tip.SetToolTip(card, $"{it.Source}{credit}");

        card.Click += async (_, _) => await ApplyAsync(it);

        flow.Controls.Add(card);
        _ = LoadThumbAsync(card, it.ThumbUrl, ct);
    }

    async Task LoadThumbAsync(ThumbCard card, string url, CancellationToken ct)
    {
        bool acquired = false;
        var size = card.ClientSize;
        try
        {
            await thumbGate.WaitAsync(ct);
            acquired = true;
            var bytes = await Http.Client.GetByteArrayAsync(url, ct);
            // Decode dan perkecil di thread latar supaya UI tidak tersendat.
            var bmp = await Task.Run(() => ThumbCard.Render(bytes, size), ct);
            if (card.IsDisposed) bmp.Dispose();
            else card.SetThumb(bmp);
        }
        catch
        {
            // thumbnail gagal: tampilkan keterangan di kartu
            if (!card.IsDisposed && !ct.IsCancellationRequested) card.SetFailed();
        }
        finally
        {
            if (acquired) thumbGate.Release();
        }
    }

    async Task ApplyAsync(WallItem it)
    {
        status.SetStatus("Mengunduh...", false, 0);
        try
        {
            var prog = new Progress<int>(p => status.SetStatus($"Mengunduh... {p}%", false, p));
            string path = await Downloader.DownloadAsync(it, prog, CancellationToken.None);
            app.SetWallpaper(path);
            status.SetStatus($"Wallpaper terpasang ({it.Source}, {it.Width}x{it.Height})");
        }
        catch (Exception ex)
        {
            status.SetStatus("Gagal: " + ex.Message, true);
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
            status.SetStatus("Wallpaper terpasang: " + Path.GetFileName(dlg.FileName));
        }
    }

    /// <summary>Bebaskan thumbnail dari memori. Dipanggil saat jendela disembunyikan.</summary>
    void ClearGallery()
    {
        flow.SuspendLayout();
        foreach (Control c in flow.Controls.Cast<Control>().ToList())
        {
            tip.SetToolTip(c, null);
            flow.Controls.Remove(c);
            c.Dispose(); // ThumbCard ikut membuang bitmap-nya
        }
        flow.ResumeLayout();
        ShowMore(false);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!Quitting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            cts?.Cancel();
            ClearGallery();
            SetEmpty(null);
            Hide();
            GC.Collect();
            return;
        }
        base.OnFormClosing(e);
    }
}
