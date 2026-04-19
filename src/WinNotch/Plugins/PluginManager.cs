using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using WinNotch.Models;

namespace WinNotch.Plugins;

/// <summary>
/// Manages plugin loading, initialization, and lifecycle.
/// </summary>
public class PluginManager : IDisposable
{
    private readonly IPluginContext _context;
    private readonly List<IPlugin> _loadedPlugins = new();
    private readonly Dictionary<string, bool> _pluginEnabledState = new();
    private readonly string _pluginsDirectory;

    public IReadOnlyList<IPlugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

    public event Action<IPlugin>? PluginLoaded;
    public event Action<IPlugin>? PluginUnloaded;
    public event Action<IPlugin, Exception>? PluginError;

    public PluginManager(IPluginContext context)
    {
        _context = context;

        _pluginsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinNotch",
            "Plugins");

        Directory.CreateDirectory(_pluginsDirectory);
        LoadPluginStates();
    }

    /// <summary>
    /// Discover and load all plugins from the plugins directory.
    /// </summary>
    public async Task LoadAllPluginsAsync()
    {
        _context.Log("Starting plugin discovery...", PluginLogLevel.Info);

        var pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.AllDirectories);

        foreach (var file in pluginFiles)
        {
            try
            {
                await LoadPluginFromFileAsync(file);
            }
            catch (Exception ex)
            {
                _context.Log($"Failed to load plugin from {file}: {ex.Message}", PluginLogLevel.Error);
            }
        }

        _context.Log($"Loaded {_loadedPlugins.Count} plugins", PluginLogLevel.Info);
    }

    /// <summary>
    /// Load a plugin from a DLL file.
    /// </summary>
    public async Task<IPlugin?> LoadPluginFromFileAsync(string filePath)
    {
        try
        {
            _context.Log($"Loading plugin from: {filePath}", PluginLogLevel.Debug);

            var assembly = Assembly.LoadFrom(filePath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            if (pluginTypes.Count == 0)
            {
                _context.Log($"No plugin types found in {filePath}", PluginLogLevel.Warning);
                return null;
            }

            foreach (var type in pluginTypes)
            {
                var plugin = Activator.CreateInstance(type) as IPlugin;
                if (plugin == null) continue;

                // Check if plugin is already loaded
                if (_loadedPlugins.Any(p => p.Id == plugin.Id))
                {
                    _context.Log($"Plugin {plugin.Id} is already loaded", PluginLogLevel.Warning);
                    continue;
                }

                // Verify minimum WinNotch version
                if (!IsVersionCompatible(plugin.MinimumWinNotchVersion))
                {
                    _context.Log($"Plugin {plugin.Id} requires WinNotch {plugin.MinimumWinNotchVersion} or higher", PluginLogLevel.Error);
                    continue;
                }

                // Initialize the plugin
                await plugin.InitializeAsync(_context);
                _loadedPlugins.Add(plugin);

                // Auto-enable if previously enabled or first time
                if (!_pluginEnabledState.ContainsKey(plugin.Id))
                {
                    _pluginEnabledState[plugin.Id] = true; // Default to enabled
                }

                if (_pluginEnabledState[plugin.Id])
                {
                    await plugin.OnEnableAsync();
                }

                PluginLoaded?.Invoke(plugin);
                _context.Log($"Successfully loaded plugin: {plugin.Name} v{plugin.Version}", PluginLogLevel.Info);

                return plugin;
            }
        }
        catch (Exception ex)
        {
            _context.Log($"Error loading plugin from {filePath}: {ex.Message}", PluginLogLevel.Error);
            throw;
        }

        return null;
    }

    /// <summary>
    /// Enable a plugin by ID.
    /// </summary>
    public async Task<bool> EnablePluginAsync(string pluginId)
    {
        var plugin = _loadedPlugins.FirstOrDefault(p => p.Id == pluginId);
        if (plugin == null) return false;

        try
        {
            await plugin.OnEnableAsync();
            _pluginEnabledState[pluginId] = true;
            SavePluginStates();
            return true;
        }
        catch (Exception ex)
        {
            _context.Log($"Error enabling plugin {pluginId}: {ex.Message}", PluginLogLevel.Error);
            PluginError?.Invoke(plugin, ex);
            return false;
        }
    }

    /// <summary>
    /// Disable a plugin by ID.
    /// </summary>
    public async Task<bool> DisablePluginAsync(string pluginId)
    {
        var plugin = _loadedPlugins.FirstOrDefault(p => p.Id == pluginId);
        if (plugin == null) return false;

        try
        {
            await plugin.OnDisableAsync();
            _pluginEnabledState[pluginId] = false;
            SavePluginStates();
            return true;
        }
        catch (Exception ex)
        {
            _context.Log($"Error disabling plugin {pluginId}: {ex.Message}", PluginLogLevel.Error);
            PluginError?.Invoke(plugin, ex);
            return false;
        }
    }

    /// <summary>
    /// Unload a plugin by ID.
    /// </summary>
    public async Task<bool> UnloadPluginAsync(string pluginId)
    {
        var plugin = _loadedPlugins.FirstOrDefault(p => p.Id == pluginId);
        if (plugin == null) return false;

        try
        {
            if (_pluginEnabledState.GetValueOrDefault(pluginId))
            {
                await plugin.OnDisableAsync();
            }

            await plugin.ShutdownAsync();
            plugin.Dispose();

            _loadedPlugins.Remove(plugin);
            PluginUnloaded?.Invoke(plugin);

            _context.Log($"Unloaded plugin: {plugin.Name}", PluginLogLevel.Info);
            return true;
        }
        catch (Exception ex)
        {
            _context.Log($"Error unloading plugin {pluginId}: {ex.Message}", PluginLogLevel.Error);
            PluginError?.Invoke(plugin, ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a plugin is enabled.
    /// </summary>
    public bool IsPluginEnabled(string pluginId)
    {
        return _pluginEnabledState.GetValueOrDefault(pluginId, false);
    }

    /// <summary>
    /// Get all UI plugins.
    /// </summary>
    public IEnumerable<IUIPlugin> GetUIPlugins()
    {
        return _loadedPlugins.OfType<IUIPlugin>()
            .Where(p => _pluginEnabledState.GetValueOrDefault(p.Id, false));
    }

    /// <summary>
    /// Get all service plugins.
    /// </summary>
    public IEnumerable<IServicePlugin> GetServicePlugins()
    {
        return _loadedPlugins.OfType<IServicePlugin>()
            .Where(p => _pluginEnabledState.GetValueOrDefault(p.Id, false));
    }

    /// <summary>
    /// Get all animation plugins.
    /// </summary>
    public IEnumerable<IAnimationPlugin> GetAnimationPlugins()
    {
        return _loadedPlugins.OfType<IAnimationPlugin>()
            .Where(p => _pluginEnabledState.GetValueOrDefault(p.Id, false));
    }

    /// <summary>
    /// Get plugin directory path.
    /// </summary>
    public string GetPluginsDirectory() => _pluginsDirectory;

    private bool IsVersionCompatible(string requiredVersion)
    {
        try
        {
            var currentVersion = new Version("0.3.1"); // WinNotch version
            var required = new Version(requiredVersion);
            return currentVersion >= required;
        }
        catch
        {
            return true; // If parsing fails, allow it
        }
    }

    private void LoadPluginStates()
    {
        try
        {
            var statePath = Path.Combine(_pluginsDirectory, "plugin-states.json");
            if (File.Exists(statePath))
            {
                var json = File.ReadAllText(statePath);
                var states = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                if (states != null)
                {
                    foreach (var kvp in states)
                    {
                        _pluginEnabledState[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _context.Log($"Failed to load plugin states: {ex.Message}", PluginLogLevel.Warning);
        }
    }

    private void SavePluginStates()
    {
        try
        {
            var statePath = Path.Combine(_pluginsDirectory, "plugin-states.json");
            var json = System.Text.Json.JsonSerializer.Serialize(_pluginEnabledState, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(statePath, json);
        }
        catch (Exception ex)
        {
            _context.Log($"Failed to save plugin states: {ex.Message}", PluginLogLevel.Warning);
        }
    }

    public async void Dispose()
    {
        foreach (var plugin in _loadedPlugins.ToList())
        {
            try
            {
                await plugin.ShutdownAsync();
                plugin.Dispose();
            }
            catch (Exception ex)
            {
                _context.Log($"Error disposing plugin {plugin.Id}: {ex.Message}", PluginLogLevel.Error);
            }
        }

        _loadedPlugins.Clear();
        GC.SuppressFinalize(this);
    }
}
