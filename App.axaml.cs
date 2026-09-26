using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WifiRoam;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainWindow = new MainWindow();
            desktop.MainWindow = _mainWindow;

            SetupTrayIcon(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var icon = CreateSimpleIcon();

            _trayIcon = new TrayIcon
            {
                Icon = icon,
                ToolTipText = "WifiRoam",
                IsVisible = true
            };

            var menu = new NativeMenu();

            var openItem = new NativeMenuItem("打开主界面");
            openItem.Click += (_, _) =>
            {
                if (_mainWindow != null)
                {
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Activate();
                }
            };
            menu.Add(openItem);

            var pauseItem = new NativeMenuItem("暂停自动切换");
            pauseItem.Click += (_, _) =>
            {
                // 简单切换，后续可接入配置
                pauseItem.Header = pauseItem.Header == "暂停自动切换" ? "启用自动切换" : "暂停自动切换";
            };
            menu.Add(pauseItem);

            menu.Add(new NativeMenuItemSeparator());

            var exitItem = new NativeMenuItem("退出");
            exitItem.Click += (_, _) =>
            {
                Storage.Shutdown();
                Wifi.Shutdown();
                desktop.Shutdown();
            };
            menu.Add(exitItem);

            _trayIcon.Menu = menu;
            var icons = new TrayIcons { _trayIcon };
            TrayIcon.SetIcons(this, icons);
        }
        catch (Exception ex)
        {
            Storage.LogError($"托盘初始化失败: {ex.Message}");
        }
    }

    private static WindowIcon CreateSimpleIcon()
    {
        // 创建一个 16x16 的蓝色方块作为托盘图标
        var bitmap = new WriteableBitmap(new PixelSize(16, 16), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var fb = bitmap.Lock())
        {
            unsafe
            {
                var ptr = (uint*)fb.Address;
                for (int i = 0; i < 16 * 16; i++)
                {
                    ptr[i] = 0xFF0000FF; // ABGR: 蓝色，不透明
                }
            }
        }
        return new WindowIcon(bitmap);
    }
}
