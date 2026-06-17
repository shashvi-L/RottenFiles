using System;
using System.Threading;
using System.Windows;

namespace RottenFiles;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string appName = "RottenFiles";
        _mutex = new Mutex(true, appName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("RottenFiles is already running.", appName,
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}