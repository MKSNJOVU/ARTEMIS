using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Artemis.Desktop.Models;

namespace Artemis.Desktop.Services;

public sealed class RecentFilesService : IRecentFilesService
{
    private const int MaxEntries = 12;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly ISettingsService _settings;
    private readonly string _filePath;
    private List<RecentFileEntry> _entries = [];

    public RecentFilesService(ISettingsService settings)
    {
        _settings = settings;
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Artemis");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "recent-files.json");
        Load();
    }

    public IReadOnlyList<RecentFileEntry> Get(string kind)
    {
        if (!_settings.Current.RememberRecentFiles)
            return [];

        return _entries
            .Where(entry => string.Equals(entry.Kind, kind, StringComparison.OrdinalIgnoreCase))
            .Where(entry => File.Exists(entry.Path))
            .OrderByDescending(entry => entry.LastUsed)
            .Take(MaxEntries)
            .ToList();
    }

    public void Add(string path, string kind)
    {
        if (!_settings.Current.RememberRecentFiles || string.IsNullOrWhiteSpace(path))
            return;

        _entries.RemoveAll(entry =>
            string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.Kind, kind, StringComparison.OrdinalIgnoreCase));

        _entries.Insert(0, new RecentFileEntry
        {
            Path = path,
            Kind = kind,
            LastUsed = DateTimeOffset.UtcNow
        });

        _entries = _entries
            .GroupBy(entry => entry.Kind)
            .SelectMany(group => group.Take(MaxEntries))
            .ToList();

        Save();
    }

    public void Clear()
    {
        _entries.Clear();
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            string json = File.ReadAllText(_filePath);
            _entries = JsonSerializer.Deserialize<List<RecentFileEntry>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            _entries = [];
        }
    }

    private void Save()
    {
        string json = JsonSerializer.Serialize(_entries, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
