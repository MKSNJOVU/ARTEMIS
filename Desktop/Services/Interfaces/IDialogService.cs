using System.Threading.Tasks;
namespace Artemis.Desktop.Services;

public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string? message);
}