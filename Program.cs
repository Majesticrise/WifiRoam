using System;
using System.Threading;
using Avalonia;

namespace WifiRoam;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    public static void Main(string[] args)
    {
        // 单实例：命名互斥体，只允许一个 WifiRoam 进程
        _mutex = new Mutex(true, "Global\\WifiRoam_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            // 已有实例在运行，直接退出
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
