using System.Threading.Tasks;

namespace Artemis.Desktop.Services.Interfaces;

public interface IClipboardService
{
    Task ClearClipboardAsync();
}
