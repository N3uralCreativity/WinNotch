using System.Reflection;

namespace WinNotch.Helpers;

/// <summary>
/// Single source of truth for the application's version, read from the assembly at
/// runtime. The assembly version is stamped from the git tag at release time (see
/// .github/workflows/release.yml), so every version shown in the app — settings,
/// "check for update", the plugin User-Agent, compatibility checks — stays in sync
/// with the released version automatically, with no hardcoded strings to update.
/// </summary>
public static class AppInfo
{
    /// <summary>Version as "Major.Minor.Build" (e.g. "0.7.0").</summary>
    public static string Version { get; } = ResolveVersion();

    /// <summary>Version prefixed with 'v' (e.g. "v0.7.0").</summary>
    public static string DisplayVersion { get; } = "v" + Version;

    /// <summary>Product name plus version (e.g. "WinNotch v0.7.0").</summary>
    public static string NameWithVersion { get; } = "WinNotch " + DisplayVersion;

    private static string ResolveVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version != null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";
    }
}
