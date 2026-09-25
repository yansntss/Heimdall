using Microsoft.Win32;

namespace InfoBar.Services;

/// <summary>Liga/desliga "Iniciar com o Windows" via HKCU\...\Run — sem instalador nem tarefa agendada.</summary>
internal static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "InfoBar";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath)) key?.SetValue(ValueName, $"\"{exePath}\"");
        }
        else
        {
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
