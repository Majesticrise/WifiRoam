using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace WifiRoam;

public partial class MainWindow : Window
{
    private DispatcherTimer? _timer;

    public MainWindow()
    {
        InitializeComponent();
        InitializeServices();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeServices()
    {
        try
        {
            Storage.Initialize();
            Storage.Log("程序启动");

            // Wifi 的日志转发到 Storage
            Wifi.Log += msg => Storage.Log(msg);

            if (!Wifi.Initialize())
            {
                Storage.LogError("WLAN 初始化失败");
                return;
            }

            // 兜底定时器：每 30 秒评估一次，防止通知丢失
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _timer.Tick += (_, _) => Wifi.Tick();
            _timer.Start();

            // 启动后立即评估一次
            Wifi.Tick();
        }
        catch (Exception ex)
        {
            Storage.LogError($"启动失败: {ex.Message}");
        }
    }
}
