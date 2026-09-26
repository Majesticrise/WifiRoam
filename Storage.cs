using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace WifiRoam;

public class AppConfig
{
    public int SignalThreshold { get; set; } = 30;
    public int TargetThreshold { get; set; } = 45;
    public int CooldownSeconds { get; set; } = 60;
    public List<string> Priority { get; set; } = new();
    public List<string> Blocked { get; set; } = new();
    public string LogLevel { get; set; } = "switch";
    public int LogMaxSizeMB { get; set; } = 1;
    public int LogRetainDays { get; set; } = 7;
    public bool Notifications { get; set; } = true;
    public bool AutoStart { get; set; } = false;
    public bool CheckUpdate { get; set; } = true;
    public string Version { get; set; } = "1.0.0";
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppConfig))]
internal partial class ConfigJsonContext : JsonSerializerContext { }

internal static class Storage
{
    public static string BaseDir { get; } = AppContext.BaseDirectory;
    public static string ConfigPath { get; } = Path.Combine(BaseDir, "config.json");
    public static string LogDir { get; } = Path.Combine(BaseDir, "logs");
    public static string LogFile { get; } = Path.Combine(LogDir, "wifiroam.log");

    private static readonly object _logLock = new();
    private static FileSystemWatcher? _watcher;
    private static AppConfig _config = new();

    public static AppConfig Config => _config;
    public static event Action<AppConfig>? ConfigChanged;

    // ---------- 初始化 ----------
    public static void Initialize()
    {
        Directory.CreateDirectory(LogDir);
        LoadConfig();
        StartWatcher();
        CleanupOldLogs();
    }

    public static void Shutdown()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    // ---------- 配置加载/保存 ----------
    public static void LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                _config = new AppConfig();
                SaveConfig(_config);
            }
            else
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);
                _config = cfg ?? new AppConfig();
            }

            ApplyToWifi();
            ConfigChanged?.Invoke(_config);
        }
        catch (Exception ex)
        {
            LogError($"加载配置失败: {ex.Message}");
        }
    }

    public static void SaveConfig(AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.AppConfig);
            File.WriteAllText(ConfigPath, json);
            _config = config;
            ApplyToWifi();
            ConfigChanged?.Invoke(_config);
        }
        catch (Exception ex)
        {
            LogError($"保存配置失败: {ex.Message}");
        }
    }

    private static void ApplyToWifi()
    {
        Wifi.SignalThreshold = _config.SignalThreshold;
        Wifi.TargetThreshold = _config.TargetThreshold;
        Wifi.CooldownSeconds = _config.CooldownSeconds;
        Wifi.Blocked = new HashSet<string>(_config.Blocked, StringComparer.OrdinalIgnoreCase);
        Wifi.Priority = new List<string>(_config.Priority);
    }

    // ---------- 热重载 ----------
    private static void StartWatcher()
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        var file = Path.GetFileName(ConfigPath);

        _watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += (_, _) =>
        {
            Thread.Sleep(200); // 简单防抖，等文件写完
            LoadConfig();
            Log("配置已热重载");
        };
    }

    // ---------- 日志 ----------
    public static void Log(string message) => Write("INFO", message);
    public static void LogError(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        if (_config.LogLevel == "off") return;
        if (_config.LogLevel == "switch"
            && level != "ERROR"
            && !message.Contains("切换")) return;

        lock (_logLock)
        {
            try
            {
                RotateIfNeeded();
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFile, line);
            }
            catch
            {
                // 日志失败不抛异常
            }
        }
    }

    private static void RotateIfNeeded()
    {
        var fi = new FileInfo(LogFile);
        if (!fi.Exists) return;
        if (fi.Length < _config.LogMaxSizeMB * 1024L * 1024L) return;

        var bak = LogFile + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bak";
        File.Move(LogFile, bak);
    }

    private static void CleanupOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-_config.LogRetainDays);
            foreach (var f in Directory.GetFiles(LogDir, "*.bak"))
            {
                if (File.GetLastWriteTime(f) < cutoff)
                    File.Delete(f);
            }
        }
        catch { }
    }
}
