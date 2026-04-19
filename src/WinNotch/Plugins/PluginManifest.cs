using System;
using System.Text.Json.Serialization;

namespace WinNotch.Plugins;

/// <summary>
/// Metadata about a plugin from the plugin library.
/// </summary>
public class PluginManifest
{
    /// <summary>Unique plugin identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Plugin version.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>Author name.</summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>Description of the plugin.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Minimum WinNotch version required.</summary>
    [JsonPropertyName("minimumWinNotchVersion")]
    public string MinimumWinNotchVersion { get; set; } = "0.2.3";

    /// <summary>Direct download URL for the plugin DLL.</summary>
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>Homepage or repository URL.</summary>
    [JsonPropertyName("homepage")]
    public string Homepage { get; set; } = string.Empty;

    /// <summary>Icon URL (optional).</summary>
    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    /// <summary>Plugin category.</summary>
    [JsonPropertyName("category")]
    public PluginCategory Category { get; set; } = PluginCategory.Other;

    /// <summary>Required permissions.</summary>
    [JsonPropertyName("permissions")]
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>Plugin dependencies (other plugin IDs).</summary>
    [JsonPropertyName("dependencies")]
    public string[] Dependencies { get; set; } = Array.Empty<string>();

    /// <summary>SHA256 hash of the DLL for verification (optional but recommended).</summary>
    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    /// <summary>Release date.</summary>
    [JsonPropertyName("releaseDate")]
    public DateTime? ReleaseDate { get; set; }

    /// <summary>Whether this plugin is verified/official.</summary>
    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PluginCategory
{
    Animation,
    Integration,
    Productivity,
    Media,
    SystemUtility,
    Theme,
    Widget,
    Other
}
