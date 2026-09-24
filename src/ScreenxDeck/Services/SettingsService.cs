using System;
using System.IO;
using System.Text.Json;
using ScreenxDeck.Core.Models;
using Serilog;

namespace ScreenxDeck.Services;

public sealed class SettingsService
{
    private readonly ILogger _logger;
    private readonly string _settingsFilePath;
    private readonly object _lock = new();
    private PluginSettings _settings;

    public PluginSettings Current
    {
        get
        {
            lock (_lock)
            {
                return _settings;
            }
        }
    }

    public SettingsService(ILogger logger)
    {
        _logger = logger.ForContext<SettingsService>();

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string configDir = Path.Combine(appData, "MacroDeck", "config", "com.cjhackeryt.screenxdeck");
        Directory.CreateDirectory(configDir);

        _settingsFilePath = Path.Combine(configDir, "settings.json");
        _settings = LoadSettings();
    }

    public void SaveSettings()
    {
        lock (_lock)
        {
            try
            {
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
                _logger.Information("Saved ScreenxDeck settings to {Path}", _settingsFilePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save settings to {Path}", _settingsFilePath);
            }
        }
    }

    private PluginSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<PluginSettings>(json);
                if (loaded != null)
                {
                    _logger.Information("Loaded settings from {Path}", _settingsFilePath);
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error reading settings file, reverting to default settings");
        }

        var defaultSettings = new PluginSettings();
        // Seed default sample profiles
        defaultSettings.SavedProfiles.Add(new DisplayProfile
        {
            Name = "Gaming",
            Description = "High refresh rate, primary monitor focused",
            DisplayMode = "Extend"
        });
        defaultSettings.SavedProfiles.Add(new DisplayProfile
        {
            Name = "Work",
            Description = "Multi-monitor extended layout with standard 60Hz and comfortable brightness",
            DisplayMode = "Extend"
        });
        defaultSettings.SavedProfiles.Add(new DisplayProfile
        {
            Name = "Movie",
            Description = "Cinema viewing setup",
            DisplayMode = "Extend"
        });

        return defaultSettings;
    }

    public void Update(Action<PluginSettings> updateAction)
    {
        lock (_lock)
        {
            updateAction(_settings);
            SaveSettings();
        }
    }
}
