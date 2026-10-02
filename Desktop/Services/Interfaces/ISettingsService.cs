using System;
using Artemis.Desktop.Models;

namespace Artemis.Desktop.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    event EventHandler? Changed;
    void Load();
    void Save();
    void Update(Action<AppSettings> mutator);
}
