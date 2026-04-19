using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WinNotch.Plugins;

/// <summary>
/// Service for discovering and downloading plugins from the online plugin library.
/// </summary>
public class PluginLibraryService
{
    private const string DefaultLibraryUrl = "https://raw.githubusercontent.com/N3uralCreativity/WinNotch-Plugins/main/library.json";
    private readonly HttpClient _httpClient;
    private List<PluginManifest> _availablePlugins = new();

    public IReadOnlyList<PluginManifest> AvailablePlugins => _availablePlugins.AsReadOnly();

    public event Action? LibraryUpdated;

    public string? LastError { get; private set; }

    public PluginLibraryService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WinNotch/0.3.1");
    }

    /// <summary>
    /// Fetch the latest plugin library from GitHub.
    /// </summary>
    public async Task<bool> RefreshLibraryAsync(string? customUrl = null)
    {
        try
        {
            var url = customUrl ?? DefaultLibraryUrl;
            var json = await _httpClient.GetStringAsync(url);

            var library = JsonSerializer.Deserialize<PluginLibrary>(json);
            if (library?.Plugins != null)
            {
                _availablePlugins = library.Plugins;
                LibraryUpdated?.Invoke();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            System.Diagnostics.Debug.WriteLine($"Failed to refresh plugin library: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Download a plugin from the library to the local plugins directory.
    /// </summary>
    public async Task<string?> DownloadPluginAsync(string pluginId, string targetDirectory, IProgress<double>? progress = null)
    {
        var manifest = _availablePlugins.FirstOrDefault(p => p.Id == pluginId);
        if (manifest == null)
        {
            throw new InvalidOperationException($"Plugin {pluginId} not found in library");
        }

        if (string.IsNullOrEmpty(manifest.DownloadUrl))
        {
            throw new InvalidOperationException($"Plugin {pluginId} has no download URL");
        }

        try
        {
            // Create plugin-specific directory
            var pluginDir = Path.Combine(targetDirectory, SanitizeFileName(pluginId));
            Directory.CreateDirectory(pluginDir);

            var fileName = $"{SanitizeFileName(manifest.Name)}.dll";
            var targetPath = Path.Combine(pluginDir, fileName);

            // Download the plugin DLL
            using var response = await _httpClient.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    progress?.Report((double)downloadedBytes / totalBytes * 100);
                }
            }

            // Verify SHA256 if provided
            if (!string.IsNullOrEmpty(manifest.Sha256))
            {
                var computedHash = await ComputeSha256Async(targetPath);
                if (!computedHash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(targetPath);
                    throw new InvalidOperationException("Plugin file hash verification failed. The file may be corrupted or tampered with.");
                }
            }

            // Save manifest alongside the DLL
            var manifestPath = Path.Combine(pluginDir, "manifest.json");
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, manifestJson);

            return targetPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to download plugin {pluginId}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Check if a newer version of a plugin is available.
    /// </summary>
    public bool IsUpdateAvailable(string pluginId, string currentVersion)
    {
        var manifest = _availablePlugins.FirstOrDefault(p => p.Id == pluginId);
        if (manifest == null) return false;

        try
        {
            var current = new Version(currentVersion);
            var available = new Version(manifest.Version);
            return available > current;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Search plugins by name or description.
    /// </summary>
    public IEnumerable<PluginManifest> SearchPlugins(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _availablePlugins;

        query = query.ToLowerInvariant();
        return _availablePlugins.Where(p =>
            p.Name.ToLowerInvariant().Contains(query) ||
            p.Description.ToLowerInvariant().Contains(query) ||
            p.Author.ToLowerInvariant().Contains(query));
    }

    /// <summary>
    /// Filter plugins by category.
    /// </summary>
    public IEnumerable<PluginManifest> GetPluginsByCategory(PluginCategory category)
    {
        return _availablePlugins.Where(p => p.Category == category);
    }

    private async Task<string> ComputeSha256Async(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await Task.Run(() => sha256.ComputeHash(stream));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    private class PluginLibrary
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("plugins")]
        public List<PluginManifest> Plugins { get; set; } = new();
    }
}
