using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace StockTracker;

/// <summary>
/// 在线更新服务：负责版本检测、二进制下载及自我替换重启
/// </summary>
public static class UpdateService
{
    private const string README_URL = "https://raw.githubusercontent.com/Joker-smile/StockTracker/master/README.md";

    public record UpdateInfo(string Version, string DownloadUrl, bool HasUpdate);

    /// <summary>
    /// 检查更新
    /// </summary>
    public static async Task<UpdateInfo> CheckForUpdateAsync()
    {
        try
        {
            using var client = NetworkHelper.CreateSSLHttpClient(15);
            string readme = await client.GetStringAsync(README_URL);

            // 匹配版本号标记，例如 ### Current Version: v1.1.0
            var versionMatch = Regex.Match(readme, @"Current Version:\s*(v\d+\.\d+\.\d+)");
            if (!versionMatch.Success)
                return new UpdateInfo(Program.APP_VERSION, "", false);

            string latestVersion = versionMatch.Groups[1].Value;
            bool hasUpdate = IsNewerVersion(latestVersion, Program.APP_VERSION);

            // 解析下载地址
            string downloadUrl = ParseDownloadUrl(readme);

            return new UpdateInfo(latestVersion, downloadUrl, hasUpdate);
        }
        catch (Exception ex)
        {
            Program.LogError("Update Check Failed", ex);
            return new UpdateInfo(Program.APP_VERSION, "", false);
        }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var v1 = new Version(latest.TrimStart('v'));
            var v2 = new Version(current.TrimStart('v'));
            return v1 > v2;
        }
        catch { return false; }
    }

    private static string ParseDownloadUrl(string readme)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows x64
            var match = Regex.Match(readme, @"\[下载 StockTracker\.exe\]\((.*?win-x64.*?\.exe)\)");
            return match.Success ? match.Groups[1].Value : "";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Mac ARM 或 Intel
            bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
            string pattern = isArm 
                ? @"\[下载 StockTracker\]\((.*?osx-arm64.*?StockTracker)\)"
                : @"\[下载 StockTracker\]\((.*?osx-x64.*?StockTracker)\)";
            var match = Regex.Match(readme, pattern);
            return match.Success ? match.Groups[1].Value : "";
        }
        return "";
    }

    /// <summary>
    /// 开始下载并执行安装
    /// </summary>
    public static async Task DownloadAndInstallAsync(string downloadUrl, Action<double> onProgress)
    {
        string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        string tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(currentExe) + "_new");

        try
        {
            using var client = NetworkHelper.CreateSSLHttpClient(60);
            var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;
                if (totalBytes.HasValue)
                {
                    onProgress?.Invoke((double)totalRead / totalBytes.Value);
                }
            }
            fileStream.Close();

            // 执行替换逻辑
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RunWindowsUpdater(currentExe, tempFile);
            }
            else
            {
                RunUnixUpdater(currentExe, tempFile);
            }

            // 立即退出主程序
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Program.LogError("Update Download/Install Failed", ex);
            if (File.Exists(tempFile)) File.Delete(tempFile);
            throw;
        }
    }

    private static void RunWindowsUpdater(string currentExe, string tempFile)
    {
        string batFile = Path.Combine(Path.GetTempPath(), "StockTrackerUpdater.bat");
        string exeName = Path.GetFileName(currentExe);
        string exeDir = Path.GetDirectoryName(currentExe) ?? "";

        string script = $@"
@echo off
timeout /t 2 /nobreak > nul
cd /d ""{exeDir}""
del ""{exeName}""
move /y ""{tempFile}"" ""{exeName}""
start """" ""{exeName}""
del ""%~f0""
";
        File.WriteAllText(batFile, script, System.Text.Encoding.Default);
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batFile}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    private static void RunUnixUpdater(string currentExe, string tempFile)
    {
        string exeDir = Path.GetDirectoryName(currentExe) ?? "";
        string exeName = Path.GetFileName(currentExe);

        // macOS: sleep 2 -> move -> chmod +x -> open
        string script = $"sleep 2 && mv \"{tempFile}\" \"{currentExe}\" && chmod +x \"{currentExe}\" && open \"{currentExe}\"";
        
        Process.Start(new ProcessStartInfo
        {
            FileName = "sh",
            Arguments = $"-c \"{script}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }
}
