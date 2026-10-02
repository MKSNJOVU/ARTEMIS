using System;
using System.IO;
using System.Text.Json;
using Artemis.Desktop.Models;

namespace Artemis.Desktop.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    public AppSettings Current { get; private set; } = new();
    public event EventHandler? Changed;

    public SettingsService()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Artemis");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "settings.json");
        Load();
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            string json = File.ReadAllText(_filePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(_filePath, json);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Update(Action<AppSettings> mutator)
    {
        mutator(Current);
        Save();
    }
}
