using System.Threading.Tasks;

namespace Artemis.Desktop.Services.Interfaces;

public interface IClipboardService
{
    Task ClearClipboardAsync();
    void ScheduleClear(int delaySeconds);
}
