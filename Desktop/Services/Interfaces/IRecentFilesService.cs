using System.Collections.Generic;
using Artemis.Desktop.Models;

namespace Artemis.Desktop.Services;

public interface IRecentFilesService
{
    IReadOnlyList<RecentFileEntry> Get(string kind);
    void Add(string path, string kind);
    void Clear();
}
