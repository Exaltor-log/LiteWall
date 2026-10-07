using System.Text.Json;

namespace LiteWall;

public sealed record WallItem(
    string Source, string Id, string ThumbUrl, string FileUrl,
    int Width, int Height, bool IsVideo, string Credit);

public interface IProvider
{
    string Name { get; }
    bool NeedsKey { get; }
    Task<List<WallItem>> SearchAsync(string query, int page, int minW, int minH, CancellationToken ct);
}

static class Http
{
    public static readonly HttpClient Client = Create();

    static HttpClient Create()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("LiteWall/0.1");
        return c;
    }

    public static async Task<JsonDocument> GetJsonAsync(HttpRequestMessage req, CancellationToken ct)
    {
        using var res = await Client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Server menjawab HTTP {(int)res.StatusCode}. Cek API key atau batas permintaan.");
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
    }
}

/// <summary>Gambar statis. Filter resolusi minimum langsung di sisi server.</summary>
public sealed class WallhavenProvider : IProvider
{
    readonly string key;
    public WallhavenProvider(string key) { this.key = key; }
    public string Name => "Wallhaven";
    public bool NeedsKey => false;

    public async Task<List<WallItem>> SearchAsync(string q, int page, int minW, int minH, CancellationToken ct)
    {
        string sorting = string.IsNullOrWhiteSpace(q) ? "toplist" : "relevance";
        string url = $"https://wallhaven.cc/api/v1/search?q={Uri.EscapeDataString(q ?? "")}" +
                     $"&atleast={minW}x{minH}&ratios=landscape&sorting={sorting}&purity=100&categories=111&page={page}";
        if (!string.IsNullOrWhiteSpace(key)) url += "&apikey=" + Uri.EscapeDataString(key);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var doc = await Http.GetJsonAsync(req, ct);

        var list = new List<WallItem>();
        foreach (var d in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            string id = d.GetProperty("id").GetString();
            string path = d.GetProperty("path").GetString();
            string thumb = d.GetProperty("thumbs").GetProperty("large").GetString();
            int w = d.GetProperty("dimension_x").GetInt32();
            int h = d.GetProperty("dimension_y").GetInt32();
            list.Add(new WallItem(Name, id, thumb, path, w, h, false, ""));
        }
        return list;
    }
}

/// <summary>Video loop dari Pexels. Mengambil file mp4 terkecil yang masih memenuhi resolusi minimum.</summary>
public sealed class PexelsProvider : IProvider
{
    readonly string key;
    public PexelsProvider(string key) { this.key = key; }
    public string Name => "Pexels";
    public bool NeedsKey => true;

    public async Task<List<WallItem>> SearchAsync(string q, int page, int minW, int minH, CancellationToken ct)
    {
        string query = string.IsNullOrWhiteSpace(q) ? "nature" : q;
        string size = minW >= 3840 ? "large" : "medium";
        string url = $"https://api.pexels.com/videos/search?query={Uri.EscapeDataString(query)}" +
                     $"&per_page=24&page={page}&orientation=landscape&size={size}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", key);
        using var doc = await Http.GetJsonAsync(req, ct);

        var list = new List<WallItem>();
        foreach (var v in doc.RootElement.GetProperty("videos").EnumerateArray())
        {
            string id = v.GetProperty("id").GetInt64().ToString();
            string thumb = v.GetProperty("image").GetString();
            string credit = "";
            if (v.TryGetProperty("user", out var u) && u.TryGetProperty("name", out var n))
                credit = n.GetString() ?? "";

            string bestUrl = null;
            int bestW = int.MaxValue, bestH = 0;
            foreach (var f in v.GetProperty("video_files").EnumerateArray())
            {
                if (f.GetProperty("file_type").GetString() != "video/mp4") continue;
                if (f.GetProperty("width").ValueKind != JsonValueKind.Number) continue;
                int w = f.GetProperty("width").GetInt32();
                int h = f.GetProperty("height").GetInt32();
                if (w >= minW && w < bestW)
                {
                    bestW = w; bestH = h;
                    bestUrl = f.GetProperty("link").GetString();
                }
            }
            if (bestUrl != null)
                list.Add(new WallItem(Name, id, thumb, bestUrl, bestW, bestH, true, credit));
        }
        return list;
    }
}

