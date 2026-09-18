using System;
using System.Runtime.InteropServices;
using LenovoSettingsGui;
using LenovoSettingsDemo;

internal static class App
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args == null || args.Length == 0 ||
            String.Equals(args[0], "gui", StringComparison.OrdinalIgnoreCase))
        {
            HideConsoleWindow();
            GuiApplication.Run();
            return;
        }
        Environment.ExitCode = Program.Run(args);
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);

    private static void HideConsoleWindow()
    {
        IntPtr handle = GetConsoleWindow();
        if (handle != IntPtr.Zero) ShowWindow(handle, 0);
    }
}
