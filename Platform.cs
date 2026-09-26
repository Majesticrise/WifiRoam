using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WifiRoam;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(GitHubRelease))]
internal partial class ReleaseJsonContext : JsonSerializerContext { }

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }
    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }
    [JsonPropertyName("assets")]
    public GitHubAsset[]? Assets { get; set; }
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
}

internal static class Platform
{
    public const string TaskName = "WifiRoam";
    private const string RepoOwner = "Majesticrise";
    private const string RepoName = "WifiRoam";

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // ---------- 计划任务 ----------
    public static bool IsAutoStartEnabled()
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks",
                $"/Query /TN \"{TaskName}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool EnableAutoStart()
    {
        try
        {
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "WifiRoam.exe");
            var args = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\"\" /SC ONLOGON /RL HIGHEST /F";
            return RunElevated("schtasks", args, out _);
        }
        catch (Exception ex)
        {
            Storage.LogError($"创建计划任务失败: {ex.Message}");
            return false;
        }
    }

    public static bool DisableAutoStart()
    {
        try
        {
            return RunElevated("schtasks", $"/Delete /TN \"{TaskName}\" /F", out _);
        }
        catch (Exception ex)
        {
            Storage.LogError($"删除计划任务失败: {ex.Message}");
            return false;
        }
    }

    private static bool RunElevated(string file, string args, out string output)
    {
        output = "";
        var psi = new ProcessStartInfo(file, args)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        using var p = Process.Start(psi);
        p?.WaitForExit(10000);
        return p?.ExitCode == 0;
    }

    // ---------- 更新检查 ----------
    public static async Task<GitHubRelease?> CheckForUpdateAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("WifiRoam");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize(json, ReleaseJsonContext.Default.GitHubRelease);
        }
        catch (Exception ex)
        {
            Storage.LogError($"检查更新失败: {ex.Message}");
            return null;
        }
    }

    public static bool IsNewerVersion(string? remoteTag, string localVersion)
    {
        if (string.IsNullOrWhiteSpace(remoteTag)) return false;
        var remote = remoteTag.TrimStart('v', 'V');
        return Version.TryParse(remote, out var r)
            && Version.TryParse(localVersion, out var l)
            && r > l;
    }

    public static async Task<bool> DownloadUpdateAsync(GitHubRelease release)
    {
        try
        {
            var asset = release.Assets is { Length: > 0 }
                ? release.Assets[0]
                : null;
            if (asset?.BrowserDownloadUrl == null) return false;

            var target = Path.Combine(AppContext.BaseDirectory, "WifiRoam_update.exe");
            using var resp = await _http.GetAsync(asset.BrowserDownloadUrl);
            if (!resp.IsSuccessStatusCode) return false;

            await using var fs = File.Create(target);
            await resp.Content.CopyToAsync(fs);
            Storage.Log($"新版本已下载: {target}");
            return true;
        }
        catch (Exception ex)
        {
            Storage.LogError($"下载更新失败: {ex.Message}");
            return false;
        }
    }

    // ---------- 通知 ----------
    public static void Notify(string title, string message)
    {
        if (!Storage.Config.Notifications) return;
        Storage.Log($"[通知] {title}: {message}");
        // 简单实现：只写日志。如需弹窗，后续可接入 Windows Toast。
    }
}