/// <summary>Video loop dari Pixabay.</summary>
public sealed class PixabayProvider : IProvider
{
    readonly string key;
    public PixabayProvider(string key) { this.key = key; }
    public string Name => "Pixabay";
    public bool NeedsKey => true;

    static readonly string[] Variants = { "tiny", "small", "medium", "large" };

    public async Task<List<WallItem>> SearchAsync(string q, int page, int minW, int minH, CancellationToken ct)
    {
        string url = $"https://pixabay.com/api/videos/?key={Uri.EscapeDataString(key)}" +
                     $"&q={Uri.EscapeDataString(q ?? "")}&per_page=24&page={page}&safesearch=true";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var doc = await Http.GetJsonAsync(req, ct);

        var list = new List<WallItem>();
        if (!doc.RootElement.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var hit in hits.EnumerateArray())
        {
            // Lewati entri yang tidak lengkap, jangan gagalkan seluruh pencarian.
            if (!hit.TryGetProperty("id", out var idEl) || !hit.TryGetProperty("videos", out var videos)) continue;
            string id = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64().ToString() : idEl.ToString();
            string credit = hit.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.String
                ? u.GetString() ?? "" : "";

            string bestUrl = null, thumb = null;
            int bestW = int.MaxValue, bestH = 0;
            foreach (var name in Variants)
            {
                if (!videos.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Object) continue;
                // Thumbnail terkecil sudah cukup untuk kartu galeri dan hemat unduhan.
                thumb ??= Str(v, "thumbnail");
                string link = Str(v, "url");
                int w = Int(v, "width"), h = Int(v, "height");
                if (string.IsNullOrEmpty(link) || w < minW) continue;
                if (w < bestW) { bestW = w; bestH = h; bestUrl = link; }
            }

            // API lama memakai picture_id, API baru memakai field thumbnail per ukuran video.
            if (string.IsNullOrEmpty(thumb) && Str(hit, "picture_id") is { Length: > 0 } pic)
                thumb = $"https://i.vimeocdn.com/video/{pic}_640x360.jpg";

            if (bestUrl != null)
                list.Add(new WallItem(Name, id, thumb ?? "", bestUrl, bestW, bestH, true, credit));
        }
        return list;
    }

    static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : 0;
}

static class Downloader
{
    const long MaxCacheBytes = 2L * 1024 * 1024 * 1024;

    public static async Task<string> DownloadAsync(WallItem it, IProgress<int> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(AppSettings.CacheDir);

        string ext = Path.GetExtension(new Uri(it.FileUrl).AbsolutePath);
        if (string.IsNullOrEmpty(ext)) ext = it.IsVideo ? ".mp4" : ".jpg";

        string path = Path.Combine(AppSettings.CacheDir, $"{it.Source}_{it.Id}{ext}");
        if (File.Exists(path)) return path;

        string tmp = path + ".part";
        using var res = await Http.Client.GetAsync(it.FileUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        long total = res.Content.Headers.ContentLength ?? -1;

        await using (var src = await res.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(tmp))
        {
            var buf = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                if (total > 0) progress?.Report((int)(done * 100 / total));
            }
        }

        File.Move(tmp, path, true);
        return path;
    }

    /// <summary>Hapus file cache terlama bila total melebihi 2 GB. File yang sedang dipakai dilewati.</summary>
    public static void Prune(string keepPath)
    {
        try
        {
            var files = new DirectoryInfo(AppSettings.CacheDir).GetFiles()
                .OrderBy(f => f.LastWriteTimeUtc).ToList();
            long total = files.Sum(f => f.Length);
            foreach (var f in files)
            {
                if (total <= MaxCacheBytes) break;
                if (string.Equals(f.FullName, keepPath, StringComparison.OrdinalIgnoreCase)) continue;
                total -= f.Length;
                f.Delete();
            }
        }
        catch
        {
            // abaikan
        }
    }
}
