using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace WinNotch.Services;

public sealed class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/N3uralCreativity/WinNotch/releases/latest";

    private static readonly HttpClient _http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "WinNotch-Updater" },
            { "Accept", "application/vnd.github+json" }
        },
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Separate client for large binary downloads — no API headers, longer timeout
    private static readonly HttpClient _downloadHttp = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10
    })
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "WinNotch-Updater" }
        },
        Timeout = TimeSpan.FromMinutes(10)
    };

    public string CurrentVersion { get; }
    public string? LatestVersion { get; private set; }
    public string? InstallerDownloadUrl { get; private set; }
    public bool IsUpdateAvailable => LatestVersion != null
        && Version.TryParse(LatestVersion, out var remote)
        && Version.TryParse(CurrentVersion, out var local)
        && remote > local;

    public UpdateService()
    {
        var asm = Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        CurrentVersion = ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "0.0.0";
    }

    public async Task<bool> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var release = await _http.GetFromJsonAsync<GitHubRelease>(GitHubApiUrl, ct);
            if (release?.TagName == null) return false;

            // Strip leading 'v' from tag (e.g. "v0.2.0" → "0.2.0")
            LatestVersion = release.TagName.TrimStart('v', 'V');

            // Find the installer asset (Setup.exe)
            InstallerDownloadUrl = null;
            if (release.Assets != null)
            {
                foreach (var asset in release.Assets)
                {
                    if (asset.Name != null &&
                        asset.Name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        InstallerDownloadUrl = asset.BrowserDownloadUrl;
                        break;
                    }
                }
            }

            return IsUpdateAvailable;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DownloadAndLaunchInstallerAsync(
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (InstallerDownloadUrl == null) return false;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "WinNotch-Update");
            Directory.CreateDirectory(tempDir);
            var installerPath = Path.Combine(tempDir, $"WinNotch-{LatestVersion}-Setup.exe");

            // Download with progress using dedicated download client
            using var response = await _downloadHttp.GetAsync(InstallerDownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(installerPath, FileMode.Create,
                FileAccess.Write, FileShare.None, 8192, useAsync: true);

            var buffer = new byte[8192];
            long bytesRead = 0;
            int read;
            while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesRead += read;
                if (totalBytes > 0)
                    progress?.Report((double)bytesRead / totalBytes);
            }

            progress?.Report(1.0);

            // Launch installer and exit the app
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            // Shut down the current app so the installer can replace files
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    // --- GitHub API DTOs ---

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
