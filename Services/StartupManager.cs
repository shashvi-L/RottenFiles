using Microsoft.Win32;
using System;

namespace RottenFiles.Services;

public static class StartupManager
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (enabled)
        {
            key?.SetValue("RottenFiles", Environment.ProcessPath);
        }
        else
        {
            key?.DeleteValue("RottenFiles", false);
        }
    }

    public static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("RottenFiles") != null;
    }
}