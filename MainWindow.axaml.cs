using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        BindEvents();
        LoadConfigToUi();
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

            Wifi.Log += msg => Storage.Log(msg);
            Wifi.StatusChanged += (ssid, signal) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var ssidText = this.FindControl<TextBlock>("CurrentSsidText");
                    var signalText = this.FindControl<TextBlock>("CurrentSignalText");
                    if (ssidText != null) ssidText.Text = $"当前 SSID: {ssid}";
                    if (signalText != null) signalText.Text = $"信号强度: {signal}%";
                });
            };

            if (!Wifi.Initialize())
            {
                Storage.LogError("WLAN 初始化失败");
                return;
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _timer.Tick += (_, _) => Wifi.Tick();
            _timer.Start();

            Wifi.Tick();
        }
        catch (Exception ex)
        {
            Storage.LogError($"启动失败: {ex.Message}");
        }
    }

    private void BindEvents()
    {
        var refreshBtn = this.FindControl<Button>("RefreshBtn");
        if (refreshBtn != null) refreshBtn.Click += (_, _) => RefreshNetworks();

        var saveBtn = this.FindControl<Button>("SaveConfigBtn");
        if (saveBtn != null) saveBtn.Click += (_, _) => SaveConfigFromUi();

        var refreshLogBtn = this.FindControl<Button>("RefreshLogBtn");
        if (refreshLogBtn != null) refreshLogBtn.Click += (_, _) => RefreshLog();

        var openLogFolderBtn = this.FindControl<Button>("OpenLogFolderBtn");
        if (openLogFolderBtn != null) openLogFolderBtn.Click += (_, _) => OpenLogFolder();

        var checkUpdateBtn = this.FindControl<Button>("CheckUpdateBtn");
        if (checkUpdateBtn != null) checkUpdateBtn.Click += (_, _) =>
        {
            var status = this.FindControl<TextBlock>("UpdateStatusText");
            if (status != null) status.Text = "更新检查将在后续阶段实现";
        };

        // 配置热重载后刷新 UI
        Storage.ConfigChanged += _ => Dispatcher.UIThread.Post(LoadConfigToUi);
    }

    private void LoadConfigToUi()
    {
        var cfg = Storage.Config;
        var signalBox = this.FindControl<NumericUpDown>("SignalThresholdBox");
        if (signalBox != null) signalBox.Value = cfg.SignalThreshold;

        var targetBox = this.FindControl<NumericUpDown>("TargetThresholdBox");
        if (targetBox != null) targetBox.Value = cfg.TargetThreshold;

        var cooldownBox = this.FindControl<NumericUpDown>("CooldownBox");
        if (cooldownBox != null) cooldownBox.Value = cfg.CooldownSeconds;

        var blockedBox = this.FindControl<TextBox>("BlockedBox");
        if (blockedBox != null) blockedBox.Text = string.Join(",", cfg.Blocked);

        var logLevelBox = this.FindControl<ComboBox>("LogLevelBox");
        if (logLevelBox != null) logLevelBox.SelectedIndex = cfg.LogLevel switch
        {
            "off" => 0,
            "debug" => 2,
            _ => 1
        };

        var autoStartCheck = this.FindControl<CheckBox>("AutoStartCheck");
        if (autoStartCheck != null) autoStartCheck.IsChecked = cfg.AutoStart;

        var notificationCheck = this.FindControl<CheckBox>("NotificationCheck");
        if (notificationCheck != null) notificationCheck.IsChecked = cfg.Notifications;

        var versionText = this.FindControl<TextBlock>("VersionText");
        if (versionText != null) versionText.Text = $"WifiRoam v{cfg.Version}";
    }

    private void SaveConfigFromUi()
    {
        var cfg = Storage.Config;
        cfg.SignalThreshold = (int)(this.FindControl<NumericUpDown>("SignalThresholdBox")?.Value ?? 30);
        cfg.TargetThreshold = (int)(this.FindControl<NumericUpDown>("TargetThresholdBox")?.Value ?? 45);
        cfg.CooldownSeconds = (int)(this.FindControl<NumericUpDown>("CooldownBox")?.Value ?? 60);
        cfg.Blocked = (this.FindControl<TextBox>("BlockedBox")?.Text ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        cfg.LogLevel = (this.FindControl<ComboBox>("LogLevelBox")?.SelectedIndex) switch
        {
            0 => "off",
            2 => "debug",
            _ => "switch"
        };
        cfg.AutoStart = this.FindControl<CheckBox>("AutoStartCheck")?.IsChecked ?? false;
        cfg.Notifications = this.FindControl<CheckBox>("NotificationCheck")?.IsChecked ?? true;

        Storage.SaveConfig(cfg);
        Storage.Log("配置已保存");
    }

    private void RefreshNetworks()
    {
        var list = this.FindControl<ListBox>("NetworkList");
        if (list == null) return;

        var networks = Wifi.ScanAvailableNetworks();
        list.ItemsSource = networks.Select(n =>
            $"{n.Ssid}  |  信号 {n.Signal}%  |  {(n.HasProfile ? "已保存" : "未保存")}").ToList();

        var hint = this.FindControl<TextBlock>("StatusHintText");
        if (hint != null) hint.Text = $"扫描到 {networks.Count} 个网络，最后刷新 {DateTime.Now:HH:mm:ss}";
    }

    private void RefreshLog()
    {
        var logBox = this.FindControl<TextBox>("LogTextBox");
        if (logBox == null) return;

        try
        {
            if (File.Exists(Storage.LogFile))
                logBox.Text = File.ReadAllText(Storage.LogFile);
            else
                logBox.Text = "暂无日志";
        }
        catch (Exception ex)
        {
            logBox.Text = $"读取日志失败: {ex.Message}";
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Storage.LogDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Storage.LogError($"打开日志文件夹失败: {ex.Message}");
        }
    }
}
