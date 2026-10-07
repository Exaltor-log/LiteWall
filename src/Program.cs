namespace LiteWall;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "LiteWall.SingleInstance", out bool created);
        if (!created) return;

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            var app = new TrayApp();
            Application.Run(app);
        }
        catch (DllNotFoundException)
        {
            MessageBox.Show(
                "File libmpv-2.dll tidak ditemukan di folder aplikasi.\nPastikan file itu berada satu folder dengan LiteWall.exe.",
                "LiteWall", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "LiteWall", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
