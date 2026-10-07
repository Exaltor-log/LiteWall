using System.Globalization;
using System.Runtime.InteropServices;

namespace LiteWall;

/// <summary>
/// Pembungkus tipis libmpv. Semua pengaturan diarahkan ke penggunaan CPU/RAM kecil:
/// decode hardware, tanpa audio, tanpa OSC, buffer demuxer kecil, thread decode dibatasi.
/// </summary>
public sealed class MpvPlayer : IDisposable
{
    const string Dll = "libmpv-2.dll";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr mpv_create();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    static extern int mpv_initialize(IntPtr ctx);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    static extern int mpv_set_option_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    static extern int mpv_set_property_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr mpv_get_property_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    static extern int mpv_command(IntPtr ctx, IntPtr args);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    static extern void mpv_free(IntPtr data);

    IntPtr ctx;
    int fpsLimit = 30;
    double sourceFps;
    string currentFilter = "";
    bool paused;
    int pollTries;
    readonly System.Windows.Forms.Timer pollTimer = new() { Interval = 300 };

    public MpvPlayer(IntPtr windowHandle)
    {
        ctx = mpv_create();
        if (ctx == IntPtr.Zero) throw new InvalidOperationException("Gagal membuat instance mpv.");

        Opt("wid", windowHandle.ToInt64().ToString(CultureInfo.InvariantCulture));
        Opt("vo", "gpu");
        Opt("hwdec", "auto-safe");
        Opt("loop-file", "inf");
        Opt("keep-open", "yes");
        Opt("image-display-duration", "inf");
        Opt("aid", "no");                    // tanpa audio
        Opt("panscan", "1.0");               // isi layar penuh
        Opt("osc", "no");
        Opt("input-default-bindings", "no");
        Opt("input-vo-keyboard", "no");
        Opt("input-cursor", "no");
        Opt("cursor-autohide", "no");
        Opt("vd-lavc-threads", "2");         // batasi thread decode software
        Opt("demuxer-max-bytes", "24MiB");
        Opt("demuxer-max-back-bytes", "4MiB");
        Opt("scale", "bilinear");
        Opt("cscale", "bilinear");
        Opt("dscale", "bilinear");
        Opt("deband", "no");
        Opt("terminal", "no");

        int rc = mpv_initialize(ctx);
        if (rc < 0) throw new InvalidOperationException("Gagal inisialisasi mpv (kode " + rc + ").");

        pollTimer.Tick += (_, _) => PollSourceFps();
    }

    void Opt(string name, string value) => mpv_set_option_string(ctx, name, value);
    void Prop(string name, string value) { if (ctx != IntPtr.Zero) mpv_set_property_string(ctx, name, value); }

    string GetProp(string name)
    {
        if (ctx == IntPtr.Zero) return null;
        IntPtr p = mpv_get_property_string(ctx, name);
        if (p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUTF8(p); }
        finally { mpv_free(p); }
    }

    int Command(params string[] args)
    {
        var ptrs = new IntPtr[args.Length + 1];
        for (int i = 0; i < args.Length; i++) ptrs[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
        ptrs[args.Length] = IntPtr.Zero;

        IntPtr arr = Marshal.AllocHGlobal(IntPtr.Size * ptrs.Length);
        try
        {
            Marshal.Copy(ptrs, 0, arr, ptrs.Length);
            return mpv_command(ctx, arr);
        }
        finally
        {
            for (int i = 0; i < args.Length; i++) Marshal.FreeCoTaskMem(ptrs[i]);
            Marshal.FreeHGlobal(arr);
        }
    }

    public void Play(string path)
    {
        sourceFps = 0;
        SetFilter("", "auto-safe");
        Command("loadfile", path, "replace");
        Prop("pause", paused ? "yes" : "no");

        pollTries = 0;
        pollTimer.Stop();
        pollTimer.Start();
    }

    public void SetPause(bool value)
    {
        if (paused == value) return;
        paused = value;
        Prop("pause", value ? "yes" : "no");
    }

    /// <summary>Atur batas FPS (24 sampai 120). Tidak menaikkan FPS asli video.</summary>
    public void SetFpsLimit(int fps)
    {
        fpsLimit = Math.Clamp(fps, AppSettings.MinFps, AppSettings.MaxFpsLimit);
        if (sourceFps > 0) ApplyFps();
    }

    void PollSourceFps()
    {
        pollTries++;
        var s = GetProp("container-fps");
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f > 0)
        {
            sourceFps = f;
            pollTimer.Stop();
            ApplyFps();
        }
        else if (pollTries > 40)
        {
            pollTimer.Stop();
        }
    }

    void ApplyFps()
    {
        // Bila video sumber lebih cepat dari batas, turunkan lewat filter fps.
        // Filter butuh frame di memori sistem, jadi decode beralih ke mode copy.
        if (sourceFps > fpsLimit + 0.5)
            SetFilter("fps=" + fpsLimit.ToString(CultureInfo.InvariantCulture), "auto-copy-safe");
        else
            SetFilter("", "auto-safe");
    }

    string currentHwdec = "auto-safe";

    void SetFilter(string vf, string hwdec)
    {
        if (hwdec != currentHwdec)
        {
            Prop("hwdec", hwdec);
            currentHwdec = hwdec;
        }
        if (vf != currentFilter)
        {
            Prop("vf", vf);
            currentFilter = vf;
        }
    }

    public void Dispose()
    {
        pollTimer.Stop();
        pollTimer.Dispose();
        if (ctx != IntPtr.Zero)
        {
            mpv_terminate_destroy(ctx);
            ctx = IntPtr.Zero;
        }
    }
}
