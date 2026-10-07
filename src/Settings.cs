using System.Text.Json;

namespace LiteWall;

public class AppSettings
{
    public const int MinFps = 24;
    public const int MaxFpsLimit = 120;

    int maxFps = 30;

    /// <summary>Batas FPS pemutaran, dipilih pengguna antara 24 dan 120.</summary>
    public int MaxFps
    {
        get => maxFps;
        set => maxFps = Math.Clamp(value, MinFps, MaxFpsLimit);
    }

    /// <summary>0 = otomatis (resolusi layar), 1 = Full HD, 2 = 2K, 3 = 4K.</summary>
    public int ResolutionMode { get; set; } = 0;

    public string WallhavenKey { get; set; } = "";
    public string PexelsKey { get; set; } = "";
    public string PixabayKey { get; set; } = "";

    public bool PauseOnFullscreen { get; set; } = true;
    public bool PauseOnMaximized { get; set; } = false;
    public bool PauseOnBattery { get; set; } = true;
    public bool PauseOnLock { get; set; } = true;

    public bool StartWithWindows { get; set; } = false;
    public string CurrentWallpaper { get; set; } = "";

    static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiteWall");

    static string FilePath => Path.Combine(ConfigDir, "settings.json");

    public static string CacheDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LiteWall", "cache");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            // file rusak: pakai default
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // gagal simpan: abaikan, aplikasi tetap jalan
        }
    }
}
